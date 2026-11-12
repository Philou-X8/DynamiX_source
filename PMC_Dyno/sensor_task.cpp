#include "sensor_task.h"
#include <Arduino.h>
#include "ADS1256.h" // note: this header #defines RESET, SYNC, WAKEUP, ... as
                     // command bytes, so don't use those names in this file
#include <freertos/FreeRTOS.h>
#include <freertos/task.h>
#include <freertos/semphr.h>
#include <esp_attr.h>

// Survives a watchdog/panic reset (RTC memory is not cleared by a software
// reset), so the next boot can report what each task was doing when it died.
RTC_NOINIT_ATTR volatile uint8_t g_sensorStage[2];

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

// A live ADS1256 holds DRDY low almost all the time (it only pulses high
// briefly before each new sample), while an unconnected pin with a pull-up
// sits high. One noise glitch to LOW must not count as "ADC present": a false
// positive makes the library's InitializeADC() wait on DRDY forever, which
// starves the idle task and trips the watchdog. So sample over a few ms and
// require DRDY to be low in at least 3 of every 4 samples.
bool drdyLooksAlive(int8_t pin) {
    constexpr int SAMPLES = 64;
    int lows = 0;
    for (int i = 0; i < SAMPLES; i++) {
        if (digitalRead(pin) == LOW) {
            lows++;
        }
        delayMicroseconds(50);
    }
    return lows * 4 >= SAMPLES * 3;
}

// Polls DRDY for up to timeoutMs, yielding between polls.
bool drdyGoesLow(int8_t pin, uint32_t timeoutMs) {
    const uint32_t start = millis();
    while (millis() - start < timeoutMs) {
        if (digitalRead(pin) == LOW && drdyLooksAlive(pin)) {
            return true;
        }
        vTaskDelay(1);
    }
    return false;
}

// What this task is doing, for the post-reset report. See sensorStage names
// in sensor_task.h.
inline void setStage(uint8_t id, uint8_t stage) {
    g_sensorStage[id & 1] = stage;
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

// Busy-waits (briefly) for DRDY high.
bool drdyHighWithin(int8_t pin, uint32_t timeoutUs) {
    const uint32_t start = micros();
    while (micros() - start < timeoutUs) {
        if (digitalRead(pin) == HIGH) {
            return true;
        }
    }
    return false;
}

// DRDY goes high right after a read (first SCLK edge), so this only ever has
// to wait a few microseconds. Anything near this long means the line is stuck.
constexpr uint32_t DRDY_HIGH_WAIT_US = 300;

// ADS1256 commands / SPI mode, same values the library uses.
constexpr uint8_t CMD_RDATAC = 0x03;
const SPISettings ADC_SPI_SETTINGS(1920000, MSBFIRST, SPI_MODE1);

// Reads one sample in read-data-continuous mode WITHOUT the library, whose
// readSingleContinuous() waits on DRDY with unbounded while() loops. A stuck
// DRDY line there spins the task forever at priority 2, starves the idle task
// and trips the task watchdog.
//
// The caller has already seen DRDY low. The first call starts RDATAC mode and
// leaves CS low and the SPI transaction open (same as the library does), so
// every call after that only clocks out 3 bytes. 'streaming' tracks that.
// Returns false if DRDY didn't return high after the read.
bool readSampleBounded(SPIClass* spi, int8_t csPin, int8_t drdyPin,
                       bool& streaming, int32_t& out) {
    if (!streaming) {
        spi->beginTransaction(ADC_SPI_SETTINGS);
        digitalWrite(csPin, LOW);
        spi->transfer(CMD_RDATAC);
        delayMicroseconds(7); // t6, datasheet figure 30
        streaming = true;
    }
    const uint32_t b0 = spi->transfer(0);
    const uint32_t b1 = spi->transfer(0);
    const uint32_t b2 = spi->transfer(0);
    const uint32_t u = (b0 << 16) | (b1 << 8) | b2;
    out = (int32_t)(u << 8) >> 8; // sign-extend from bit 23
    return drdyHighWithin(drdyPin, DRDY_HIGH_WAIT_US);
}

// Normal init takes ~4 s (the library's built-in delays), so this is generous.
constexpr uint32_t INIT_TIMEOUT_MS = 15000;

struct InitJob {
    ADS1256* adc;
    const AdcConfig* cfg;
    volatile bool done;
};

// Same sequence as the ESPLOADCELL test sketch.
void adcInitTaskFn(void* arg) {
    InitJob* job = (InitJob*)arg;
    job->adc->InitializeADC();
    job->adc->setAutoCal(1);
    job->adc->setBuffer(job->cfg->buffer ? 1 : 0);
    job->adc->setPGA(job->cfg->pga);
    job->adc->setDRATE(job->cfg->drate);
    job->adc->setMUX(job->cfg->mux);
    job->done = true;
    vTaskDelete(nullptr);
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
    const uint8_t id = params->id;
    setStage(id, STAGE_PRESENCE);
    if (drdyGoesLow(cfg.drdyPin, PRESENCE_TIMEOUT_MS)) {
        setStage(id, STAGE_LIB_INIT); // the library's unbounded waits live here
        // The library is built once and never freed (the task never exits).
        ADS1256* candidate = new ADS1256(cfg.drdyPin, cfg.resetPin, cfg.syncPin, cfg.csPin,
                                         cfg.vref, params->spi);
        pinMode(cfg.drdyPin, INPUT_PULLUP); // the constructor reset it to plain INPUT

        // The library's init and config calls wait on DRDY with unbounded
        // while() loops, and I can't bound those. So they run in a helper task
        // at the IDLE priority: if one of them spins forever it only shares
        // the core with the idle task (round-robin) instead of starving it, so
        // the watchdog stays fed. This task gives up on it after a timeout and
        // treats the ADC as absent.
        InitJob job = {candidate, &cfg, false};
        TaskHandle_t helper = nullptr;
        xTaskCreatePinnedToCore(adcInitTaskFn, "adc_init", 4096, &job,
                                tskIDLE_PRIORITY, &helper, xPortGetCoreID());
        const uint32_t initStart = millis();
        while (!job.done && millis() - initStart < INIT_TIMEOUT_MS) {
            vTaskDelay(pdMS_TO_TICKS(20));
        }
        if (job.done) {
            adc = candidate;
            params->target->present = true;
        } else {
            // Stuck inside the library. Kill the helper; its SPI bus may be
            // left locked, but this ADC is never touched again.
            if (helper != nullptr) {
                vTaskDelete(helper);
            }
            setStage(id, STAGE_INIT_FAILED);
        }
    }
    if (adc != nullptr) {
        setStage(id, STAGE_RUNNING);
    } else if (g_sensorStage[id & 1] != STAGE_INIT_FAILED) {
        setStage(id, STAGE_ABSENT);
    }
    // else: nothing is wired. Leave 'present' false and publish a blank value.

    const TickType_t periodTicks = pdMS_TO_TICKS(2); // target ~500 Hz
    bool streaming = false; // RDATAC mode started (see readSampleBounded)

    for (;;) {
        TickType_t loopStart = xTaskGetTickCount();
        int32_t raw = 0;

        if (adc == nullptr) {
            // No ADC: keep the sequence counter moving (so the main loop's
            // stall detection doesn't mistake this for a hung task) and
            // publish a blank reading.
            sharedSensorDataWrite(*(params->target), BLANK_READING);
        } else if (drdyLowWithin(cfg.drdyPin, DRDY_WAIT_US) &&
                   (setStage(id, STAGE_READING),
                    readSampleBounded(params->spi, cfg.csPin, cfg.drdyPin, streaming, raw))) {
            setStage(id, STAGE_RUNNING);
            // Every wait above and inside the read is bounded. The (3-byte)
            // SPI read happens outside any lock.
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
