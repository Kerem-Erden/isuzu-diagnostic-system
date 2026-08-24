#include <stdio.h>
#include <stdint.h>
#include <inttypes.h>

#include "driver/uart.h"
#include "esp_err.h"
#include "gateway_protocol.h"
#include "can_bus.h"

#include "freertos/FreeRTOS.h"
#include "freertos/task.h"

#define SERIAL_UART UART_NUM_0
#define UART_RX_BUFFER_SIZE 256
#define REQUEST_LINE_BUFFER_SIZE 128
#define RESPONSE_BUFFER_SIZE 128
#define CAN_MAX_FRAMES_PER_CYCLE 8

/*
 * Sends one group of simulated vehicle values to the serial output.
 *
 * This function is static because it is used only inside this source file.
 * It returns void because it only sends data and does not calculate a result.
 */

 static void initialize_serial_input(void)
 {
    const uart_config_t uart_configuration = {
                .baud_rate = 115200,
                .data_bits = UART_DATA_8_BITS,
                .parity = UART_PARITY_DISABLE,
                .stop_bits = UART_STOP_BITS_1,
                .flow_ctrl = UART_HW_FLOWCTRL_DISABLE,
                .source_clk = UART_SCLK_DEFAULT
            };

            ESP_ERROR_CHECK(
                uart_param_config(SERIAL_UART, &uart_configuration));
            
            ESP_ERROR_CHECK(
                uart_set_pin(
                        SERIAL_UART,
                        UART_PIN_NO_CHANGE,
                        UART_PIN_NO_CHANGE,
                        UART_PIN_NO_CHANGE,
                        UART_PIN_NO_CHANGE));

            ESP_ERROR_CHECK(
                uart_driver_install(
                        SERIAL_UART,
                        UART_RX_BUFFER_SIZE,
                        0,
                        0,
                        NULL,
                        0));
 }


static void send_live_data(int rpm, int coolant_temperature, float battery_voltage)
{
   	printf("LIVE:RPM:%d\n", rpm);
   	printf("LIVE:COOLANT_TEMP:%d\n", coolant_temperature);
	printf("LIVE:BATTERY_VOLTAGE:%f\n", battery_voltage);

	/*
     * Force buffered serial output to be written immediately.
     * This is useful while testing communication with the PC application.
     */
	fflush(stdout);
}

static void process_serial_input(gateway_protocol_t *protocol)
{
    static char request_line[REQUEST_LINE_BUFFER_SIZE];
    static size_t request_length = 0;

    uint8_t received_bytes[32];

    int received_bytes_count = uart_read_bytes(SERIAL_UART, received_bytes, sizeof(received_bytes), pdMS_TO_TICKS(20));

    for (int index = 0; index < received_bytes_count; index++)
    {
        char received_character = (char)received_bytes[index];

        /*if (received_character == '\r')
        {
            continue;
        }*/

        if (received_character == '\n' || received_character == '\r')
        {
            if (request_length > 0 )
            {
                request_line[request_length] = '\0';

                char response_buffer[RESPONSE_BUFFER_SIZE];

                bool response_created = gateway_protocol_handle_line(protocol, request_line, response_buffer, sizeof(response_buffer));

                if (response_created)
                {
                    printf("%s\n", response_buffer);
                }
                else
                {
                    printf("SYS:INVALID_REQUEST\n");
                }

                fflush(stdout);
                request_length = 0;
            }

            continue;
        }

        if (request_length < REQUEST_LINE_BUFFER_SIZE - 1)
        {
            request_line[request_length] = received_character;

            request_length++;
        }
        else
        {
            request_length = 0;
            
            printf("SYS:REQUEST_TOO_LONG\n");

            fflush(stdout);
        }
    }
}

static void start_can_listener(void)
{
    esp_err_t result = can_bus_init();

    if (result != ESP_OK)
    {
        printf("CAN:ERROR:INIT:%s\n", esp_err_to_name(result));
        fflush(stdout);
        return;
    }

    printf("\nCAN:INITIALIZED\n");

    result = can_bus_start();

    if(result != ESP_OK)
    {
        printf("CAN:ERROR:START:%s\n", esp_err_to_name(result));
        can_bus_deinit();
        fflush(stdout);
        return;
    }
    
    printf("CAN:LISTENING\n");
    fflush(stdout);
}

static void process_can_input(void)
{
    can_bus_frame_t frame;

    for (int i = 0; i < CAN_MAX_FRAMES_PER_CYCLE; i++)
    {
        esp_err_t result = can_bus_receive(&frame, 0);

        if (result == ESP_ERR_TIMEOUT)
        {
            break;
        }

        if (result != ESP_OK)
        {
            printf("CAN:RX:EXT:%08" PRIX32 ":%u:", frame.id, frame.data_length);
        }
        else
        {
            printf("CAN:RX:STD:%3" PRIX32 ":%u:", frame.id, frame.data_length);
        }

        if (frame.is_remote)
        {
            printf("RTR");
        }
        else
        {
            for (uint8_t byte_index = 0; byte_index < frame.data_length; byte_index++)
            {
                printf("%02X", frame.data[byte_index]);

                if (byte_index + 1 < frame.data_length)
                {
                    printf(":");
                }
            }
        }

        printf("\n");
        fflush(stdout);
    }
}

void app_main(void)
{
    gateway_protocol_t gateway_protocol;

    gateway_protocol_init(&gateway_protocol);

    initialize_serial_input();

    start_can_listener();

    TickType_t previous_live_data_time = xTaskGetTickCount();

	int rpm = 750;
	int coolant_temperature = 86;
	float battery_voltage = 13.6f;

	/*
     * This message is sent once after the ESP32 application starts.
     * Later, the desktop application will use it to recognize that the
     * diagnostic gateway firmware is running.
  	 */
	printf("SYS:READY\n");
	fflush(stdout);

	while(1) 
	{
        process_serial_input(&gateway_protocol);

        TickType_t current_time = xTaskGetTickCount();

        if (gateway_protocol.state == GATEWAY_STATE_STREAMING && current_time - previous_live_data_time >= pdMS_TO_TICKS(1000))
        {


            send_live_data(rpm, coolant_temperature, battery_voltage);
            rpm += 25;
            coolant_temperature += 1;
            battery_voltage += 0.1f;

            previous_live_data_time = current_time;

        /*
         * Keep the simulated values within a realistic test range.
         * These are not real Isuzu reference values; they are only
         * temporary communication test data.
         */
            if (rpm > 900)
            {
                rpm = 750;
            }

            if (coolant_temperature > 90)
            {
                coolant_temperature = 86;
            }

            if (battery_voltage > 14.2f)
            { 
                battery_voltage = 13.8f;
            }

        }

        process_can_input();

        /*
         * Pause only this FreeRTOS task for one second.
         * The CPU is not trapped in an empty busy-wait loop.
         */
        vTaskDelay(pdMS_TO_TICKS(10));
    }
}
