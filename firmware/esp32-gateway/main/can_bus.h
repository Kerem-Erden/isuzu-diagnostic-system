#ifndef CAN_BUS_H
#define CAN_BUS_H

#include <stdbool.h>
#include <stdint.h>
#include "esp_err.h"
#include "freertos/FreeRTOS.h"

/* All functions must be called from one owner task, as in current app_main.
 * Concurrent public API calls (including init/deinit) require external serialization.
 */

typedef enum
{
    CAN_BUS_STATUS_UNINITIALIZED = 0,
    CAN_BUS_STATUS_STOPPED,
    CAN_BUS_STATUS_RUNNING,
    CAN_BUS_STATUS_ERROR
} can_bus_status_t;

typedef enum 
{
    CAN_BUS_MODE_PASSIVE = 0,
    CAN_BUS_MODE_DIAGNOSTIC
} can_bus_mode_t;

typedef struct 
{
    uint32_t id;

    uint8_t dlc;
    uint8_t data_length;

    bool is_extended;
    bool is_remote;

    uint8_t data[8];

    int64_t timestamp_us;
} can_bus_frame_t;

typedef enum
{
    CAN_BUS_BITRATE_250K = 250000,
    CAN_BUS_BITRATE_500K = 500000
} can_bus_bitrate_t;

/*
 * Allocate and configure the CAN/TWAI controller.
 * The controller remains stopped after initialization.
 */
esp_err_t can_bus_init(can_bus_mode_t mode, can_bus_bitrate_t bitrate);

/*
 * Start CAN/TWAI communication.
 */
esp_err_t can_bus_start(void);

/*
 * Stop CAN/TWAI communication.
 */

esp_err_t can_bus_receive(can_bus_frame_t *frame, uint32_t timeout_ms);

esp_err_t can_bus_stop(void);

/*
 * Release CAN/TWAI resources.
 */
esp_err_t can_bus_deinit(void);

/*
 * Return the current state of the CAN bus abstraction.
 */
can_bus_status_t can_bus_get_status(void);

/*
 * Bench-only CAN/TWAI self-reception test. Requires external TX-to-RX feedback
 * through a transceiver or a GPIO21-GPIO22 jumper; not an internal-only test.
 * Disconnect CANH/CANL from the vehicle before invoking either bench test.
 */
esp_err_t can_bus_run_loopback_test(void);

/* ESP_OK: on_tx_done reported success (normal mode requires CAN ACK).
 * ESP_FAIL: on_tx_done reported transmission failure; see diagnostics.
 * ESP_ERR_TIMEOUT: wait expired, NOT cancellation. Storage stays protected.
 * Another TX is rejected while the old completion is outstanding. Stop/start
 * may resume pending TX; deinit deletes it. No automatic retry after timeout.
 * A CAN ACK is not an OBD/ECU application response.
 */
esp_err_t can_bus_transmit(const can_bus_frame_t *frame, uint32_t timeout_ms);

esp_err_t can_bus_run_transceiver_test(void);

void can_bus_print_diagnostics(void);

#endif
