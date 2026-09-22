#include "sensor_task.h"
#include <freertos/FreeRTOS.h>
#include <freertos/task.h>
#include <freertos/semphr.h>

void sharedSensorDataInit(SharedSensorData& s) {
    s.rawValue = 0;
    s.sequence = 0;
    s.mutex = xSemaphoreCreateMutex();
}

void sharedSensorDataWrite(SharedSensorData& s, int32_t value) {
    xSemaphoreTake((SemaphoreHandle_t)s.mutex, portMAX_DELAY);
    s.rawValue = value;
    s.sequence++;
    xSemaphoreGive((SemaphoreHandle_t)s.mutex);
}

void sharedSensorDataRead(SharedSensorData& s, int32_t& outValue, uint32_t& outSequence) {
    xSemaphoreTake((SemaphoreHandle_t)s.mutex, portMAX_DELAY);
    outValue = s.rawValue;
    outSequence = s.sequence;
    xSemaphoreGive((SemaphoreHandle_t)s.mutex);
}

void sensorTaskFn(void* pvParameters) {
    SensorTaskParams* params = (SensorTaskParams*)pvParameters;

    // TODO: construct/reference the ADS1256 instance for this task here
    // (or receive it fully configured via params), matching whatever
    // constructor signature the chosen library uses.

    const TickType_t periodTicks = pdMS_TO_TICKS(2); // target ~500 Hz

    for (;;) {
        TickType_t loopStart = xTaskGetTickCount();

        // TODO: replace with the real read, e.g.:
        //   int32_t raw = adc.readSingleContinuous();
        // This call blocks on DRDY internally -- that's fine here, it
        // happens outside any lock. See earlier discussion: block time
        // scales with the ADC's configured data rate.
        int32_t raw = 0;

        sharedSensorDataWrite(*(params->target), raw);

        // Free-running at ~500 Hz. Not using vTaskDelayUntil's strict
        // cadence here is deliberate -- exact phase doesn't matter per the
        // design doc's timing-margin discussion, only that both sensor
        // tasks and the main loop stay roughly in the same ballpark.
        vTaskDelayUntil(&loopStart, periodTicks);
    }
}
