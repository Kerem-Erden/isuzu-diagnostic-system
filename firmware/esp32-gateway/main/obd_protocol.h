#ifndef OBD_PROTOCOL_H
#define OBD_PROTOCOL_H
#include "can_bus.h"
#define OBD_MODE_CURRENT_DATA 0x01
#define OBD_MODE_STORED_DTC 0x03
#define OBD_MODE_CLEAR_DTC 0x04
#define OBD_PID_ENGINE_LOAD 0x04
#define OBD_PID_COOLANT_TEMP 0x05
#define OBD_PID_ENGINE_RPM 0x0C
#define OBD_PID_VEHICLE_SPEED 0x0D
#define OBD_PID_MODULE_VOLTAGE 0x42
#define OBD_DTC_STRING_LENGTH 6
#define OBD_MAX_DTCS 32
#define OBD_MAX_DTCS_SINGLE_FRAME 2
#ifndef OBD_PHYSICAL_REQUEST_ID
#define OBD_PHYSICAL_REQUEST_ID 0x7E0
#endif
#define OBD_PHYSICAL_RESPONSE_ID (OBD_PHYSICAL_REQUEST_ID + 8)
typedef struct { uint8_t mode, pid; } obd_pid_request_t;
typedef struct { uint8_t pid; float value; } obd_pid_value_t;
typedef struct { uint8_t raw_high, raw_low; char code[OBD_DTC_STRING_LENGTH]; } obd_dtc_t;
typedef struct { obd_dtc_t dtcs[OBD_MAX_DTCS]; uint8_t count; } obd_dtc_list_t;
bool obd_build_pid_request(const obd_pid_request_t *, can_bus_frame_t *);
bool obd_decode_pid_response(const can_bus_frame_t *, obd_pid_value_t *);
bool obd_decode_dtc(uint8_t, uint8_t, obd_dtc_t *);
bool obd_build_dtc_request(can_bus_frame_t *);
bool obd_decode_dtc_response(const can_bus_frame_t *, obd_dtc_list_t *);
esp_err_t obd_request_pid(uint8_t, uint32_t);
esp_err_t obd_wait_pid_response(uint8_t, obd_pid_value_t *, uint32_t);
esp_err_t obd_request_dtcs(uint32_t);
esp_err_t obd_wait_dtc_response(obd_dtc_list_t *, uint32_t);
esp_err_t obd_clear_dtcs(uint32_t);
esp_err_t obd_read_pid(uint8_t, obd_pid_value_t *, uint32_t);
#endif
