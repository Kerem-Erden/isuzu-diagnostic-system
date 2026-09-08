#ifndef OBD_PROTOCOL_H
#define OBD_PROTOCOL_H

#include <stdint.h>
#include <stdbool.h>

#include "can_bus.h"

#define OBD_MODE_CURRENT_DATA 0x01

#define OBD_PID_ENGINE_LOAD 0x04
#define OBD_PID_COOLANT_TEMP 0x05
#define OBD_PID_ENGINE_RPM 0x0C
#define OBD_PID_VEHICLE_SPEED 0x0D

typedef struct 
{
    uint8_t mode;
    uint8_t pid;
} obd_pid_request_t;

typedef struct
{
    uint8_t pid;
    float value;
} obd_pid_value_t;

bool obd_build_pid_request(const obd_pid_request_t *request, can_bus_frame_t *out_frame);

bool obd_decode_pid_response(const can_bus_frame_t *frame, obd_pid_value_t *out_value);

esp_err_t obd_request_pid(uint8_t pid, uint32_t timeout_ms);

esp_err_t obd_wait_pid_response(uint8_t pid, obd_pid_value_t *out_value, uint32_t timeout_ms);



#endif

