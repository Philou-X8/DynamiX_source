#pragma once
#include <stdint.h>
#include <atomic>

// Reads the MLX90614 IR thermometer (I2C) in its own low-priority FreeRTOS
// task, every Config::TEMPERATURE_PERIOD_MS, and keeps the newest valid
// reading. The I2C calls block only this task (the ESP32 driver sleeps while
// the hardware transfers), so a slow or stuck bus never delays the main loop.
//
// After TEMPERATURE_RECOVERY_FAILURES failed reads in a row, the I2C driver is
// restarted, so a sensor that was unplugged works again without a reboot.
class TemperatureReader {
public:
    void begin(int core, unsigned priority);

    // Object temperature in 0.01 degC, or 0 if no read had a valid checksum
    // for Config::TEMPERATURE_STALE_MS (or none yet). Safe from any task.
    int32_t centiCelsius() const { return value_.load(std::memory_order_relaxed); }

private:
    static void taskFn(void* self);
    void run();
    void startBus();
    bool readObject(int32_t& centiCelsius);

    std::atomic<int32_t> value_{0};
};
