#include <stdio.h>
#include <stdint.h>
#include <inttypes.h>

#include "driver/uart.h"
#include "esp_err.h"

#include "gateway_protocol.h"
#include "can_bus.h"
#include "can_stats.h"
#include "obd_protocol.h"

#include "freertos/FreeRTOS.h"
#include "freertos/task.h"

#define SERIAL_UART UART_NUM_0
#define UART_RX_BUFFER_SIZE 256
#define REQUEST_LINE_BUFFER_SIZE 128
#define RESPONSE_BUFFER_SIZE 128
#define CAN_MAX_FRAMES_PER_CYCLE 8

#define OBD_VEHICLE_TEST_ENABLED 1

#define CAN_TEST_BITRATE CAN_BUS_BITRATE_500K
#define CAN_AUTO_BITRATE_PROBE_ENABLED 1

/*
 * Sends one group of simulated vehicle values to the serial output.
 *
 * This function is static because it is used only inside this source file.
 * It returns void because it only sends data and does not calculate a result.
 */

 static void initialize_serial_input(void)
 {
    const uart_config_t uart_configuration = {
                .baud_rate = 115200,
                .data_bits = UART_DATA_8_BITS,
                .parity = UART_PARITY_DISABLE,
                .stop_bits = UART_STOP_BITS_1,
                .flow_ctrl = UART_HW_FLOWCTRL_DISABLE,
                .source_clk = UART_SCLK_DEFAULT
            };

            ESP_ERROR_CHECK(
                uart_param_config(SERIAL_UART, &uart_configuration));
            
            ESP_ERROR_CHECK(
                uart_set_pin(
                        SERIAL_UART,
                        UART_PIN_NO_CHANGE,
                        UART_PIN_NO_CHANGE,
                        UART_PIN_NO_CHANGE,
                        UART_PIN_NO_CHANGE));

            ESP_ERROR_CHECK(
                uart_driver_install(
                        SERIAL_UART,
                        UART_RX_BUFFER_SIZE,
                        0,
                        0,
                        NULL,
                        0));
 }


static void send_live_data(int rpm, int coolant_temperature, float battery_voltage)
{
   	printf("LIVE:RPM:%d\n", rpm);
   	printf("LIVE:COOLANT_TEMP:%d\n", coolant_temperature);
	printf("LIVE:BATTERY_VOLTAGE:%f\n", battery_voltage);

	/*
     * Force buffered serial output to be written immediately.
     * This is useful while testing communication with the PC application.
     */
	fflush(stdout);
}

static void process_serial_input(gateway_protocol_t *protocol)
{
    static char request_line[REQUEST_LINE_BUFFER_SIZE];
    static size_t request_length = 0;

    uint8_t received_bytes[32];

    int received_bytes_count = uart_read_bytes(SERIAL_UART, received_bytes, sizeof(received_bytes), pdMS_TO_TICKS(20));

    for (int index = 0; index < received_bytes_count; index++)
    {
        char received_character = (char)received_bytes[index];

        /*if (received_character == '\r')
        {
            continue;
        }*/

        if (received_character == '\n' || received_character == '\r')
        {
            if (request_length > 0 )
            {
                request_line[request_length] = '\0';

                char response_buffer[RESPONSE_BUFFER_SIZE];

                bool response_created = gateway_protocol_handle_line(protocol, request_line, response_buffer, sizeof(response_buffer));

                if (response_created)
                {
                    printf("%s\n", response_buffer);
                }
                else
                {
                    printf("SYS:INVALID_REQUEST\n");
                }

                fflush(stdout);
                request_length = 0;
            }

            continue;
        }

        if (request_length < REQUEST_LINE_BUFFER_SIZE - 1)
        {
            request_line[request_length] = received_character;

            request_length++;
        }
        else
        {
            request_length = 0;
            
            printf("SYS:REQUEST_TOO_LONG\n");

            fflush(stdout);
        }
    }
}

static void start_can_bus(can_bus_mode_t mode, can_bus_bitrate_t bitrate)
{
    esp_err_t result = can_bus_init(mode, bitrate);

    if (result != ESP_OK)
    {
        printf("CAN:ERROR:INIT:%s\n", esp_err_to_name(result));
        fflush(stdout);
        return;
    }

    printf("\nCAN:INITIALIZED\n");
    printf("CAN:BITRATE:%u\n", (unsigned int)bitrate);

    result = can_bus_start();

    if(result != ESP_OK)
    {
        printf("CAN:ERROR:START:%s\n", esp_err_to_name(result));
        can_bus_deinit();
        fflush(stdout);
        return;
    }

    if (mode == CAN_BUS_MODE_PASSIVE)
    {
        printf("CAN:LISTENING\n");
    }
    else
    {
        printf("CAN:DIAGNOSTIC_MODE\n");
    }
    
    fflush(stdout);
}

static uint32_t probe_can_bitrate(can_bus_bitrate_t bitrate, uint32_t duration_ms)
{
    printf("\nCAN:PROBE:START:%u\n", (unsigned int)bitrate);

    start_can_bus(CAN_BUS_MODE_PASSIVE, bitrate);

    if (can_bus_get_status() != CAN_BUS_STATUS_RUNNING)
    {
        printf("CAN:PROBE:START_FAILED:%u\n", (unsigned int)bitrate);

        can_bus_deinit();
        return 0;
    }

    uint32_t total_frames = 0;
    uint32_t standard_frames = 0;
    uint32_t extended_frames = 0;

    TickType_t start_time = xTaskGetTickCount();
    TickType_t duration_ticks = pdMS_TO_TICKS(duration_ms);

    while ((xTaskGetTickCount() - start_time) < duration_ticks)
    {
        can_bus_frame_t frame;

        esp_err_t result = can_bus_receive(&frame, 50);

        if (result == ESP_ERR_TIMEOUT)
        {
            continue;
        }

        if (result != ESP_OK)
        {
            printf("CAN:PROBE:RX_ERROR:%s\n",
            esp_err_to_name(result));

            break;
        }

        total_frames++;

        if (frame.is_extended)
        {
            extended_frames++;
        }
        else 
        {
            standard_frames++;
        }
    }

    printf("CAN:PROBE:RESULT:%u:TOTAL:%lu:STD:%lu:EXT:%lu\n",
        (unsigned int)bitrate,
        (unsigned long)total_frames,
        (unsigned long)standard_frames,
        (unsigned long)extended_frames
    );

    esp_err_t result = can_bus_deinit();

    if (result != ESP_OK)
    {
        printf("CAN:PROBE:DEINIT_ERROR:%s\n", esp_err_to_name(result));
    }

    fflush(stdout);

    return total_frames;
}

static void process_can_input(void)
{
    if (can_bus_get_status() != CAN_BUS_STATUS_RUNNING)
    {
        return;
    }

    can_bus_frame_t frame;

    for (int i = 0; i < CAN_MAX_FRAMES_PER_CYCLE; i++)
    {
        esp_err_t result = can_bus_receive(&frame, 0);

        /*
         * No CAN frame is currently waiting in the RX queue.
         * This is normal and is not treated as an error.
         */

        if (result == ESP_ERR_TIMEOUT)
        {
            break;
        }

        /*
         * A real receive error occurred.
         * Do not use frame here because it may not contain valid data.
         */

        if (result != ESP_OK)
        {
            printf("CAN:ERROR:RECEIVE:%s\n", esp_err_to_name(result));

            fflush(stdout);
            break;
        }

        /*
        * Record statistics only after a CAN frame has been received successfully.
        * The statistics module keeps its own internal state and tracks values such as
        * frame count and first/last reception timestamps for each CAN identifier.
        */

        can_stats_record_frame(&frame);

        if (frame.is_extended)
        {
            printf("CAN:RX:EXT:%08" PRIX32 ":%u:", frame.id, (unsigned)frame.data_length);
        }
        else
        {
            printf("CAN:RX:STD:%03" PRIX32 ":%u:", frame.id, (unsigned)frame.data_length);
        }

        if (frame.is_remote)
        {
            printf("RTR");
        }
        else
        {
            for (uint8_t byte_index = 0; byte_index < frame.data_length; byte_index++)
            {
                printf("%02X", (unsigned)frame.data[byte_index]);

                if (byte_index + 1 < frame.data_length)
                {
                    printf(":");
                }
            }
        }

        printf("\n");
        fflush(stdout);
    }
}
    /*
        * Print a read-only snapshot of the currently collected CAN statistics.
    *
    * The statistics module keeps its internal table private. This function
    * requests a copy and is responsible only for formatting that data for
    * the serial console.
    */

    static void print_can_stats_snapshot(void)
    {
            static can_stats_entry_t snapshot[CAN_STATS_MAX_IDS];

            size_t entry_count = can_stats_snapshot(snapshot, CAN_STATS_MAX_IDS);

            printf("CAN:STATS:BEGIN:%u\n", (unsigned)entry_count);

            for (size_t i = 0; i < entry_count; i++)
        {
            const can_stats_entry_t *entry = &snapshot[i];

            int64_t average_period_us = 0;

            if (entry->count > 1)
            {
                average_period_us = (entry->last_seen_us - entry->first_seen_us) / (entry->count - 1);
            }

            if (entry->is_extended)
            {
                printf("CAN:STATS:EXT:%08" PRIX32 ":%" PRIu32 ":%" PRId64 "\n", entry->id, entry->count, average_period_us);
            }
            else
            {
                printf("CAN:STATS:STD:%03" PRIX32 ":%" PRIu32 ":%" PRId64 "\n", entry->id, entry->count, average_period_us);
            }
        }

        printf("CAN:STATS:END\n");
        fflush(stdout);
    }

    /*
    * Build a sample OBD-II PID request without transmitting it.
    *
    * This verifies that the OBD layer converts a logical PID request
    * into the expected CAN frame before active CAN transmission is enabled.
    */

    static void test_obd_pid_builder(void)
    {
        obd_pid_request_t request = {.mode = OBD_MODE_CURRENT_DATA, .pid = OBD_PID_ENGINE_RPM};

        can_bus_frame_t frame;

        if (!obd_build_pid_request(&request, &frame))
        {
            printf("OBD:TEST:BUILD_FAILED\n");;
            return;
        }

        printf("OBD:TEST:STD:%03" PRIX32 ":%u:", frame.id, frame.data_length);

        for (uint8_t i = 0; i < frame.data_length; i++)
        {
            printf("%02X", frame.data[i]);

            if (i + 1 < frame.data_length)
            {
                printf(":");
            }
        }

        printf("\n");
        fflush(stdout);

    }

    /*
    * Test the OBD-II response decoder with a synthetic RPM response.
    *
    * No CAN frame is transmitted. The frame below represents data that
    * could have been received from an ECU.
    */

    static void test_obd_pid_decoder(void)
{
    const can_bus_frame_t frames[] = {
        {
            .id = 0x7E8,
            .dlc = 8,
            .data_length = 8,
            .is_extended = false,
            .is_remote = false,
            .data = {0x04, 0x41, 0x0C, 0x1A, 0xF8, 0, 0, 0}
        },
        {
            .id = 0x7E8,
            .dlc = 8,
            .data_length = 8,
            .is_extended = false,
            .is_remote = false,
            .data = {0x03, 0x41, 0x05, 0x7E, 0, 0, 0, 0}
        },
        {
            .id = 0x7E8,
            .dlc = 8,
            .data_length = 8,
            .is_extended = false,
            .is_remote = false,
            .data = {0x03, 0x41, 0x0D, 0x64, 0, 0, 0, 0}
        },
        {
            .id = 0x7E8,
            .dlc = 8,
            .data_length = 8,
            .is_extended = false,
            .is_remote = false,
            .data = {0x03, 0x41, 0x04, 0x80, 0, 0, 0, 0}
        }
    };

    for (size_t i = 0; i < sizeof(frames) / sizeof(frames[0]); i++)
    {
        obd_pid_value_t value;

        if (!obd_decode_pid_response(&frames[i], &value))
        {
            printf("OBD:TEST:DECODE_FAILED\n");
            continue;
        }

        printf(
            "OBD:TEST:DECODE:PID:%02X:VALUE:%.2f\n",
            (unsigned int)value.pid,
            value.value
        );
    }

    fflush(stdout);
}

static void test_obd_passive_tx_guard(void)
{
    esp_err_t result = obd_request_pid(OBD_PID_ENGINE_RPM, 100);

    if (result == ESP_ERR_NOT_SUPPORTED)
    {
        printf("OBD:TEST:PASSIVE_TX_BLOCKED\n");
        return;
    }

    printf("OBD:TEST:PASSSIVE_TX_GUARD_FAILED:%s\n", esp_err_to_name(result));
}

#if OBD_VEHICLE_TEST_ENABLED

static void test_obd_vehicle_rpm_once(void)
{
    esp_err_t result =
        obd_request_pid(OBD_PID_ENGINE_RPM, 100);

    if (result != ESP_OK)
    {
        printf(
            "OBD:VEHICLE:REQUEST_ERROR:%s\n",
            esp_err_to_name(result)
        );
        return;
    }

    printf("OBD:VEHICLE:RPM_REQUEST_SENT\n");

    obd_pid_value_t value;

    result = obd_wait_pid_response(
        OBD_PID_ENGINE_RPM,
        &value,
        1000
    );

    if (result != ESP_OK)
    {
        printf(
            "OBD:VEHICLE:RESPONSE_ERROR:%s\n",
            esp_err_to_name(result)
        );
        return;
    }

    printf(
        "OBD:VEHICLE:RPM:%.2f\n",
        value.value
    );

    fflush(stdout);
}

#endif

static void test_obd_dtc_decoder(void)
{
    const can_bus_frame_t frame = {
        .id = 0x7E8,
        .dlc = 8,
        .data_length = 8,
        .is_extended = false,
        .is_remote = false,
        .data = {
            0x05,   // payload length = 5
            0x43,   // Mode 03 positive response
            0x01, 0x0A,   // P010A  
            0x04, 0x01,   // P0401 
            0x00, 0x00
        }
    };

    obd_dtc_list_t list;

    if (!obd_decode_dtc_response(&frame, &list))
    {
        printf("OBD:TEST:DTC_DECODE_FAILED\n");
        return;
    }

    printf("OBD:TEST:DTC_COUNT:%u\n", (unsigned int)list.count);

    for (uint8_t i = 0; i < list.count; i++)
    {
        printf("OBD:TEST:DTC:%s\n", list.dtcs[i].code);
    }

    fflush(stdout);
}

#if OBD_VEHICLE_TEST_ENABLED

static void test_obd_vehicle_dtcs_once(void)
{
    esp_err_t result = obd_request_dtcs(100);

    if (result != ESP_OK)
    {
        printf("OBD:VEHICLE:DTC_REQUEST_ERROR:%s\n", esp_err_to_name(result));
        return;
    }

    printf("OBD:VEHICLE:DTC_REQUEST_SENT\n");

    obd_dtc_list_t list;

    result = obd_wait_dtc_response(&list, 1500);

    if (result != ESP_OK)
    {
        printf("OBD:VEHICLE:DTC_RESPONSE_ERROR:%s\n", esp_err_to_name(result));
        return;
    }

    printf("OBD:VEHICLE:DTC_COUNT:%u\n", (unsigned int)list.count);

    for (uint8_t i = 0; i < list.count; i++)
    {
        printf("OBD:VEHICLE:DTC:%s\n", list.dtcs[i].code);
    }

    fflush(stdout);
}

#endif

void app_main(void)
{
    gateway_protocol_t gateway_protocol;

    gateway_protocol_init(&gateway_protocol);

    initialize_serial_input();

    can_bus_bitrate_t detected_bitrate = CAN_BUS_BITRATE_500K;
    bool bitrate_detected = false;

    #if CAN_AUTO_BITRATE_PROBE_ENABLED
        printf("\nCAN:PROBE:BEGIN\n");

        uint32_t frames_500k = probe_can_bitrate(CAN_BUS_BITRATE_500K, 1500);

        vTaskDelay(pdMS_TO_TICKS(200));

        uint32_t frames_250k = probe_can_bitrate(CAN_BUS_BITRATE_250K, 1500);

        printf("CAN:PROBE:SUMMARY:500K:%lu:250K:%lu\n",
            (unsigned long)frames_500k,
            (unsigned long)frames_250k
        );

        if (frames_500k > 0 || frames_250k > 0)
        {
            bitrate_detected = true;

            if (frames_250k > frames_500k)
            {
                detected_bitrate = CAN_BUS_BITRATE_250K;
            }
            else
            {
                detected_bitrate = CAN_BUS_BITRATE_500K;
            } 
            
            printf("CAN:PROBE:SELECTED:%u\n", (unsigned int)detected_bitrate);
        }
        else
        {
            printf("CAN:PROBE:NO_TRAFFIC_DETECTED\n");
        }
        
        printf("CAN:PROBE:END\n\n");

    #endif

    #if OBD_VEHICLE_TEST_ENABLED

        if (!bitrate_detected)
        {
            printf("CAN:DIAG:SKIPPED:NO_BITRATE\n");
        }
        else
        {
            start_can_bus(CAN_BUS_MODE_DIAGNOSTIC, detected_bitrate);

            vTaskDelay(pdMS_TO_TICKS(200));

            printf("CAN:DIAG:PRE_TX_RX_CHECK\n");
            
            for (int i = 0; i < 20; i++)
            {
                process_can_input();
                vTaskDelay(pdMS_TO_TICKS(25));
            }

            printf("CAN:DIAG:BEFORE_TX\n");
            can_bus_print_diagnostics();

            test_obd_vehicle_rpm_once();

            printf("CAN:DIAG:AFTER_RPM\n");
            can_bus_print_diagnostics();
            
            test_obd_vehicle_dtcs_once();

            printf("CAN:DIAG:AFTER_DTC\n");
            can_bus_print_diagnostics();
        }
        
    #else
        if (bitrate_detected)
        {
            start_can_bus(CAN_BUS_MODE_PASSIVE, detected_bitrate);
        }  
    #endif
    
    // test_obd_dtc_decoder();
    // test_obd_pid_builder();
    // test_obd_pid_decoder();
    // test_obd_passive_tx_guard();
    
    TickType_t previous_live_data_time = xTaskGetTickCount();
    TickType_t previous_can_data_stats_time = xTaskGetTickCount();

	int rpm = 750;
	int coolant_temperature = 86;
	float battery_voltage = 13.6f;

	/*
     * This message is sent once after the ESP32 application starts.
     * Later, the desktop application will use it to recognize that the
     * diagnostic gateway firmware is running.
  	 */
	printf("SYS:READY\n");
	fflush(stdout);

	while(1) 
	{
        process_serial_input(&gateway_protocol);

        TickType_t current_time = xTaskGetTickCount();

        if (gateway_protocol.state == GATEWAY_STATE_STREAMING && current_time - previous_live_data_time >= pdMS_TO_TICKS(1000))
        {


            send_live_data(rpm, coolant_temperature, battery_voltage);
            rpm += 25;
            coolant_temperature += 1;
            battery_voltage += 0.1f;

            previous_live_data_time = current_time;

            /*
            * Keep the simulated values within a realistic test range.
            * These are not real Isuzu reference values; they are only
            * temporary communication test data.
            */
            if (rpm > 900)
            {
                rpm = 750;
            }

            if (coolant_temperature > 90)
            {
                coolant_temperature = 86;
            }

            if (battery_voltage > 14.2f)
            { 
                battery_voltage = 13.8f;
            }

        }

        process_can_input();

        /*
        * Print the collected CAN traffic statistics every five seconds.
        * A FreeRTOS tick timestamp is sufficient here because this interval
        * does not require microsecond-level precision.
        */

        if (current_time - previous_can_data_stats_time >= pdMS_TO_TICKS(5000))
        {
            print_can_stats_snapshot();
            previous_can_data_stats_time = current_time; 
        }

        /*
         * Pause this FreeRTOS task briefly to avoid a busy-wait loop.
         */

        vTaskDelay(pdMS_TO_TICKS(10));
    }
}
