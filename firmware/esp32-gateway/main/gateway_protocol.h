#ifndef GATEWAY_PROTOCOL_H
#define GATEWAY_PROTOCOL_H
#include <stdbool.h>
#include <stddef.h>
typedef enum { GATEWAY_STATE_IDLE=0,GATEWAY_STATE_STREAMING } gateway_state_t;
typedef bool (*gateway_handler_t)(const char *command,char *payload,size_t capacity,void *context);
typedef struct {
    gateway_state_t state;
    gateway_handler_t handler;
    void *context;
    int last_clear_id;
} gateway_protocol_t;
void gateway_protocol_init(gateway_protocol_t *);
void gateway_protocol_set_handler(gateway_protocol_t *,gateway_handler_t,void *);
bool gateway_protocol_handle_line(gateway_protocol_t *,const char *,char *,size_t);
#endif
