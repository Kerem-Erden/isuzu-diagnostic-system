#include <stdio.h>
#include "can_bus.h"

#include "esp_twai.h"
#include "esp_twai_onchip.h"

#include "freertos/FreeRTOS.h"
#include "freertos/task.h"
#include "freertos/queue.h"
#include "esp_timer.h"

#define CAN_BUS_TX_GPIO -1
#define CAN_BUS_RX_GPIO 22
#define CAN_RX_QUEUE_DEPTH 32


/*
 * Temporary bitrate for internal loopback testing.
 * This is not yet assumed to be the vehicle CAN Bitrate. 
 */
#define CAN_BUS_BITRATE 500000

static twai_node_handle_t s_twai_node = NULL;
static can_bus_status_t s_can_status = CAN_BUS_STATUS_UNINITIALIZED;
static bool can_bus_rx_callback(twai_node_handle_t handle, const twai_rx_done_event_data_t *event_data, void *user_ctx);
static volatile bool s_rx_frame_received = false;
static twai_frame_header_t s_rx_header;
static uint8_t s_rx_data[8];
static size_t s_rx_data_length = 0;
static QueueHandle_t s_rx_queue = NULL;

/*
 * Run a controlled internal CAN/TWAI loopback test.
 */
esp_err_t can_bus_run_loopback_test(void);

esp_err_t can_bus_init(void)
{
    if (s_twai_node != NULL)
    {
        return ESP_ERR_INVALID_STATE;
    }

    s_rx_queue = xQueueCreate(CAN_RX_QUEUE_DEPTH, sizeof(can_bus_frame_t));

    if (s_rx_queue == NULL)
    {
        s_can_status = CAN_BUS_STATUS_ERROR;
        return ESP_ERR_NO_MEM;
    }

    twai_onchip_node_config_t node_config = 
    {
        .io_cfg = {
            .tx = CAN_BUS_TX_GPIO,
            .rx = CAN_BUS_RX_GPIO,
            .quanta_clk_out = -1,
            .bus_off_indicator = -1,
        },

        .bit_timing = {
            .bitrate = CAN_BUS_BITRATE,
        },

        .tx_queue_depth = 0,
        .fail_retry_cnt = 0,

         /*
         * Temporary test configuration.
         * Self-test removes the ACK requirement.
         * Loopback lets the controller receive its own frames.
         */
        .flags = {
            .enable_self_test = 0,
            .enable_loopback = 0,
            .enable_listen_only = 1,
        },
    };

    esp_err_t result = twai_new_node_onchip(&node_config, &s_twai_node);

    if (result != ESP_OK)
        {
            vQueueDelete(s_rx_queue);
            s_rx_queue = NULL;

            s_twai_node = NULL;
            s_can_status = CAN_BUS_STATUS_ERROR;

            return result;
        }

    twai_event_callbacks_t callbacks = {
        .on_rx_done = can_bus_rx_callback,
    };

    result = twai_node_register_event_callbacks(s_twai_node, &callbacks, NULL);

    if (result != ESP_OK)
    {
        twai_node_delete(s_twai_node);
        s_twai_node = NULL;

        vQueueDelete(s_rx_queue);
        s_rx_queue = NULL;

        s_can_status = CAN_BUS_STATUS_ERROR;
        return result;
    }

    s_can_status = CAN_BUS_STATUS_STOPPED;

    return ESP_OK;
}

esp_err_t can_bus_deinit(void)
{
    if (s_twai_node == NULL)
    {
        if (s_rx_queue != NULL)
        {
            vQueueDelete(s_rx_queue);
            s_rx_queue = NULL;
        }
        s_can_status = CAN_BUS_STATUS_UNINITIALIZED;
        return ESP_OK;
    }

    if (s_can_status == CAN_BUS_STATUS_RUNNING)
    {
        esp_err_t stop_result = can_bus_stop();

        if (stop_result != ESP_OK)
        {
            return stop_result;
        }
    }

    esp_err_t result = twai_node_delete(s_twai_node);

    if (result != ESP_OK)
    {
        s_can_status = CAN_BUS_STATUS_ERROR;
        return result;
    }

    s_twai_node = NULL;
    if (s_rx_queue != NULL)
    {
        vQueueDelete(s_rx_queue);
        s_rx_queue = NULL;
    }
    s_can_status = CAN_BUS_STATUS_UNINITIALIZED;

    return ESP_OK;
}

esp_err_t can_bus_start(void)
{
    if (s_twai_node == NULL)
    {
        return ESP_ERR_INVALID_STATE;
    }

    if (s_can_status == CAN_BUS_STATUS_RUNNING)
    {
        return ESP_OK;
    }

    esp_err_t result = twai_node_enable(s_twai_node);

    if (result != ESP_OK)
    {
        s_can_status = CAN_BUS_STATUS_ERROR;
        return result;
    }

    s_can_status = CAN_BUS_STATUS_RUNNING;

    return ESP_OK;
}

esp_err_t can_bus_stop(void)
{
    if (s_twai_node == NULL)
    {
        return ESP_ERR_INVALID_STATE;
    }

    if (s_can_status == CAN_BUS_STATUS_STOPPED)
    {
        return ESP_OK;
    }

    esp_err_t result = twai_node_disable(s_twai_node);

    if (result != ESP_OK)
    {
        s_can_status = CAN_BUS_STATUS_ERROR;
        return result;
    }

    s_can_status = CAN_BUS_STATUS_STOPPED;

    return ESP_OK;
}

static bool can_bus_rx_callback(twai_node_handle_t handle, const twai_rx_done_event_data_t *event_data, void *user_ctx)
{
    uint8_t rx_buffer[8];

    twai_frame_t rx_frame = {
        .buffer = rx_buffer,
        .buffer_len = sizeof(rx_buffer),
    };

    esp_err_t result = twai_node_receive_from_isr(handle, &rx_frame);

    if (result != ESP_OK)
    {
        return false;
    }

    size_t data_length = twaifd_dlc2len(rx_frame.header.dlc);

    if (data_length > sizeof(s_rx_data))
    {
        data_length = sizeof(s_rx_data);
    }

    can_bus_frame_t received_frame = {
        .id = rx_frame.header.id,
        .dlc = rx_frame.header.dlc,
        .data_length = (uint8_t)data_length,
        .is_extended = rx_frame.header.ide != 0,
        .is_remote = rx_frame.header.rtr != 0,
        .timestamp_us = esp_timer_get_time(),
    };

    s_rx_header = rx_frame.header;

    for (size_t i = 0; i < data_length; i++)
    {
        received_frame.data[i] = rx_buffer[i];

        /*
        * Keep a copy for the bench loopback test.
        */

        s_rx_data[i] = rx_buffer[i];
    }

    s_rx_data_length = data_length;
    s_rx_frame_received = true;

    BaseType_t higher_priority_task_woken = pdFALSE;

    if (s_rx_queue != NULL)
    {
        xQueueSendFromISR(s_rx_queue, &received_frame, &higher_priority_task_woken);
    }

    return higher_priority_task_woken == pdTRUE;
}

esp_err_t can_bus_run_loopback_test(void)
{
    if (s_twai_node == NULL || s_can_status != CAN_BUS_STATUS_RUNNING)
    {
        return ESP_ERR_INVALID_STATE;
    }

    static uint8_t tx_data[3] = {
        0xAA,
        0xBB,
        0xCC
    };

    twai_frame_t tx_frame = {
        .header = {
            .id =0x123,
            .dlc = 3,
            .ide = 0,
            .rtr = 0,
            .fdf = 0,
        },
        .buffer = tx_data,
        .buffer_len = sizeof(tx_data),
    };

    s_rx_frame_received = false;
    s_rx_data_length = 0;

    esp_err_t result = twai_node_transmit(s_twai_node, &tx_frame, 100);

    if (result != ESP_OK)
    {
        return result;
    }

    result = twai_node_transmit_wait_all_done(s_twai_node, 1000);

    if (result != ESP_OK)
    {
        return result;
    }

    for (int attempt = 0; attempt < 10; attempt++)
    {
        if (s_rx_frame_received)
        {
            break;
        }

        vTaskDelay(pdMS_TO_TICKS(10));
    }

    if (!s_rx_frame_received)
    {
        return ESP_ERR_TIMEOUT;
    }

    if (s_rx_header.id != 0x123)
    {
        return ESP_FAIL;
    }

    if (s_rx_data[0] != 0xAA ||  s_rx_data[1] != 0xBB || s_rx_data[2] != 0xCC)
    {
        return ESP_FAIL;
    }

    return ESP_OK;
}

esp_err_t can_bus_receive(can_bus_frame_t *frame, uint32_t timeout_ms)
{
    if (frame == NULL)
    {
        return ESP_ERR_INVALID_ARG;
    }

    if (s_rx_queue == NULL || s_can_status != CAN_BUS_STATUS_RUNNING)
    {
        return ESP_ERR_INVALID_STATE;
    }

    BaseType_t result = xQueueReceive(s_rx_queue, frame, pdMS_TO_TICKS(timeout_ms));

    if (result != pdTRUE)
    {
        return ESP_ERR_TIMEOUT;
    }

    return ESP_OK;
}

can_bus_status_t can_bus_get_status(void)
{
    return s_can_status;
}


 