#include <stdio.h>
#include <string.h>
#include "can_bus.h"
#include "esp_twai.h"
#include "esp_twai_onchip.h"
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

/* All public functions belong to one owner task (app_main in this project).
 * The ISR shares only queues and counters protected by s_diag_lock.
 */
static twai_node_handle_t s_twai_node;
static can_bus_status_t s_can_status = CAN_BUS_STATUS_UNINITIALIZED;
static can_bus_mode_t s_can_mode = CAN_BUS_MODE_PASSIVE;
static bool s_node_enabled;
static QueueHandle_t s_rx_queue;
static QueueHandle_t s_tx_done_queue;

/* Driver borrows BOTH objects. They must survive timeout and stop/start.
 * Never overwrite them until the completion is consumed or node deleted.
 */
static uint8_t s_tx_data[CAN_CLASSIC_MAX_DATA_LENGTH];
static twai_frame_t s_tx_frame;
static bool s_tx_pending;

typedef struct {
    uint32_t tx_ok, tx_failed, ack, bit, form, stuff, arbitration;
    uint32_t rx_dropped, completion_dropped, last_error_flags;
} can_diag_t;

static can_diag_t s_diag;
static portMUX_TYPE s_diag_lock = portMUX_INITIALIZER_UNLOCKED;

static TickType_t timeout_ticks(uint32_t ms)
{
    uint64_t ticks = ((uint64_t)ms * configTICK_RATE_HZ + 999) / 1000;
    /* This API has finite millisecond timeouts, including at UINT32_MAX. */
    if (ticks >= portMAX_DELAY) {
        ticks = portMAX_DELAY - 1;
    }
    return (TickType_t)ticks;
}

static bool can_bus_rx_callback(twai_node_handle_t handle,
                               const twai_rx_done_event_data_t *event_data,
                               void *user_ctx)
{
    (void)event_data;
    (void)user_ctx;
    uint8_t rx_buffer[8] = {0};
    twai_frame_t rx_frame = {
        .buffer = rx_buffer,
        .buffer_len = sizeof(rx_buffer),
    };
    if (twai_node_receive_from_isr(handle, &rx_frame) != ESP_OK) {
        return false;
    }
    if (rx_frame.header.fdf || rx_frame.header.dlc > 8) {
        return false;
    }
    can_bus_frame_t frame = {
        .id = rx_frame.header.id,
        .dlc = (uint8_t)rx_frame.header.dlc,
        .data_length = rx_frame.header.rtr ? 0 : (uint8_t)rx_frame.header.dlc,
        .is_extended = rx_frame.header.ide != 0,
        .is_remote = rx_frame.header.rtr != 0,
        .timestamp_us = esp_timer_get_time(),
    };
    memcpy(frame.data, rx_buffer, frame.data_length);
    BaseType_t woken = pdFALSE;
    if (xQueueSendFromISR(s_rx_queue, &frame, &woken) != pdTRUE) {
        portENTER_CRITICAL_ISR(&s_diag_lock);
        s_diag.rx_dropped++;
        portEXIT_CRITICAL_ISR(&s_diag_lock);
    }
    return woken == pdTRUE;
}

static bool can_bus_tx_callback(twai_node_handle_t handle,
                               const twai_tx_done_event_data_t *event_data,
                               void *user_ctx)
{
    (void)handle;
    (void)user_ctx;
    if (event_data->done_tx_frame != &s_tx_frame) {
        return false;
    }
    bool success = event_data->is_tx_success;
    portENTER_CRITICAL_ISR(&s_diag_lock);
    if (success) {
        s_diag.tx_ok++;
    } else {
        s_diag.tx_failed++;
    }
    portEXIT_CRITICAL_ISR(&s_diag_lock);
    BaseType_t woken = pdFALSE;
    if (xQueueSendFromISR(s_tx_done_queue, &success, &woken) != pdTRUE) {
        portENTER_CRITICAL_ISR(&s_diag_lock);
        s_diag.completion_dropped++;
        portEXIT_CRITICAL_ISR(&s_diag_lock);
    }
    return woken == pdTRUE;
}

static bool can_bus_error_callback(twai_node_handle_t handle,
                                  const twai_error_event_data_t *event_data,
                                  void *user_ctx)
{
    (void)handle;
    (void)user_ctx;
    twai_error_flags_t flags = event_data->err_flags;
    portENTER_CRITICAL_ISR(&s_diag_lock);
    s_diag.last_error_flags = flags.val;
    s_diag.ack += flags.ack_err;
    s_diag.bit += flags.bit_err;
    s_diag.form += flags.form_err;
    s_diag.stuff += flags.stuff_err;
    s_diag.arbitration += flags.arb_lost;
    portEXIT_CRITICAL_ISR(&s_diag_lock);
    return false;
}

static void delete_queues(void)
{
    if (s_rx_queue != NULL) {
        vQueueDelete(s_rx_queue);
        s_rx_queue = NULL;
    }
    if (s_tx_done_queue != NULL) {
        vQueueDelete(s_tx_done_queue);
        s_tx_done_queue = NULL;
    }
}

static esp_err_t can_bus_init_node(can_bus_mode_t mode,
                                   can_bus_bitrate_t bitrate, bool self_test)
{
    if ((mode != CAN_BUS_MODE_PASSIVE && mode != CAN_BUS_MODE_DIAGNOSTIC) ||
        (bitrate != CAN_BUS_BITRATE_250K && bitrate != CAN_BUS_BITRATE_500K)) {
        return ESP_ERR_INVALID_ARG;
    }
    if (s_twai_node != NULL) {
        return ESP_ERR_INVALID_STATE;
    }
    s_rx_queue = xQueueCreate(CAN_RX_QUEUE_DEPTH, sizeof(can_bus_frame_t));
    s_tx_done_queue = xQueueCreate(1, sizeof(bool));
    if (s_rx_queue == NULL || s_tx_done_queue == NULL) {
        delete_queues();
        s_can_status = CAN_BUS_STATUS_ERROR;
        return ESP_ERR_NO_MEM;
    }
    s_tx_pending = false;
    s_node_enabled = false;
    s_diag = (can_diag_t){0};

    twai_onchip_node_config_t config = {
        .io_cfg = {
            .tx = mode == CAN_BUS_MODE_PASSIVE ? -1 : CAN_BUS_DIAGNOSTIC_TX_GPIO,
            .rx = CAN_BUS_RX_GPIO,
            .quanta_clk_out = -1,
            .bus_off_indicator = -1,
        },
        .bit_timing = {.bitrate = (uint32_t)bitrate},
        .tx_queue_depth = mode == CAN_BUS_MODE_PASSIVE ? 0 : 1,
        .fail_retry_cnt = 0,
        .flags = {
            .enable_self_test = self_test,
            .enable_loopback = self_test,
            .enable_listen_only = mode == CAN_BUS_MODE_PASSIVE,
        },
    };
    esp_err_t result = twai_new_node_onchip(&config, &s_twai_node);
    if (result != ESP_OK) {
        s_twai_node = NULL;
        delete_queues();
        s_can_status = CAN_BUS_STATUS_ERROR;
        return result;
    }
    twai_event_callbacks_t callbacks = {
        .on_rx_done = can_bus_rx_callback,
        .on_tx_done = can_bus_tx_callback,
        .on_error = can_bus_error_callback,
    };
    result = twai_node_register_event_callbacks(s_twai_node, &callbacks, NULL);
    if (result != ESP_OK) {
        /* Retain all callback resources if deletion fails. */
        esp_err_t cleanup = twai_node_delete(s_twai_node);
        if (cleanup == ESP_OK) {
            s_twai_node = NULL;
            delete_queues();
        }
        s_can_status = CAN_BUS_STATUS_ERROR;
        return result;
    }
    s_can_mode = mode;
    s_can_status = CAN_BUS_STATUS_STOPPED;
    return ESP_OK;
}

esp_err_t can_bus_init(can_bus_mode_t mode, can_bus_bitrate_t bitrate)
{
    return can_bus_init_node(mode, bitrate, false);
}

esp_err_t can_bus_start(void)
{
    if (s_twai_node == NULL) {
        return ESP_ERR_INVALID_STATE;
    }
    if (s_node_enabled) {
        return ESP_OK;
    }
    esp_err_t result = twai_node_enable(s_twai_node);
    if (result == ESP_OK) {
        s_node_enabled = true;
        s_can_status = CAN_BUS_STATUS_RUNNING;
    } else {
        s_can_status = CAN_BUS_STATUS_ERROR;
    }
    return result;
}

esp_err_t can_bus_stop(void)
{
    if (s_twai_node == NULL) {
        return ESP_ERR_INVALID_STATE;
    }
    if (!s_node_enabled) {
        return ESP_OK;
    }
    esp_err_t result = twai_node_disable(s_twai_node);
    /* IDF 6.0.2 on-chip driver also reports INVALID_STATE in BUS_OFF. */
    if (result == ESP_ERR_INVALID_STATE) {
        twai_node_status_t info = {0};
        if (twai_node_get_info(s_twai_node, &info, NULL) == ESP_OK &&
            info.state == TWAI_ERROR_BUS_OFF) {
            result = ESP_OK;
        }
    }
    if (result == ESP_OK) {
        s_node_enabled = false;
        s_can_status = CAN_BUS_STATUS_STOPPED;
    } else {
        s_can_status = CAN_BUS_STATUS_ERROR;
    }
    /* Do not clear pending TX: disable/enable can resume that frame. */
    return result;
}

esp_err_t can_bus_deinit(void)
{
    if (s_twai_node != NULL) {
        esp_err_t result = can_bus_stop();
        if (result != ESP_OK) {
            return result;
        }
        result = twai_node_delete(s_twai_node);
        if (result != ESP_OK) {
            s_can_status = CAN_BUS_STATUS_ERROR;
            return result;
        }
        s_twai_node = NULL;
    }
    /* Only deletion releases the driver's borrowed TX pointers. */
    delete_queues();
    s_tx_pending = false;
    s_node_enabled = false;
    s_can_mode = CAN_BUS_MODE_PASSIVE;
    s_can_status = CAN_BUS_STATUS_UNINITIALIZED;
    return ESP_OK;
}

esp_err_t can_bus_transmit(const can_bus_frame_t *frame, uint32_t timeout_ms)
{
    if (frame == NULL) {
        return ESP_ERR_INVALID_ARG;
    }
    if (s_twai_node == NULL || s_can_status != CAN_BUS_STATUS_RUNNING) {
        return ESP_ERR_INVALID_STATE;
    }
    if (s_can_mode != CAN_BUS_MODE_DIAGNOSTIC) {
        return ESP_ERR_NOT_SUPPORTED;
    }
    if (frame->dlc > 8 || frame->data_length > 8 ||
        (!frame->is_remote && frame->dlc != frame->data_length) ||
        (frame->is_remote && frame->data_length != 0) ||
        frame->id > (frame->is_extended ? CAN_EXTENDED_MAX_ID : CAN_STANDARD_MAX_ID)) {
        return ESP_ERR_INVALID_ARG;
    }

    bool success = false;
    if (s_tx_pending) {
        /* A timed-out operation may complete later. Never reuse its storage
         * merely because the caller's wait expired. Consume its result first.
         */
        if (xQueueReceive(s_tx_done_queue, &success, 0) != pdTRUE) {
            return ESP_ERR_INVALID_STATE;
        }
        s_tx_pending = false;
    }
    memset(s_tx_data, 0, sizeof(s_tx_data));
    memcpy(s_tx_data, frame->data, frame->data_length);
    s_tx_frame = (twai_frame_t){
        .header = {
            .id = frame->id,
            .dlc = frame->dlc,
            .ide = frame->is_extended,
            .rtr = frame->is_remote,
            .fdf = 0,
        },
        .buffer = s_tx_data,
        .buffer_len = frame->data_length,
    };
    s_tx_pending = true;
    /* One outstanding frame only. Do not spend a second timeout enqueueing. */
    esp_err_t result = twai_node_transmit(s_twai_node, &s_tx_frame, 0);
    if (result != ESP_OK) {
        s_tx_pending = false;
        return result;
    }
    if (xQueueReceive(s_tx_done_queue, &success, timeout_ticks(timeout_ms)) != pdTRUE) {
        return ESP_ERR_TIMEOUT;
    }
    s_tx_pending = false;
    return success ? ESP_OK : ESP_FAIL;
}

esp_err_t can_bus_receive(can_bus_frame_t *frame, uint32_t timeout_ms)
{
    if (frame == NULL) {
        return ESP_ERR_INVALID_ARG;
    }
    if (s_rx_queue == NULL || s_can_status != CAN_BUS_STATUS_RUNNING) {
        return ESP_ERR_INVALID_STATE;
    }
    return xQueueReceive(s_rx_queue, frame, timeout_ticks(timeout_ms)) == pdTRUE
        ? ESP_OK : ESP_ERR_TIMEOUT;
}

can_bus_status_t can_bus_get_status(void)
{
    return s_can_status;
}

static const char *can_bus_error_state_name(twai_error_state_t state)
{
    switch (state) {
        case TWAI_ERROR_ACTIVE: return "ACTIVE";
        case TWAI_ERROR_WARNING: return "WARNING";
        case TWAI_ERROR_PASSIVE: return "PASSIVE";
        case TWAI_ERROR_BUS_OFF: return "BUS_OFF";
        default: return "UNKNOWN";
    }
}

void can_bus_print_diagnostics(void)
{
    if (s_twai_node == NULL) {
        printf("CAN:DIAG:NO_NODE\n");
        fflush(stdout);
        return;
    }
    twai_node_status_t status = {0};
    twai_node_record_t statistics = {0};
    esp_err_t result = twai_node_get_info(s_twai_node, &status, &statistics);
    if (result != ESP_OK) {
        printf("CAN:DIAG:ERROR:%s\n", esp_err_to_name(result));
        fflush(stdout);
        return;
    }
    portENTER_CRITICAL(&s_diag_lock);
    can_diag_t diag = s_diag;
    portEXIT_CRITICAL(&s_diag_lock);
    printf("CAN:DIAG:STATE:%s:TEC:%u:REC:%u:BUS_ERRORS:%lu:TX_QUEUE_FREE:%lu\n",
           can_bus_error_state_name(status.state),
           (unsigned)status.tx_error_count, (unsigned)status.rx_error_count,
           (unsigned long)statistics.bus_err_num, (unsigned long)status.tx_queue_remaining);
    printf("CAN:DIAG:TX_OK:%lu:TX_FAILED:%lu:TX_RESULT_PENDING:%u:RX_DROPPED:%lu:COMPLETION_DROPPED:%lu\n",
           (unsigned long)diag.tx_ok, (unsigned long)diag.tx_failed,
           (unsigned)s_tx_pending, (unsigned long)diag.rx_dropped,
           (unsigned long)diag.completion_dropped);
    printf("CAN:DIAG:ACK:%lu:BIT:%lu:FORM:%lu:STUFF:%lu:ARB_LOST:%lu:LAST_FLAGS:0x%08lX\n",
           (unsigned long)diag.ack, (unsigned long)diag.bit, (unsigned long)diag.form,
           (unsigned long)diag.stuff, (unsigned long)diag.arbitration,
           (unsigned long)diag.last_error_flags);
    fflush(stdout);
}

/* BENCH ONLY: CANH/CANL must be disconnected from the vehicle/other nodes.
 * Also usable with module completely removed and GPIO21 wired to GPIO22.
 * In that setup PASS verifies only GPIO + jumper, not the transceiver.
 */
esp_err_t can_bus_run_transceiver_test(void)
{
    if (s_twai_node != NULL) {
        return ESP_ERR_INVALID_STATE;
    }
    printf("CAN:GPIO_TEST:V2:TX=21:RX=22\n");
    esp_err_t result = gpio_reset_pin(CAN_BUS_DIAGNOSTIC_TX_GPIO);
    if (result != ESP_OK) return result;
    result = gpio_reset_pin(CAN_BUS_RX_GPIO);
    if (result != ESP_OK) return result;
    /* Preload recessive before enabling output; input enables pad readback. */
    result = gpio_set_level(CAN_BUS_DIAGNOSTIC_TX_GPIO, 1);
    if (result != ESP_OK) return result;
    gpio_config_t tx_config = {
        .pin_bit_mask = 1ULL << CAN_BUS_DIAGNOSTIC_TX_GPIO,
        .mode = GPIO_MODE_INPUT_OUTPUT,
        .pull_up_en = GPIO_PULLUP_DISABLE,
        .pull_down_en = GPIO_PULLDOWN_DISABLE,
        .intr_type = GPIO_INTR_DISABLE,
    };
    result = gpio_config(&tx_config);
    if (result != ESP_OK) return result;
    gpio_config_t rx_config = {
        .pin_bit_mask = 1ULL << CAN_BUS_RX_GPIO,
        .mode = GPIO_MODE_INPUT,
        .pull_up_en = GPIO_PULLUP_DISABLE,
        .pull_down_en = GPIO_PULLDOWN_DISABLE,
        .intr_type = GPIO_INTR_DISABLE,
    };
    result = gpio_config(&rx_config);
    if (result != ESP_OK) return result;
    const int levels[] = {1, 0, 1};
    bool matches = true;
    for (unsigned i = 0; i < 3; ++i) {
        result = gpio_set_level(CAN_BUS_DIAGNOSTIC_TX_GPIO, levels[i]);
        if (result != ESP_OK) break;
        vTaskDelay(timeout_ticks(20));
        int tx = gpio_get_level(CAN_BUS_DIAGNOSTIC_TX_GPIO);
        int rx = gpio_get_level(CAN_BUS_RX_GPIO);
        printf("CAN:GPIO_TEST:SET=%d:TX_PAD=%d:RX=%d\n", levels[i], tx, rx);
        matches = matches && tx == levels[i] && rx == levels[i];
    }
    /* Keep CTX recessive after the test. TWAI may configure the pin later. */
    esp_err_t idle_result = gpio_set_level(CAN_BUS_DIAGNOSTIC_TX_GPIO, 1);
    fflush(stdout);
    if (result != ESP_OK) return result;
    if (idle_result != ESP_OK) return idle_result;
    return matches ? ESP_OK : ESP_FAIL;
}

/* External feedback is still necessary on ESP32: transceiver or TX-RX jumper.
 * Self-test suppresses ACK checking; this is not proof of an ECU response.
 * BENCH ONLY; the frame can reach CANH/CANL when a transceiver is attached.
 */
esp_err_t can_bus_run_loopback_test(void)
{
    if (s_twai_node != NULL) {
        return ESP_ERR_INVALID_STATE;
    }
    esp_err_t result = can_bus_init_node(CAN_BUS_MODE_DIAGNOSTIC, CAN_BUS_BITRATE_500K, true);
    if (result != ESP_OK) return result;
    result = can_bus_start();
    const can_bus_frame_t tx = {
        .id = 0x123, .dlc = 4, .data_length = 4,
        .data = {0xDE, 0xAD, 0xBE, 0xEF},
    };
    if (result == ESP_OK) result = can_bus_transmit(&tx, 1000);
    can_bus_frame_t rx = {0};
    if (result == ESP_OK) result = can_bus_receive(&rx, 1000);
    if (result == ESP_OK && (rx.id != tx.id || rx.is_extended || rx.is_remote ||
        rx.dlc != tx.dlc || rx.data_length != tx.data_length ||
        memcmp(rx.data, tx.data, tx.data_length) != 0)) {
        result = ESP_FAIL;
    }
    can_bus_print_diagnostics();
    esp_err_t cleanup = can_bus_deinit();
    if (cleanup != ESP_OK) {
        printf("CAN:LOOP:CLEANUP_ERROR:%s\n", esp_err_to_name(cleanup));
        fflush(stdout);
        if (result == ESP_OK) result = cleanup;
    }
    return result;
}
