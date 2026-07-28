#include <stdio.h>

#include "freertos/FreeRTOS.h"
#include "freertos/task.h"

/*
 * Sends one group of simulated vehicle values to the serial output.
 *
 * This function is static because it is used only inside this source file.
 * It returns void because it only sends data and does not calculate a result.
 */

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

void app_main(void)
{
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
		send_live_data(rpm, coolant_temperature, battery_voltage);
        rpm += 25;
        coolant_temperature += 1;
        battery_voltage += 0.1f;

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

        /*
         * Pause only this FreeRTOS task for one second.
         * The CPU is not trapped in an empty busy-wait loop.
         */
        vTaskDelay(pdMS_TO_TICKS(1000));
    }
}
