#include "obd_protocol.h"
#include "esp_timer.h"
#include <stdio.h>

#define OBD_FUNCTIONAL_REQUEST_ID_11BIT 0x7DF
#define OBD_CAN_FRAME_LENGTH 8
#define OBD_SINGLE_FRAME_PAYLOAD_LENGTH 2

#define OBD_RESPONSE_ID_MIN_11BIT 0x7E8
#define OBD_RESPONSE_ID_MAX_11BIT 0x7EF

bool obd_build_pid_request(const obd_pid_request_t *request, can_bus_frame_t *out_frame)
{
    if (request == NULL || out_frame == NULL)
    {
        return false;
    }

    *out_frame = (can_bus_frame_t){0};

    out_frame->id = OBD_FUNCTIONAL_REQUEST_ID_11BIT;
    out_frame->dlc = OBD_CAN_FRAME_LENGTH;
    out_frame->data_length = OBD_CAN_FRAME_LENGTH;

    out_frame->is_extended = false;
    out_frame->is_remote = false;

    out_frame->data[0] = OBD_SINGLE_FRAME_PAYLOAD_LENGTH;
    out_frame->data[1] = request->mode;
    out_frame->data[2] = request->pid;

    return true;
}

bool obd_decode_pid_response(const can_bus_frame_t *frame, obd_pid_value_t *out_value)
{
    if (frame == NULL || out_value == NULL)
    {
        return false;
    }

    /*
     * OBD PID responses must contain data, must not be RTR frames,
     * and this implementation currently handles only standard
     * ISO-TP Single Frames.
     */

    if (frame->is_remote || frame->is_extended ||  frame->data_length < 4)
    {
        return false;
    }

    /*
     * ISO-TP Single Frame:
     *
     * upper nibble = frame type (0 = Single Frame)
     * lower nibble = diagnostic payload length
     *
     * Example:
     * 04 41 0C 1A F8 ...
     *
     * 0x04 means: Single Frame containing 4 payload bytes.
     */

    uint8_t frame_type =  frame->data[0] >> 4;
    uint8_t payload_length = frame->data[0] & 0x0F;

    if (frame_type != 0)
    {
        return false;
    }

    /*
     * Byte 1 must be a positive response to Mode 01.
     *
     * Request:  0x01
     * Response: 0x41
     */

    if (frame->data[1] != (OBD_MODE_CURRENT_DATA + 0x40))
    {
        return false;
    }

    uint8_t pid = frame->data[2];


    switch (pid)
    {
        case OBD_PID_ENGINE_RPM:
        {
            if (payload_length < 4 || frame->data_length < 5)
            {
                return false;
            }

            uint8_t a = frame->data[3];
            uint8_t b = frame->data[4];

            out_value->value = ((a * 256.0f) + b) / 4.0f;

            out_value->pid = pid;

            return true;   
        }

        case OBD_PID_COOLANT_TEMP:
        {
            if (payload_length < 3 || frame->data_length < 4)
            {
                return false;
            }

            uint8_t a = frame->data[3];

            out_value->value = (float)a - 40.0f;

            out_value->pid = pid;

            return true;
        }

        case OBD_PID_VEHICLE_SPEED:
        {
            if (payload_length < 3 || frame->data_length < 4)
            {
                return false;
            }

            uint8_t a = frame->data[3];

            out_value->value = (float)a;

            out_value->pid = pid;

            return true;
        }

        case OBD_PID_ENGINE_LOAD:
        {
            if (payload_length < 3 || frame->data_length < 4)
            {
                return false;
            }

            uint8_t a = frame->data[3];

            out_value->value = ((float)a * 100.0f) / 255.0f;

            out_value->pid = pid;

            return true;
        }
        default:
            return false;
    }

    
}
esp_err_t obd_request_pid(uint8_t pid, uint32_t timeout_ms)

{
    obd_pid_request_t request = {
        .mode = OBD_MODE_CURRENT_DATA,
        .pid = pid
    };

    can_bus_frame_t frame;

    if (!obd_build_pid_request(&request, &frame))
    {
        return ESP_FAIL;
    }

    return can_bus_transmit(&frame, timeout_ms);
}

esp_err_t obd_wait_pid_response(uint8_t pid, obd_pid_value_t *out_value, uint32_t timeout_ms)
{
    if (out_value == NULL)
    {
        return ESP_ERR_INVALID_ARG;
    }

    /*
     * Use one absolute deadline for the whole request/response wait.
     * Otherwise every unrelated CAN frame could restart the timeout.
     */

    int64_t deadline_us = esp_timer_get_time() + ((int64_t)timeout_ms * 1000);

    while (true)
    {
        int64_t remaining_us = deadline_us - esp_timer_get_time();

        if (remaining_us <= 0)
        {
            return ESP_ERR_TIMEOUT;
        }

        /*
         * can_bus_receive() accepts milliseconds.
         * Round upward so a remaining fraction of a millisecond does not
         * accidentally become a zero-length wait.
         */

        uint32_t remaining_ms = (uint32_t)((remaining_us + 999) / 1000);

        can_bus_frame_t frame;

        esp_err_t result = can_bus_receive(&frame, remaining_ms);

        if (result == ESP_ERR_TIMEOUT)
        {
            return ESP_ERR_TIMEOUT;
        }

        if (result != ESP_OK)
        {
            return result;
        }

         /*
         * This first implementation expects standard 11-bit OBD-II
         * responses from the usual 0x7E8-0x7EF response range.
         */

        if (frame.is_extended || frame.is_remote)
        {
            continue;
        }

        if (frame.id < OBD_RESPONSE_ID_MIN_11BIT || frame.id > OBD_RESPONSE_ID_MAX_11BIT)
        {
            continue;
        }

        obd_pid_value_t decoded_value;

        if (!obd_decode_pid_response(&frame, &decoded_value))
        {
            continue;
        }

        /*
         * Another ECU/PID response may arrive while we are waiting.
         * Only accept the PID requested by the caller.
         */

        if (decoded_value.pid != pid)
        {
            continue;
        }

        *out_value = decoded_value;

        return ESP_OK;
    }


}

bool obd_decode_dtc(uint8_t high_byte, uint8_t low_byte, obd_dtc_t *out_dtc)
{
    if (out_dtc == NULL)
    {
        return false;
    }

    /*
     * 00 -> P
     * 01 -> C
     * 10 -> B
     * 11 -> U
     */

    static const char system_chars[] = {'P', 'C', 'B', 'U'};

    uint8_t system_bits = (high_byte >> 6) & 0x03;
    uint8_t first_digit = (high_byte >> 4) & 0x03;
    uint8_t second_digit = high_byte & 0x0F;

    uint8_t third_digit = (low_byte >> 4) & 0x0F;
    uint8_t fourth_digit = low_byte & 0x0F;

    out_dtc->raw_high = high_byte;
    out_dtc->raw_low = low_byte;

    snprintf(out_dtc->code, sizeof(out_dtc->code), "%c%X%X%X%X", system_chars[system_bits], first_digit, second_digit, third_digit, fourth_digit);

    return true;
}

bool obd_build_dtc_request(can_bus_frame_t *out_frame)
{
    if (out_frame == NULL)
    {
        return false;
    }

    *out_frame = (can_bus_frame_t){0};

    out_frame->id = OBD_FUNCTIONAL_REQUEST_ID_11BIT;
    out_frame->dlc = OBD_CAN_FRAME_LENGTH;
    out_frame->data_length = OBD_CAN_FRAME_LENGTH;

    out_frame->is_extended = false;
    out_frame->is_remote = false;

    /*
     * ISO-TP Single Frame:
     *
     * 01 = one diagnostic payload byte follows
     * 03 = OBD Mode 03, request stored DTCs
     */

    out_frame->data[0] = 0x01;
    out_frame->data[1] = OBD_MODE_STORED_DTC;

    return true;
}

bool obd_decode_dtc_response(const can_bus_frame_t *frame, obd_dtc_list_t *out_list)
{
    if (frame == NULL || out_list == NULL)
    {
        return false;
    }

    out_list->count = 0;

    if (frame->is_extended || frame->is_remote || frame->data_length < 2)
    {
        return false;
    }

    uint8_t frame_type = frame->data[0] >> 4;
    uint8_t payload_length = frame->data[0] & 0x0F;

    /*
     * MVP implementation currently handles an ISO-TP Single Frame.
     */

    if (frame_type != 0)
    {
        return false;
    }

    if (payload_length < 1)
    {
        return false;
    }

    if ((uint16_t)payload_length + 1 > frame->data_length)
    {
        return false;
    }

    /*
     * Mode 03 positive response:
     *
     * request  = 0x03
     * response = 0x43
     */

    if (frame->data[1] != (OBD_MODE_STORED_DTC + 0x40))
    {
        return false;
    }

    uint8_t dtc_bytes = payload_length - 1;

    /*
     * Every DTC occupies exactly two bytes.
     */

    if ((dtc_bytes % 2) != 0)
    {
        return false;
    }

    for (uint8_t index = 0; index < dtc_bytes && out_list->count < OBD_MAX_DTCS_SINGLE_FRAME; index +=2)
    {
        uint8_t high_byte = frame->data[2 + index];
        uint8_t low_byte = frame->data[3 + index];

        /*
         * 00 00 means an unused/empty DTC slot.
         */

        if (high_byte == 0x00 && low_byte == 0x00)
        {
            continue;
        }

        obd_dtc_t *dtc = &out_list->dtcs[out_list->count];

        if (!obd_decode_dtc(high_byte, low_byte, dtc))
        {
            return false;
        }

        out_list->count++;
    }

    return true;
}

esp_err_t obd_request_dtcs(uint32_t timeout_ms)
{
    can_bus_frame_t frame;

    if (!obd_build_dtc_request(&frame))
    {
        return ESP_FAIL;
    }

    return can_bus_transmit(&frame, timeout_ms);
}

esp_err_t obd_wait_dtc_response(obd_dtc_list_t *out_list, uint32_t timeout_ms)
{
    if (out_list == NULL)
    {
        return ESP_ERR_INVALID_ARG;
    }

    out_list->count = 0;

    int64_t deadline_us = esp_timer_get_time() + ((int64_t)timeout_ms *1000);

    while (true)
    {
        int64_t remaining_us = deadline_us - esp_timer_get_time();

        if (remaining_us <= 0)
        {
            return ESP_ERR_TIMEOUT;
        }

        uint32_t remaining_ms = (uint32_t)((remaining_us + 999) / 1000);

        can_bus_frame_t frame;

        esp_err_t result = can_bus_receive(&frame, remaining_ms);

        if (result == ESP_ERR_TIMEOUT)
        {
            return ESP_ERR_TIMEOUT;
        }

        if (result != ESP_OK)
        {
            return result;
        }

        if (frame.is_extended || frame.is_remote)
        {
            continue;
        }

        if (frame.id < OBD_RESPONSE_ID_MIN_11BIT || frame.id > OBD_RESPONSE_ID_MAX_11BIT)
        {
            continue;
        }

        if (!obd_decode_dtc_response(&frame, out_list))
        {
            continue;
        }

        return ESP_OK;
    }
}