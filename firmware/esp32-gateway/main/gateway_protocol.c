#include "gateway_protocol.h"
#include <stdio.h>
#include <string.h>
#include <limits.h>
void gateway_protocol_init(gateway_protocol_t *p) {if(p) *p=(gateway_protocol_t){0};}
void gateway_protocol_set_handler(gateway_protocol_t *p,gateway_handler_t h,void *ctx) {if(p){p->handler=h;p->context=ctx;}}
bool gateway_protocol_handle_line(gateway_protocol_t *p,const char *line,char *response,size_t capacity)
{
    // A too-small caller buffer must be rejected before commands with side
    // effects (START/CLEAR_DTC) are executed.
    if(!p || !line || !response || capacity<531) return false;
    size_t n=0;while(n<128 && line[n]) ++n;
    if(n==0 || n>=128) return false;
    char copy[128];memcpy(copy,line,n+1);
    while(n && (copy[n-1]=='\r'||copy[n-1]=='\n')) copy[--n]=0;
    if(strncmp(copy,"REQ|",4)!=0) return false;
    char *id_text=copy+4,*sep=strchr(id_text,'|');
    if(!sep || sep==id_text || !sep[1] || strchr(sep+1,'|')) return false;
    *sep=0;int id=0;
    for(char *c=id_text;*c;++c){if(*c<'0'||*c>'9'||id>(INT_MAX-(*c-'0'))/10)return false;id=id*10+(*c-'0');}
    if(id<=0) return false;
    char *command=sep+1;char payload[512];const char *status="OK";
    if(strcmp(command,"PING")==0) strcpy(payload,"PONG");
    else if(strcmp(command,"START")==0){p->state=GATEWAY_STATE_STREAMING;strcpy(payload,"STREAMING");}
    else if(strcmp(command,"STOP")==0){p->state=GATEWAY_STATE_IDLE;strcpy(payload,"STOPPED");}
    else if(strcmp(command,"STATUS")==0) strcpy(payload,p->state==GATEWAY_STATE_STREAMING?"STATE=STREAMING":"STATE=IDLE");
    else if(strcmp(command,"INFO")==0 || strcmp(command,"SCAN_DTC")==0 || strcmp(command,"CLEAR_DTC")==0) {
        bool clear=strcmp(command,"CLEAR_DTC")==0;
        if(clear && p->last_clear_id==id){status="ERR";strcpy(payload,"DUPLICATE_CLEAR");}
        else {
            if(clear) p->last_clear_id=id;
            strcpy(payload,"NOT_READY");
            if(!p->handler || !p->handler(command,payload,sizeof(payload),p->context)) status="ERR";
        }
    } else {status="ERR";strcpy(payload,"UNKNOWN_COMMAND");}
    int written=snprintf(response,capacity,"RES|%d|%s|%s",id,status,payload);
    return written>=0 && (size_t)written<capacity;
}
