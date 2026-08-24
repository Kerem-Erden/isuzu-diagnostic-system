#ifndef CAN_BUS_H
#define CAN_BUS_H

#include <stdbool.h>
#include <stdint.h>
#include "esp_err.h"

typedef enum
{
    CAN_BUS_STATUS_UNINITIALIZED = 0,
    CAN_BUS_STATUS_STOPPED,
    CAN_BUS_STATUS_RUNNING,
    CAN_BUS_STATUS_ERROR
} can_bus_status_t;

typedef struct 
{
    uint32_t id;

    uint8_t dlc;
    uint8_t data_length;

    bool is_extended;
    bool is_remote;

    uint8_t data[8];
} can_bus_frame_t;


/*
 * Allocate and configure the CAN/TWAI controller.
 * The controller remains stopped after initialization.
 */
esp_err_t can_bus_init(void);

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
 * Runs a controlled internal CAN/TWAI loopback test.
 */
esp_err_t can_bus_run_loopback_test(void);

#endif