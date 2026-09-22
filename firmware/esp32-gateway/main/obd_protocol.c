#include "obd_protocol.h"
#include "esp_timer.h"
#include <stdio.h>
#include <string.h>
#define PAYLOAD_CAPACITY (2 + 2 * OBD_MAX_DTCS)
static int64_t s_request_started_us;

static bool build(uint8_t mode, uint8_t pid, bool has_pid, can_bus_frame_t *frame)
{
    if (!frame) return false;
    *frame = (can_bus_frame_t){.id=OBD_PHYSICAL_REQUEST_ID,.dlc=8,.data_length=8};
    frame->data[0] = has_pid ? 2 : 1; frame->data[1]=mode; frame->data[2]=has_pid ? pid : 0;
    return true;
}
bool obd_build_pid_request(const obd_pid_request_t *r, can_bus_frame_t *f) { return r && build(r->mode,r->pid,true,f); }
bool obd_build_dtc_request(can_bus_frame_t *f) { return build(3,0,false,f); }
static bool valid_frame(const can_bus_frame_t *f)
{ return f && !f->is_remote && !f->is_extended && f->data_length > 0 && f->data_length <= 8 && f->dlc == f->data_length; }
static bool decode_pid(const uint8_t *p, size_t n, obd_pid_value_t *out)
{
    if (!out || n < 3 || p[0] != 0x41) return false;
    float value;
    switch (p[1]) {
        case OBD_PID_ENGINE_RPM: if(n!=4) return false; value=((p[2]*256)+p[3])/4.0f; break;
        case OBD_PID_COOLANT_TEMP: if(n!=3) return false; value=(float)p[2]-40; break;
        case OBD_PID_VEHICLE_SPEED: if(n!=3) return false; value=p[2]; break;
        case OBD_PID_ENGINE_LOAD: if(n!=3) return false; value=p[2]*100.0f/255; break;
        case OBD_PID_MODULE_VOLTAGE: if(n!=4) return false; value=((p[2]*256)+p[3])/1000.0f; break;
        default: return false;
    }
    *out=(obd_pid_value_t){.pid=p[1],.value=value}; return true;
}
bool obd_decode_pid_response(const can_bus_frame_t *f, obd_pid_value_t *out)
{
    return valid_frame(f) && f->data[0] >= 3 && f->data[0] <= 7 && f->data[0]+1 <= f->data_length && decode_pid(f->data+1,f->data[0],out);
}
bool obd_decode_dtc(uint8_t hi,uint8_t lo,obd_dtc_t *out)
{
    if (!out) return false;
    static const char systems[]="PCBU";
    out->raw_high=hi;out->raw_low=lo;
    snprintf(out->code,sizeof(out->code),"%c%X%X%X%X",systems[hi>>6],(hi>>4)&3,hi&15,lo>>4,lo&15);
    return true;
}
static bool decode_dtcs(const uint8_t *p,size_t n,obd_dtc_list_t *out)
{
    if (!out) return false;
    out->count=0;
    // ISO 15765 Mode 03: 43, NUMBER_OF_DTCS, then two bytes per code.
    if (n<2 || p[0]!=0x43 || p[1]>OBD_MAX_DTCS || n < (size_t)(2+2*p[1])) return false;
    for (size_t i=2+2*p[1];i<n;++i) if(p[i]!=0) return false;
    obd_dtc_list_t decoded={0};
    for (uint8_t i=0;i<p[1];++i) {
        uint8_t hi=p[2+2*i],lo=p[3+2*i];
        if (hi==0 && lo==0) return false;
        obd_decode_dtc(hi,lo,&decoded.dtcs[i]);
    }
    decoded.count=p[1];*out=decoded;return true;
}
bool obd_decode_dtc_response(const can_bus_frame_t *f,obd_dtc_list_t *out)
{
    if(out) out->count=0;
    return valid_frame(f) && f->data[0]>=2 && f->data[0]<=7 && f->data[0]+1<=f->data_length && decode_dtcs(f->data+1,f->data[0],out);
}
static esp_err_t request(uint8_t mode,uint8_t pid,bool has_pid,uint32_t timeout)
{
    can_bus_frame_t f;
    build(mode,pid,has_pid,&f);
    // Bounded drain plus receive timestamps prevent accepting queued old replies.
    can_bus_frame_t stale;
    for (int i=0;i<32 && can_bus_receive(&stale,0)==ESP_OK;++i) { }
    s_request_started_us=esp_timer_get_time();
    return can_bus_transmit(&f,timeout);
}
esp_err_t obd_request_pid(uint8_t pid,uint32_t timeout) {return request(1,pid,true,timeout);}
esp_err_t obd_request_dtcs(uint32_t timeout) {return request(3,0,false,timeout);}

// Bounded ISO-TP receiver for one explicitly addressed 11-bit ECU. Absolute
// deadline, matching response ID/service, length and sequence checks; no retries.
static esp_err_t receive_payload(uint8_t mode,int pid,uint8_t *payload,size_t *length,uint32_t timeout)
{
    int64_t deadline=esp_timer_get_time()+(int64_t)timeout*1000;
    size_t used=0,total=0; uint8_t sequence=1;
    while (esp_timer_get_time()<deadline) {
        int64_t remaining=deadline-esp_timer_get_time();
        if (remaining<=0) break;
        can_bus_frame_t f;
        esp_err_t result=can_bus_receive(&f,(uint32_t)((remaining+999)/1000));
        if(result!=ESP_OK) return result;
        if (!valid_frame(&f) || f.id!=OBD_PHYSICAL_RESPONSE_ID || f.timestamp_us<s_request_started_us) continue;
        uint8_t type=f.data[0]>>4;
        if(total==0) {
            const uint8_t *p; size_t available;
            if(type==0) {
                total=f.data[0]&15;
                if(total==0 || total>7 || total+1>f.data_length) return ESP_FAIL;
                p=f.data+1;available=total;
            } else if(type==1 && f.data_length==8) {
                total=((f.data[0]&15)<<8)|f.data[1];
                if(total<=7 || total>PAYLOAD_CAPACITY) return ESP_ERR_NOT_SUPPORTED;
                p=f.data+2;available=6;
            } else continue;
            if(p[0]==0x7F && available>=3 && p[1]==mode) {
                total=0;
                if(p[2]==0x78) continue; // response-pending, same deadline
                return ESP_FAIL;
            }
            if(p[0]!=(mode+0x40) || (pid>=0 && (available<2 || p[1]!=(uint8_t)pid))) {total=0;continue;}
            memcpy(payload,p,available);used=available;
            if(type==1) {
                can_bus_frame_t flow={.id=OBD_PHYSICAL_REQUEST_ID,.dlc=8,.data_length=8,.data={0x30,0,10}};
                result=can_bus_transmit(&flow,100);
                if(result!=ESP_OK) return result;
            }
        } else {
            if(type!=2 || (f.data[0]&15)!=sequence) return ESP_FAIL;
            size_t copy=total-used; if(copy>7) copy=7;
            if(f.data_length<copy+1) return ESP_FAIL;
            memcpy(payload+used,f.data+1,copy);used+=copy;sequence=(sequence+1)&15;
        }
        if(used==total) {*length=total;return ESP_OK;}
    }
    return ESP_ERR_TIMEOUT;
}
esp_err_t obd_wait_pid_response(uint8_t pid,obd_pid_value_t *out,uint32_t timeout)
{
    if(!out) return ESP_ERR_INVALID_ARG;
    uint8_t payload[PAYLOAD_CAPACITY];size_t n=0;
    esp_err_t result=receive_payload(1,pid,payload,&n,timeout);
    return result==ESP_OK ? (decode_pid(payload,n,out)?ESP_OK:ESP_FAIL) : result;
}
esp_err_t obd_wait_dtc_response(obd_dtc_list_t *out,uint32_t timeout)
{
    if(!out) return ESP_ERR_INVALID_ARG;
    out->count=0;uint8_t payload[PAYLOAD_CAPACITY];size_t n=0;
    esp_err_t result=receive_payload(3,-1,payload,&n,timeout);
    return result==ESP_OK ? (decode_dtcs(payload,n,out)?ESP_OK:ESP_FAIL) : result;
}
esp_err_t obd_clear_dtcs(uint32_t timeout)
{
    esp_err_t result=request(4,0,false,100);
    if(result!=ESP_OK) return result;
    uint8_t payload[PAYLOAD_CAPACITY];size_t n=0;
    result=receive_payload(4,-1,payload,&n,timeout);
    return result==ESP_OK ? (n==1 && payload[0]==0x44?ESP_OK:ESP_FAIL) : result;
}
esp_err_t obd_read_pid(uint8_t pid,obd_pid_value_t *out,uint32_t timeout)
{
    esp_err_t result=obd_request_pid(pid,100);
    return result==ESP_OK ? obd_wait_pid_response(pid,out,timeout) : result;
}
