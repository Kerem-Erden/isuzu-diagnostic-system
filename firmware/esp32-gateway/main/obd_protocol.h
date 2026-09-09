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

#define OBD_DTC_STRING_LENGTH 6

#define OBD_MODE_STORED_DTC 0x03
#define OBD_MAX_DTCS_SINGLE_FRAME 3



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

typedef struct
{
    uint8_t raw_high;
    uint8_t raw_low;
    char code[OBD_DTC_STRING_LENGTH];
} obd_dtc_t;

typedef struct
{
    obd_dtc_t dtcs[OBD_MAX_DTCS_SINGLE_FRAME];
    uint8_t count;
} obd_dtc_list_t;

bool obd_build_pid_request(const obd_pid_request_t *request, can_bus_frame_t *out_frame);

bool obd_decode_pid_response(const can_bus_frame_t *frame, obd_pid_value_t *out_value);

bool obd_decode_dtc(uint8_t high_byte, uint8_t low_byte, obd_dtc_t *out_dtc);

bool obd_build_dtc_request(can_bus_frame_t *out_frame);

bool obd_decode_dtc_response(const can_bus_frame_t *frame, obd_dtc_list_t *out_list);

esp_err_t obd_request_pid(uint8_t pid, uint32_t timeout_ms);

esp_err_t obd_wait_pid_response(uint8_t pid, obd_pid_value_t *out_value, uint32_t timeout_ms);

esp_err_t obd_request_dtcs(uint32_t timeout_ms);

esp_err_t obd_wait_dtc_response(obd_dtc_list_t *out_list, uint32_t timeout_ms);

#endif

