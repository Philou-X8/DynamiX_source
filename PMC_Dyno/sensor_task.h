#pragma once
#include <stdint.h>
#include <SPI.h>

// One instance of this per ADS1256. Written by the sensor task at ~500 Hz,
// read by the main loop. The mutex only ever guards a plain copy of a few
// bytes -- the SPI transaction itself must happen OUTSIDE the lock (see
// design doc section 3) so the main loop is never blocked waiting on SPI.
struct SharedSensorData {
    uint32_t rawValue;    // latest reading as a 24-bit unsigned value, see
                          // adcToUnsigned24() for the encoding
    uint32_t sequence;    // increments every update; main loop uses this
                          // to detect a stalled sensor task (-> panic)
    volatile bool present; // false until the sensor task has detected an ADC;
                           // stays false if none is wired, in which case
                           // rawValue is a fixed blank value (see sensor_task.cpp)
    void* mutex;           // SemaphoreHandle_t, opaque here
};

void sharedSensorDataInit(SharedSensorData& s);

// Thread-safe accessors.
void sharedSensorDataWrite(SharedSensorData& s, uint32_t value);
void sharedSensorDataRead(SharedSensorData& s, uint32_t& outValue, uint32_t& outSequence);

// Converts the ADS1256's signed reading (-8388608..8388607) to the 24-bit
// unsigned value that goes on the wire, in offset-binary form: signed 0 maps
// to 0x800000, full-scale negative to 0 and full-scale positive to 0xFFFFFF.
// The order is preserved and there is no wrap-around at zero. To get the
// signed value back: (int32_t)u - 0x800000.
uint32_t adcToUnsigned24(int32_t signedReading);

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
