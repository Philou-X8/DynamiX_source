#include "temperature_reader.h"
#include <Arduino.h>
#include <Wire.h>
#include <freertos/FreeRTOS.h>
#include <freertos/task.h>
#include "config.h"
#include "mlx90614.h"

namespace {
constexpr uint32_t TASK_STACK = 3072;
} // namespace

void TemperatureReader::begin(int core, unsigned priority) {
    xTaskCreatePinnedToCore(taskFn, "temperature", TASK_STACK, this, priority, nullptr, core);
}

void TemperatureReader::taskFn(void* self) {
    ((TemperatureReader*)self)->run();
}

void TemperatureReader::startBus() {
    Wire.begin(Pins::TEMPERATURE_SDA, Pins::TEMPERATURE_SCL, Config::TEMPERATURE_I2C_HZ);
    Wire.setTimeOut(Config::TEMPERATURE_I2C_TIMEOUT_MS);
}

void TemperatureReader::run() {
    startBus();
    uint32_t lastValidMs = millis();
    uint32_t failuresInARow = 0;
    TickType_t lastWake = xTaskGetTickCount();
    for (;;) {
        vTaskDelayUntil(&lastWake, pdMS_TO_TICKS(Config::TEMPERATURE_PERIOD_MS));
        const uint32_t nowMs = millis();
        int32_t centiCelsius;
        if (readObject(centiCelsius)) {
            value_.store(centiCelsius, std::memory_order_relaxed);
            lastValidMs = nowMs;
            failuresInARow = 0;
            continue;
        }
        if (nowMs - lastValidMs >= Config::TEMPERATURE_STALE_MS) {
            value_.store(0, std::memory_order_relaxed);
        }
        // Bus recovery: restart the driver (a few microseconds of work, in
        // this task), at most once every TEMPERATURE_RECOVERY_FAILURES reads.
        if (++failuresInARow >= Config::TEMPERATURE_RECOVERY_FAILURES) {
            failuresInARow = 0;
            Wire.end();
            startBus();
        }
    }
}

// Register read with a repeated start (the MLX90614 needs it), checksum checked.
bool TemperatureReader::readObject(int32_t& centiCelsius) {
    Wire.beginTransmission(MLX_ADDRESS);
    Wire.write(MLX_REG_OBJECT);
    if (Wire.endTransmission(false) != 0) {
        return false;
    }
    if (Wire.requestFrom((int)MLX_ADDRESS, 3) != 3) {
        return false;
    }
    uint8_t reply[3];
    for (uint8_t& b : reply) {
        b = (uint8_t)Wire.read();
    }
    return mlxParseTemperature(MLX_ADDRESS, MLX_REG_OBJECT, reply, centiCelsius);
}
