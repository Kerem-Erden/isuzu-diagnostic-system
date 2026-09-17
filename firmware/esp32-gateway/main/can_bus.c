#include <stdio.h>
#include "can_bus.h"

#include "esp_twai.h"
#include "esp_twai_onchip.h"

#include "freertos/FreeRTOS.h"
#include "freertos/task.h"
#include "freertos/queue.h"
#include "esp_timer.h"
#include "driver/gpio.h"

#define CAN_BUS_DIAGNOSTIC_TX_GPIO 21
#define CAN_BUS_RX_GPIO 22
#define CAN_RX_QUEUE_DEPTH 32
#define CAN_STANDARD_MAX_ID 0x7FF
#define CAN_EXTENDED_MAX_ID 0x1FFFFFFF
#define CAN_CLASSIC_MAX_DATA_LENGTH 8


/*
 * Normal vehicle CAN configuration.
 * Passive mode enables listen-only operation.
 * Diagnostic mode enables normal CAN participation.
 */

static twai_node_handle_t s_twai_node = NULL;
static can_bus_status_t s_can_status = CAN_BUS_STATUS_UNINITIALIZED;
static bool can_bus_rx_callback(twai_node_handle_t handle, const twai_rx_done_event_data_t *event_data, void *user_ctx);
static QueueHandle_t s_rx_queue = NULL;
static can_bus_mode_t s_can_mode = CAN_BUS_MODE_PASSIVE;


esp_err_t can_bus_init(can_bus_mode_t mode, can_bus_bitrate_t bitrate)
{
    int tx_gpio;
    uint32_t tx_queue_depth;
    bool listen_only;

    if (bitrate != CAN_BUS_BITRATE_250K && bitrate != CAN_BUS_BITRATE_500K)
    {
        return ESP_ERR_INVALID_ARG;
    }

    if (mode == CAN_BUS_MODE_PASSIVE)
    {
        tx_gpio = -1;
        tx_queue_depth = 0;
        listen_only = true;
    }
    else if (mode == CAN_BUS_MODE_DIAGNOSTIC)
    {
        tx_gpio = CAN_BUS_DIAGNOSTIC_TX_GPIO;
        tx_queue_depth = 5; 
        listen_only = false;
    }
    else
    {
        return ESP_ERR_INVALID_ARG;
    }

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
            .tx = tx_gpio,
            .rx = CAN_BUS_RX_GPIO,
            .quanta_clk_out = -1,
            .bus_off_indicator = -1,
        },

        .bit_timing = {
            .bitrate = (uint32_t)bitrate,
        },

        .tx_queue_depth = tx_queue_depth,
        .fail_retry_cnt = 0,

        .flags = {
            .enable_self_test = 0,
            .enable_loopback = 0,
            .enable_listen_only = listen_only,
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
    s_can_mode = mode;

    return ESP_OK;
}

esp_err_t can_bus_deinit(void)
{
    if (s_twai_node == NULL)
    {
        s_can_mode = CAN_BUS_MODE_PASSIVE;

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

    s_can_mode = CAN_BUS_MODE_PASSIVE;

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

    if (data_length > sizeof(rx_buffer))
    {
        data_length = sizeof(rx_buffer);
    }

    can_bus_frame_t received_frame = {
        .id = rx_frame.header.id,
        .dlc = rx_frame.header.dlc,
        .data_length = (uint8_t)data_length,
        .is_extended = rx_frame.header.ide != 0,
        .is_remote = rx_frame.header.rtr != 0,
        .timestamp_us = esp_timer_get_time(),
    };

    for (size_t i = 0; i < data_length; i++)
    {
        received_frame.data[i] = rx_buffer[i];
    }

    BaseType_t higher_priority_task_woken = pdFALSE;

    QueueHandle_t target_queue = user_ctx != NULL ? (QueueHandle_t)user_ctx : s_rx_queue;

    if (target_queue != NULL)
    {
        xQueueSendFromISR(target_queue, &received_frame, &higher_priority_task_woken);
    }

    return higher_priority_task_woken == pdTRUE;
}

esp_err_t can_bus_transmit(const can_bus_frame_t *frame, uint32_t timeout_ms)
{
    if (frame == NULL)
    {
        return ESP_ERR_INVALID_ARG;
    }

    if (s_twai_node == NULL || s_can_status != CAN_BUS_STATUS_RUNNING)
    {
        return ESP_ERR_INVALID_STATE;
    }

    if (s_can_mode != CAN_BUS_MODE_DIAGNOSTIC)
    {
        return ESP_ERR_NOT_SUPPORTED;
    }
    
    if (frame->data_length > CAN_CLASSIC_MAX_DATA_LENGTH || frame->dlc > CAN_CLASSIC_MAX_DATA_LENGTH)
    {
        return ESP_ERR_INVALID_ARG;
    }

    if (!frame->is_extended)
    {
        if (frame->id > CAN_STANDARD_MAX_ID)
        {
            return ESP_ERR_INVALID_ARG;
        }
    }
    else
    {
        if (frame->id > CAN_EXTENDED_MAX_ID)
        {
            return ESP_ERR_INVALID_ARG;
        }
    }

    uint8_t tx_data[CAN_CLASSIC_MAX_DATA_LENGTH] = {0};

    for (uint8_t i = 0; i < frame->data_length; i++)
    {
        tx_data[i] = frame->data[i];
    }

    twai_frame_t tx_frame = {
        .header = {
            .id = frame->id,
            .dlc = frame->dlc,
            .ide = frame->is_extended ? 1 : 0,
            .rtr = frame->is_remote ? 1 : 0,
            .fdf = 0,
        },
        .buffer = tx_data,
        .buffer_len = frame->data_length,
    };

    esp_err_t result = twai_node_transmit(s_twai_node, &tx_frame, timeout_ms);

    if (result != ESP_OK)
    {
        return result;
    }

    return twai_node_transmit_wait_all_done(s_twai_node, timeout_ms);

}

esp_err_t can_bus_run_transceiver_test(void)
{
    if (s_twai_node != NULL)
    {
        return ESP_ERR_INVALID_STATE;
    }

    gpio_reset_pin(CAN_BUS_DIAGNOSTIC_TX_GPIO);
    gpio_reset_pin(CAN_BUS_RX_GPIO);

    gpio_set_direction(
        CAN_BUS_DIAGNOSTIC_TX_GPIO,
        GPIO_MODE_OUTPUT);

    gpio_set_direction(
        CAN_BUS_RX_GPIO,
        GPIO_MODE_INPUT);
gpio_set_level(CAN_BUS_DIAGNOSTIC_TX_GPIO, 1);
vTaskDelay(pdMS_TO_TICKS(10));

int recessive_1 = gpio_get_level(CAN_BUS_RX_GPIO);
printf("CAN:TRANSCEIVER:RECESSIVE1:RX=%d\n", recessive_1);

gpio_set_level(CAN_BUS_DIAGNOSTIC_TX_GPIO, 0);
vTaskDelay(pdMS_TO_TICKS(10));

int dominant = gpio_get_level(CAN_BUS_RX_GPIO);
printf("CAN:TRANSCEIVER:DOMINANT:RX=%d\n", dominant);

gpio_set_level(CAN_BUS_DIAGNOSTIC_TX_GPIO, 1);
vTaskDelay(pdMS_TO_TICKS(10));

int recessive_2 = gpio_get_level(CAN_BUS_RX_GPIO);
printf("CAN:TRANSCEIVER:RECESSIVE2:RX=%d\n", recessive_2);

gpio_reset_pin(CAN_BUS_DIAGNOSTIC_TX_GPIO);
gpio_reset_pin(CAN_BUS_RX_GPIO);

if (recessive_1 != 1 || dominant != 0 || recessive_2 != 1)
{
    return ESP_FAIL;
}

return ESP_OK;
}
////
esp_err_t can_bus_run_loopback_test(void)
{
    if (s_twai_node != NULL)
    {
        return ESP_ERR_INVALID_STATE;
    }

    QueueHandle_t test_queue = xQueueCreate(4, sizeof(can_bus_frame_t));

    if (test_queue == NULL)
    {
        return ESP_ERR_NO_MEM;
    }

    twai_node_handle_t test_node = NULL;

    twai_onchip_node_config_t node_config =
    {
        .io_cfg = {
            .tx = CAN_BUS_DIAGNOSTIC_TX_GPIO,
            .rx = CAN_BUS_RX_GPIO,
            .quanta_clk_out = -1,
            .bus_off_indicator = -1,
        },

        .bit_timing = {
            .bitrate = CAN_BUS_BITRATE_500K,
        },

        .tx_queue_depth = 1,
        .fail_retry_cnt = 0,

        .flags = {
            .enable_self_test = 1,
            .enable_loopback = 1,
            .enable_listen_only =  0,
        },
    };

    esp_err_t result = twai_new_node_onchip(&node_config, &test_node);

    if (result != ESP_OK)
    {
        vQueueDelete(test_queue);
        return result;
    }

    twai_event_callbacks_t callbacks = {.on_rx_done = can_bus_rx_callback};

    result = twai_node_register_event_callbacks(test_node, &callbacks, test_queue);

    if (result != ESP_OK)
    {
        twai_node_delete(test_node);
        vQueueDelete(test_queue);
        return result;
    }

    result = twai_node_enable(test_node);

    if  (result != ESP_OK)
    {
        twai_node_delete(test_node);
        vQueueDelete(test_queue);
        return result;
    }

    uint8_t tx_data[4] = {0xDE, 0xAD, 0xBE, 0xEF};

    twai_frame_t tx_frame = {
        .header = {
            .id = 0x123,
            .dlc = 4,
            .ide = 0,
            .rtr = 0,
            .fdf = 0,
        },
        .buffer = tx_data,
        .buffer_len = sizeof(tx_data),
    };

    result   = twai_node_transmit(test_node, &tx_frame, 1000);

    if (result == ESP_OK)
    {
        result = twai_node_transmit_wait_all_done(test_node, 1000);
    }

    can_bus_frame_t received_frame = {0};

    if (result == ESP_OK)
    {
        BaseType_t received = xQueueReceive(test_queue, &received_frame, pdMS_TO_TICKS(1000));

        if (received != pdTRUE)
        {
            result = ESP_ERR_TIMEOUT;
        }
    }

    if (result == ESP_OK)
    {
        bool frame_matches =
            received_frame.id == 0x123  &&
            received_frame.data_length == 4 &&
            received_frame.data[0] == 0xDE &&
            received_frame.data[1] == 0xAD &&
            received_frame.data[2] == 0xBE &&
            received_frame.data[3] == 0xEF;

        if (!frame_matches)
        {
            result = ESP_FAIL;
        }
    }

    twai_node_disable(test_node);
    twai_node_delete(test_node);
    vQueueDelete(test_queue);

    return result;
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

static const char *can_bus_error_states_name(twai_error_state_t state)
{
    switch (state)
    {
        case TWAI_ERROR_ACTIVE:
            return "ACTIVE";
            
        case TWAI_ERROR_WARNING:
            return "WARNING";
        
        case TWAI_ERROR_PASSIVE:
            return "PASSIVE";

        case TWAI_ERROR_BUS_OFF:
            return "BUS_OFF";

        default:
            return "UNKNOWN";
    }
}

void can_bus_print_diagnostics(void)
{
    if (s_twai_node == NULL)
    {
        printf("CAN:DIAG:NO_NODE\n");
        fflush(stdout);
        return;
    }

    if (s_can_mode != CAN_BUS_MODE_DIAGNOSTIC)
    {
        printf("CAN:DIAG:SKIPPED:PASSIVE_MODE\n");
        fflush(stdout);
        return;
    }


    twai_node_status_t status;
    twai_node_record_t statistics;

    esp_err_t result = twai_node_get_info(s_twai_node, &status, &statistics);

    if (result != ESP_OK)
    {
        printf("CAN:DIAG:ERROR:%s\n", esp_err_to_name(result));
        fflush(stdout);
        return;
    }

    printf(
        "CAN:DIAG:STATE:%s:TEC:%u:REC:%u:BUS_ERRORS:%lu:TX_QUEUE_FREE:%lu\n",
        can_bus_error_states_name(status.state),
        (unsigned int)status.tx_error_count,
        (unsigned int)status.rx_error_count,
        (unsigned long)statistics.bus_err_num,
        (unsigned long)status.tx_queue_remaining
    );

    fflush(stdout);
}

 