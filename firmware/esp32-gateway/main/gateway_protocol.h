#ifndef GATEWAY_PROTOCOL_H
#define GATEWAY_PROTOCOL_H

#include <stdbool.h>
#include <stddef.h>

typedef enum
{
    GATEWAY_STATE_IDLE = 0,
    GATEWAY_STATE_STREAMING
} gateway_state_t;

typedef struct 
{
    gateway_state_t state ;
} gateway_protocol_t;

void gateway_protocol_init(gateway_protocol_t *protocol);

bool gateway_protocol_handle_line(gateway_protocol_t * protocol, const char *request_line, char *response_buffer, size_t response_buffer_size);

#endif
