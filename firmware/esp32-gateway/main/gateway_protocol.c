#include "gateway_protocol.h"

#include <errno.h>
#include <limits.h>
#include <stdio.h>
#include <stdlib.h>
#include <string.h>

#define MAX_REQUEST_LENGTH  128

static bool parse_request_id(const char *text, int *request_id);

static bool write_response(char *response_buffer, size_t response_buffer_size, int request_id, const char *status, const char *payload);

static void remove_line_ending(char *text);

void gateway_protocol_init(gateway_protocol_t *protocol)
{
    if (protocol == NULL)
    {
        return;
    }

    protocol->state = GATEWAY_STATE_IDLE;
}

bool gateway_protocol_handle_line(gateway_protocol_t *protocol, const char *request_line, char *response_buffer, size_t response_buffer_size)
{
    if (protocol == NULL ||
        request_line == NULL ||
        response_buffer == NULL ||
        response_buffer_size == 0)
    {
        return false;
    }

    size_t request_length = strnlen(request_line, MAX_REQUEST_LENGTH);

    if (request_length == 0 || request_length >= MAX_REQUEST_LENGTH)
    {
        return false;
    }

    char request_copy[MAX_REQUEST_LENGTH];

    memcpy(request_copy, request_line, request_length);

    request_copy[request_length] = '\0';

    remove_line_ending(request_copy);

    char *save_pointer = NULL;

    char *prefix = strtok_r(request_copy, "|", &save_pointer);

    char *request_id_text = strtok_r(NULL, "|", &save_pointer);

    char *command = strtok_r(NULL, "|", &save_pointer);

    char *extra_field = strtok_r(NULL, "|", &save_pointer);

    if (prefix == NULL || request_id_text == NULL || command == NULL || extra_field != NULL)
    {
        return false;
    }

    if (strcmp(prefix, "REQ") != 0)
    {
        return false;
    }

    int request_id;

    if (!parse_request_id(request_id_text, &request_id))
    {
        return false;
    }

    if (strcmp(command, "PING") == 0 )
    {
        return write_response(response_buffer,response_buffer_size, request_id, "OK", "PONG");
    }

    if (strcmp(command, "START") == 0 )
    {
        protocol->state = GATEWAY_STATE_STREAMING;

        return write_response(response_buffer,response_buffer_size, request_id, "OK", "STREAMING");
    }

    if (strcmp(command, "STOP") == 0 )
    {
        protocol->state = GATEWAY_STATE_IDLE;

        return write_response(response_buffer,response_buffer_size, request_id, "OK", "STOPPED");
    }

    if (strcmp(command, "STATUS") == 0)
    {
        const char *state_payload = protocol->state == GATEWAY_STATE_STREAMING ? "STATE=STREAMING" : "STATE=IDLE";

        return write_response(response_buffer,response_buffer_size, request_id, "OK", state_payload);
    }

    return write_response(response_buffer,response_buffer_size, request_id, "ERR", "UNKNOWN_COMMAND");
}

static bool parse_request_id(const char *text, int *request_id)
{
    if (text == NULL || request_id == NULL || text[0] == '\0')
    {
        return false;
    }

    errno = 0;

    char *end_pointer = NULL;

    long parsed_value = strtol( text, &end_pointer, 10);

    if (errno != 0 || end_pointer == text || *end_pointer != '\0' || parsed_value <= 0 || parsed_value > INT_MAX)
    {
        return false;
    }

    *request_id = (int)parsed_value;

    return true;
}

static bool write_response(char *response_buffer, size_t response_buffer_size, int request_id, const char *status, const char *payload)
{
    int written_character_count = snprintf(response_buffer, response_buffer_size, "RES|%d|%s|%s", request_id, status, payload);

    return written_character_count >= 0 && (size_t)written_character_count < response_buffer_size;
}

static void remove_line_ending(char *text)
{
    size_t length = strlen(text);

    while (length > 0)
    {
        char final_character = text[length - 1];

        if (final_character != '\r' && final_character != '\n')
        {
            break;
        }

        text[length - 1] = '\0';
        length--;
    }
}