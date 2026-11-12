#pragma once
#include <stdint.h>
#include <SPI.h>

// One instance of this per ADS1256. Written by the sensor task at ~500 Hz,
// read by the main loop. The mutex only ever guards a plain copy of a few
// bytes -- the SPI transaction itself must happen OUTSIDE the lock (see
// design doc section 3) so the main loop is never blocked waiting on SPI.
struct SharedSensorData {
    int32_t rawValue;     // latest reading, signed 24-bit range
                          // (-8388608..8388607), sign-extended to 32 bits
    uint32_t sequence;    // increments every update; main loop uses this
                          // to detect a stalled sensor task (-> panic)
    volatile bool present; // false until the sensor task has detected an ADC;
                           // stays false if none is wired, in which case
                           // rawValue is a fixed blank value (see sensor_task.cpp)
    void* mutex;           // SemaphoreHandle_t, opaque here
};

void sharedSensorDataInit(SharedSensorData& s);

// Thread-safe accessors.
void sharedSensorDataWrite(SharedSensorData& s, int32_t value);
void sharedSensorDataRead(SharedSensorData& s, int32_t& outValue, uint32_t& outSequence);

// Breadcrumb each sensor task leaves in RTC memory (index = SensorTaskParams::id)
// so a watchdog reset can be traced to the task that was spinning. Valid after
// a non-power-on reset only.
extern volatile uint8_t g_sensorStage[2];
enum SensorStage : uint8_t {
    STAGE_NONE     = 0, // task not started yet
    STAGE_PRESENCE = 1, // checking whether an ADC is wired
    STAGE_LIB_INIT = 2, // inside the ADS1256 library's InitializeADC/config (unbounded waits)
    STAGE_ABSENT   = 3, // no ADC: publishing blank values
    STAGE_RUNNING  = 4, // normal sampling loop
    STAGE_READING  = 5, // inside the SPI read
    STAGE_INIT_FAILED = 6 // library init never finished (gave up after a timeout); treated as absent
};

// Per-ADC settings. The values are the ADS1256 library's macros (PGA_64,
// DRATE_1000SPS, DIFF_0_1, ...), so fill them in from the .ino, which
// includes ADS1256.h.
struct AdcConfig {
    int8_t drdyPin;
    int8_t resetPin;
    int8_t syncPin;
    int8_t csPin;
    float vref;       // reference voltage in volts
    uint8_t pga;      // PGA_x
    uint8_t drate;    // DRATE_xxxSPS
    uint8_t mux;      // DIFF_x_y or SING_x
    bool buffer;      // input buffer on/off
};

// Parameters passed to each sensor task via its FreeRTOS task parameter
// pointer. Each ADS1256 sits on its own SPI bus (SPIClass), so the two tasks
// never contend for the bus.
struct SensorTaskParams {
    uint8_t id;            // 0 = position, 1 = force; indexes g_sensorStage[]
    SharedSensorData* target;
    SPIClass* spi;         // bus this ADC is wired to (&SPI for VSPI, or an HSPI instance)
    int8_t sckPin;
    int8_t misoPin;
    int8_t mosiPin;
    AdcConfig adc;
};

// FreeRTOS task entry point. One of these runs per ADS1256, pinned to a
// core other than the main loop's if possible. Detects whether the ADC is
// actually there, configures it, then free-runs: reads a sample (without
// ever waiting on DRDY indefinitely) and takes the mutex only long enough to
// publish the new value + bump the sequence counter.
void sensorTaskFn(void* pvParameters);
