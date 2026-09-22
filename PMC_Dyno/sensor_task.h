#pragma once
#include <stdint.h>

// One instance of this per ADS1256. Written by the sensor task at ~500 Hz,
// read by the main loop. The mutex only ever guards a plain copy of a few
// bytes -- the SPI transaction itself must happen OUTSIDE the lock (see
// design doc section 3) so the main loop is never blocked waiting on SPI.
struct SharedSensorData {
    int32_t rawValue;     // latest 24-bit ADC reading, sign-extended
    uint32_t sequence;    // increments every update; main loop uses this
                            // to detect a stalled sensor task (-> panic)
    void* mutex;           // SemaphoreHandle_t, opaque here
};

void sharedSensorDataInit(SharedSensorData& s);

// Thread-safe accessors.
void sharedSensorDataWrite(SharedSensorData& s, int32_t value);
void sharedSensorDataRead(SharedSensorData& s, int32_t& outValue, uint32_t& outSequence);

// Parameters passed to each sensor task via its FreeRTOS task parameter
// pointer. adcSelect distinguishes which ADS1256 instance/CS-DRDY pin set
// this task owns -- fill in against however the ADS1256 library your are
// using is instantiated (see earlier discussion: check whether it takes an
// SPIClass& if you want the two ADCs on independent hardware SPI buses).
struct SensorTaskParams {
    SharedSensorData* target;
    // TODO: ADS1256 instance / pin config reference goes here once the
    // library's instantiation details are finalized (see earlier
    // discussion on SPIClass and hardware bus vs shared-bus wiring).
};

// FreeRTOS task entry point. One of these runs per ADS1256, pinned to a
// core other than the main loop's if possible. Free-running: does a
// (possibly DRDY-blocking) SPI read, then takes the mutex only long enough
// to publish the new value + bump the sequence counter.
void sensorTaskFn(void* pvParameters);
