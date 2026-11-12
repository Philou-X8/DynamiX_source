#include "sensor_task.h"
#include <Arduino.h>
#include "ADS1256.h" // note: this header #defines RESET, SYNC, WAKEUP, ... as
                     // command bytes, so don't use those names in this file
#include <freertos/FreeRTOS.h>
#include <freertos/task.h>
#include <freertos/semphr.h>

namespace {

// Value published while no ADC is wired: a clean "blank" reading of 0.
constexpr int32_t BLANK_READING = 0;

// Clamps to the signed 24-bit range so a stray value can never wrap when it
// is written to the wire as 3 bytes.
int32_t clamp24(int32_t v) {
    if (v > 8388607) return 8388607;
    if (v < -8388608) return -8388608;
    return v;
}

// How long after boot to wait for DRDY to ever go low before deciding no
// ADC is attached. A live ADS1256 pulls DRDY low within a few ms of power-up.
constexpr uint32_t PRESENCE_TIMEOUT_MS = 500;

// Max time to wait for a fresh sample (DRDY low) in one iteration. One
// sample takes 1 ms at 1000 SPS, so this is a little over one period.
constexpr uint32_t DRDY_WAIT_US = 1200;

// Polls DRDY for up to timeoutMs, yielding between polls.
bool drdyGoesLow(int8_t pin, uint32_t timeoutMs) {
    const uint32_t start = millis();
    while (millis() - start < timeoutMs) {
        if (digitalRead(pin) == LOW) {
            return true;
        }
        vTaskDelay(1);
    }
    return false;
}

// Busy-waits (briefly) for DRDY low. Bounded, unlike the library's own wait.
bool drdyLowWithin(int8_t pin, uint32_t timeoutUs) {
    const uint32_t start = micros();
    while (micros() - start < timeoutUs) {
        if (digitalRead(pin) == LOW) {
            return true;
        }
    }
    return false;
}

} // namespace

void sharedSensorDataInit(SharedSensorData& s) {
    s.rawValue = 0;
    s.sequence = 0;
    s.present = false;
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
    const AdcConfig& cfg = params->adc;

    // The ADS1256 library waits on DRDY with unbounded while() loops. With no
    // ADC attached that means hanging forever (and starving the watchdog), so
    // everything below makes sure the library is only called when the chip
    // is really there and DRDY is already low.

    // Bring the pins to a safe idle state *before* looking for the ADC:
    // RESET/SYNC high (an output left low would hold the ADC in reset and
    // DRDY would never toggle), CS deselected, DRDY with a pull-up so an
    // unconnected line reads as "no data" instead of floating.
    pinMode(cfg.resetPin, OUTPUT);
    digitalWrite(cfg.resetPin, HIGH);
    pinMode(cfg.syncPin, OUTPUT);
    digitalWrite(cfg.syncPin, HIGH);
    pinMode(cfg.csPin, OUTPUT);
    digitalWrite(cfg.csPin, HIGH);
    pinMode(cfg.drdyPin, INPUT_PULLUP);

    // Start this ADC's SPI bus on the pins chosen in the .ino. The library's
    // InitializeADC() calls spi->begin() again with default pins; the
    // ESP32 core ignores a begin() on a bus that is already started.
    params->spi->begin(params->sckPin, params->misoPin, params->mosiPin, cfg.csPin);

    ADS1256* adc = nullptr;
    if (drdyGoesLow(cfg.drdyPin, PRESENCE_TIMEOUT_MS)) {
        // The library is built once and never freed (the task never exits).
        adc = new ADS1256(cfg.drdyPin, cfg.resetPin, cfg.syncPin, cfg.csPin,
                          cfg.vref, params->spi);
        pinMode(cfg.drdyPin, INPUT_PULLUP); // the constructor reset it to plain INPUT

        // Same sequence as the ESPLOADCELL test sketch. This takes a few
        // seconds because of the library's built-in delays.
        adc->InitializeADC();
        adc->setAutoCal(1);
        adc->setBuffer(cfg.buffer ? 1 : 0);
        adc->setPGA(cfg.pga);
        adc->setDRATE(cfg.drate);
        adc->setMUX(cfg.mux);

        params->target->present = true;
    }
    // else: nothing is wired. Leave 'present' false and publish a blank value.

    const TickType_t periodTicks = pdMS_TO_TICKS(2); // target ~500 Hz

    for (;;) {
        TickType_t loopStart = xTaskGetTickCount();

        if (adc == nullptr) {
            // No ADC: keep the sequence counter moving (so the main loop's
            // stall detection doesn't mistake this for a hung task) and
            // publish a blank reading.
            sharedSensorDataWrite(*(params->target), BLANK_READING);
        } else if (drdyLowWithin(cfg.drdyPin, DRDY_WAIT_US)) {
            // DRDY is already low, so the library's own wait returns
            // immediately. The (3-byte) SPI read happens outside any lock.
            int32_t raw = adc->readSingleContinuous();
            sharedSensorDataWrite(*(params->target), clamp24(raw));
        } else {
            // No new sample (ADC unplugged or stuck): publish nothing, so the
            // sequence counter stops and the main loop can detect the stall.
            // Block for a tick so the idle task still gets to run.
            vTaskDelay(1);
            continue;
        }

        // Free-running at ~500 Hz. Not using vTaskDelayUntil's strict
        // cadence here is deliberate -- exact phase doesn't matter per the
        // design doc's timing-margin discussion, only that both sensor
        // tasks and the main loop stay roughly in the same ballpark.
        vTaskDelayUntil(&loopStart, periodTicks);
    }
}
