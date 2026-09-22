#include <assert.h>
#include <stdio.h>
#include <string.h>
#include "obd_protocol.h"
#include "gateway_protocol.h"

static int64_t now_us;
static can_bus_frame_t rx[16];
static unsigned rx_head, rx_count, tx_count;
static can_bus_frame_t last_tx;
static esp_err_t tx_result = ESP_OK;
static bool respond_to_clear;
static void enqueue(uint32_t id, const uint8_t data[8]);

int64_t esp_timer_get_time(void) { return now_us; }
const char *esp_err_to_name(esp_err_t error) { return error == ESP_OK ? "ESP_OK" : "ERROR"; }
esp_err_t can_bus_transmit(const can_bus_frame_t *frame, uint32_t timeout_ms)
{
    (void)timeout_ms;
    assert(frame);
    last_tx = *frame;
    tx_count++;
    if (respond_to_clear && frame->data[1] == 4) {
        const uint8_t clear_ok[8]={1,0x44,0,0,0,0,0,0};
        enqueue(OBD_PHYSICAL_RESPONSE_ID,clear_ok);
    }
    return tx_result;
}
esp_err_t can_bus_receive(can_bus_frame_t *frame, uint32_t timeout_ms)
{
    if (rx_count == 0) { now_us += (int64_t)timeout_ms * 1000; return ESP_ERR_TIMEOUT; }
    *frame = rx[rx_head++ % 16];
    rx_count--;
    return ESP_OK;
}

static void enqueue(uint32_t id, const uint8_t data[8])
{
    unsigned index = (rx_head + rx_count) % 16;
    rx[index] = (can_bus_frame_t){ .id=id, .dlc=8, .data_length=8, .timestamp_us=now_us+1 };
    memcpy(rx[index].data, data, 8);
    rx_count++;
}
static void reset_fake(void)
{
    now_us=1000; rx_head=rx_count=tx_count=0; tx_result=ESP_OK; respond_to_clear=false; memset(&last_tx,0,sizeof(last_tx));
}

static bool handler(const char *command, char *payload, size_t capacity, void *context)
{
    int *clears = context;
    if (!strcmp(command,"CLEAR_DTC")) { (*clears)++; snprintf(payload,capacity,"CLEARED"); return true; }
    if (!strcmp(command,"INFO")) { snprintf(payload,capacity,"SOURCE=SIMULATION;ECU=DEMO;CLEAR=1"); return true; }
    snprintf(payload,capacity,"DTCS=P1093"); return true;
}

static void test_gateway(void)
{
    gateway_protocol_t p; gateway_protocol_init(&p);
    int clears=0; gateway_protocol_set_handler(&p,handler,&clears);
    char response[560];
    assert(gateway_protocol_handle_line(&p,"REQ|1|PING",response,sizeof(response)) && !strcmp(response,"RES|1|OK|PONG"));
    assert(gateway_protocol_handle_line(&p,"REQ|2|CLEAR_DTC",response,sizeof(response)) && !strcmp(response,"RES|2|OK|CLEARED"));
    assert(gateway_protocol_handle_line(&p,"REQ|2|CLEAR_DTC",response,sizeof(response)) && !strcmp(response,"RES|2|ERR|DUPLICATE_CLEAR"));
    assert(clears==1);
    assert(!gateway_protocol_handle_line(&p,"REQ|0|PING",response,sizeof(response)));
    assert(!gateway_protocol_handle_line(&p,"REQ|3|PING|EXTRA",response,sizeof(response)));
    assert(!gateway_protocol_handle_line(&p,"REQ|999999999999999999999|PING",response,sizeof(response)));
    assert(p.state==GATEWAY_STATE_IDLE);
    assert(!gateway_protocol_handle_line(&p,"REQ|4|START",response,5));
    assert(p.state==GATEWAY_STATE_IDLE);
    puts("PASS: strict gateway parsing and duplicate clear suppression");
}

static void test_pid_and_single_frame_dtc(void)
{
    can_bus_frame_t f;
    obd_pid_request_t request={.mode=1,.pid=OBD_PID_ENGINE_RPM};
    assert(obd_build_pid_request(&request,&f));
    assert(f.id==OBD_PHYSICAL_REQUEST_ID && f.data[0]==2 && f.data[1]==1 && f.data[2]==OBD_PID_ENGINE_RPM);
    f=(can_bus_frame_t){.id=OBD_PHYSICAL_RESPONSE_ID,.dlc=8,.data_length=8,.data={4,0x41,0x0C,0x1A,0xF8}};
    obd_pid_value_t value;
    assert(obd_decode_pid_response(&f,&value) && value.value==1726.0f);
    f=(can_bus_frame_t){.id=OBD_PHYSICAL_RESPONSE_ID,.dlc=8,.data_length=8,.data={6,0x43,2,0x01,0x0A,0x04,0x01}};
    obd_dtc_list_t list;
    assert(obd_decode_dtc_response(&f,&list) && list.count==2);
    assert(!strcmp(list.dtcs[0].code,"P010A") && !strcmp(list.dtcs[1].code,"P0401"));
    f.data[2]=3;
    assert(!obd_decode_dtc_response(&f,&list));
    puts("PASS: Mode 01 and counted Mode 03 single-frame decoding");
}

static void test_multiframe_and_filters(void)
{
    reset_fake();
    assert(obd_request_dtcs(100)==ESP_OK);
    assert(last_tx.id==OBD_PHYSICAL_REQUEST_ID && last_tx.data[1]==3);
    const uint8_t wrong[8]={6,0x43,2,0x0A,0x04,0x04,0x01,0};
    const uint8_t first[8]={0x10,0x0A,0x43,4,0x01,0x0A,0x04,0x01};
    const uint8_t next[8]={0x21,0x03,0xFF,0xC1,0x23,0,0,0};
    enqueue(0x7E9,wrong);
    enqueue(OBD_PHYSICAL_RESPONSE_ID,first);
    enqueue(OBD_PHYSICAL_RESPONSE_ID,next);
    obd_dtc_list_t list;
    assert(obd_wait_dtc_response(&list,100)==ESP_OK && list.count==4);
    assert(!strcmp(list.dtcs[2].code,"P03FF") && !strcmp(list.dtcs[3].code,"U0123"));
    assert(tx_count==2 && last_tx.data[0]==0x30 && last_tx.id==OBD_PHYSICAL_REQUEST_ID);

    reset_fake();
    assert(obd_request_dtcs(100)==ESP_OK);
    const uint8_t bad_next[8]={0x22,0x03,0xFF,0xC1,0x23,0,0,0};
    enqueue(OBD_PHYSICAL_RESPONSE_ID,first); enqueue(OBD_PHYSICAL_RESPONSE_ID,bad_next);
    assert(obd_wait_dtc_response(&list,100)==ESP_FAIL);
    puts("PASS: ISO-TP target filtering, flow control and CF sequence validation");
}

static void test_negative_timeout_and_clear(void)
{
    reset_fake();
    assert(obd_request_dtcs(100)==ESP_OK);
    const uint8_t negative[8]={3,0x7F,3,0x22,0,0,0,0};
    enqueue(OBD_PHYSICAL_RESPONSE_ID,negative);
    obd_dtc_list_t list;
    assert(obd_wait_dtc_response(&list,100)==ESP_FAIL);

    reset_fake();
    assert(obd_request_dtcs(100)==ESP_OK);
    assert(obd_wait_dtc_response(&list,5)==ESP_ERR_TIMEOUT && list.count==0);

    reset_fake();
    respond_to_clear=true;
    assert(obd_clear_dtcs(100)==ESP_OK && tx_count==1 && last_tx.data[1]==4);

    reset_fake(); tx_result=ESP_FAIL;
    assert(obd_clear_dtcs(100)==ESP_FAIL && tx_count==1);
    puts("PASS: negative response, absolute timeout and one-shot Mode 04");
}

int main(void)
{
    test_gateway();
    test_pid_and_single_frame_dtc();
    test_multiframe_and_filters();
    test_negative_timeout_and_clear();
    puts("ALL FIRMWARE PROTOCOL TESTS PASSED (host mocks, no vehicle)");
    return 0;
}
