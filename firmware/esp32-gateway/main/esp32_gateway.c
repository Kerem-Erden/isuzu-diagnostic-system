#include <stdio.h>
#include <string.h>
#include <inttypes.h>
#include "sdkconfig.h"
#include "driver/uart.h"
#include "freertos/task.h"
#include "esp_timer.h"
#include "gateway_protocol.h"
#include "obd_protocol.h"
#include "can_bus.h"

static unsigned s_sample;
#if CONFIG_GATEWAY_SIMULATION
static bool s_cleared;
#else
static bool s_ready;
static int64_t s_scan_at=-1;
#endif
static bool command(const char *name,char *out,size_t cap,void *context)
{
    (void)context;
    if(strcmp(name,"INFO")==0) {
#if CONFIG_GATEWAY_SIMULATION
        snprintf(out,cap,"SOURCE=SIMULATION;ECU=DEMO;CLEAR=1");
#else
        snprintf(out,cap,"SOURCE=VEHICLE;ECU=%03X;CLEAR=%d",OBD_PHYSICAL_REQUEST_ID,
#ifdef CONFIG_GATEWAY_ALLOW_CLEAR
        CONFIG_GATEWAY_ALLOW_CLEAR
#else
        0
#endif
        );
#endif
        return true;
    }
#if CONFIG_GATEWAY_SIMULATION
    if(strcmp(name,"SCAN_DTC")==0){snprintf(out,cap,"DTCS=%s",s_cleared?"P0087":"P1093,P0087,P3FFF");return true;}
    if(strcmp(name,"CLEAR_DTC")==0){s_cleared=true;snprintf(out,cap,"CLEARED");return true;}
#else
    if(!s_ready){snprintf(out,cap,"CAN_NOT_READY");return false;}
    if(strcmp(name,"SCAN_DTC")==0) {
        s_scan_at=-1;
        obd_dtc_list_t list;esp_err_t result=obd_request_dtcs(100);
        if(result==ESP_OK) result=obd_wait_dtc_response(&list,1500);
        if(result!=ESP_OK){snprintf(out,cap,"SCAN_%s",esp_err_to_name(result));can_bus_print_diagnostics();return false;}
        size_t used=5;snprintf(out,cap,"DTCS=");
        for(uint8_t i=0;i<list.count;++i){int n=snprintf(out+used,cap-used,"%s%s",i?",":"",list.dtcs[i].code);if(n<0||(size_t)n>=cap-used)return false;used+=(size_t)n;}
        s_scan_at=esp_timer_get_time();return true;
    }
    if(strcmp(name,"CLEAR_DTC")==0) {
#if CONFIG_GATEWAY_ALLOW_CLEAR
        if(s_scan_at<0 || esp_timer_get_time()-s_scan_at>30000000){snprintf(out,cap,"FRESH_SNAPSHOT_REQUIRED");return false;}
        s_scan_at=-1;
        obd_pid_value_t rpm,speed;
        if(obd_read_pid(OBD_PID_ENGINE_RPM,&rpm,300)!=ESP_OK || obd_read_pid(OBD_PID_VEHICLE_SPEED,&speed,300)!=ESP_OK || rpm.value!=0 || speed.value!=0){snprintf(out,cap,"ENGINE_OFF_STATIONARY_CHECK_FAILED");return false;}
        esp_err_t result=obd_clear_dtcs(1500);
        if(result!=ESP_OK){snprintf(out,cap,"CLEAR_UNVERIFIED_%s",esp_err_to_name(result));can_bus_print_diagnostics();return false;}
        snprintf(out,cap,"CLEARED");return true;
#else
        snprintf(out,cap,"CLEAR_DISABLED");return false;
#endif
    }
#endif
    snprintf(out,cap,"UNKNOWN_COMMAND");return false;
}
static void stream_sample(void)
{
#if CONFIG_GATEWAY_SIMULATION
    unsigned phase=(s_sample++/6)%4;
    const int rpm[]={1800,1900,2050,1800},temp[]={82,90,105,82};
    const float volts[]={28.4f,29.2f,30.1f,28.4f};
    printf("LIVE:RPM:%d\nLIVE:COOLANT_TEMP:%d\nLIVE:BATTERY_VOLTAGE:%.2f\nLIVE:SPEED:0\nLIVE:ENGINE_LOAD:20\n",rpm[phase],temp[phase],volts[phase]);
#else
    if(!s_ready){printf("EVT|OBD_ERROR|CAN_NOT_READY\n");return;}
    const uint8_t pids[]={OBD_PID_ENGINE_RPM,OBD_PID_COOLANT_TEMP,OBD_PID_VEHICLE_SPEED,OBD_PID_ENGINE_LOAD,OBD_PID_MODULE_VOLTAGE};
    const char *names[]={"RPM","COOLANT_TEMP","SPEED","ENGINE_LOAD","BATTERY_VOLTAGE"};
    unsigned i=s_sample++%5;obd_pid_value_t value;
    esp_err_t result=obd_read_pid(pids[i],&value,250);
    if(result==ESP_OK) printf("LIVE:%s:%.2f\n",names[i],value.value);
    else printf("EVT|OBD_ERROR|%s:%s\n",names[i],esp_err_to_name(result));
#endif
}
void app_main(void)
{
    uart_config_t config={.baud_rate=115200,.data_bits=UART_DATA_8_BITS,.parity=UART_PARITY_DISABLE,.stop_bits=UART_STOP_BITS_1,.flow_ctrl=UART_HW_FLOWCTRL_DISABLE,.source_clk=UART_SCLK_DEFAULT};
    ESP_ERROR_CHECK(uart_param_config(UART_NUM_0,&config));
    ESP_ERROR_CHECK(uart_set_pin(UART_NUM_0,UART_PIN_NO_CHANGE,UART_PIN_NO_CHANGE,UART_PIN_NO_CHANGE,UART_PIN_NO_CHANGE));
    ESP_ERROR_CHECK(uart_driver_install(UART_NUM_0,512,0,0,NULL,0));
#if CONFIG_GATEWAY_BENCH_TEST
    esp_err_t bench=can_bus_run_transceiver_test();
    printf("CAN:TRANSCEIVER:%s\n",bench==ESP_OK?"PASS":esp_err_to_name(bench));fflush(stdout);return;
#endif
#if CONFIG_GATEWAY_VEHICLE
    esp_err_t init=can_bus_init(CAN_BUS_MODE_DIAGNOSTIC,(can_bus_bitrate_t)CONFIG_GATEWAY_BITRATE);
    if(init==ESP_OK) {
        init=can_bus_start();
    }
    s_ready=init==ESP_OK;
    printf("CAN:INIT:%s\n",esp_err_to_name(init));
#endif
    gateway_protocol_t protocol;gateway_protocol_init(&protocol);gateway_protocol_set_handler(&protocol,command,NULL);
    char line[128],response[560];size_t length=0;bool discard=false;
    printf("SYS:READY\n");fflush(stdout);
    int64_t last_sample=0;
    while(true) {
        uint8_t bytes[64];int count=uart_read_bytes(UART_NUM_0,bytes,sizeof(bytes),pdMS_TO_TICKS(20));
        for(int i=0;i<count;++i){char c=(char)bytes[i];
            if(c=='\r'||c=='\n'){
                if(!discard && length){line[length]=0;if(gateway_protocol_handle_line(&protocol,line,response,sizeof(response)))printf("%s\n",response);else printf("SYS:INVALID_REQUEST\n");}
                length=0;discard=false;
            }else if(!discard){if(length+1<sizeof(line))line[length++]=c;else{length=0;discard=true;printf("SYS:REQUEST_TOO_LONG\n");}}
        }
        int64_t now=esp_timer_get_time();
        int64_t period=
#if CONFIG_GATEWAY_SIMULATION
            1000000;
#else
            200000;
#endif
        if(protocol.state==GATEWAY_STATE_STREAMING && now-last_sample>=period){last_sample=now;stream_sample();}
        fflush(stdout);vTaskDelay(pdMS_TO_TICKS(10));
    }
}
