#include "sensor_reader.h"
#include <Arduino.h>
#include <SPI.h>
#include <freertos/FreeRTOS.h>
#include <freertos/queue.h>
#include <freertos/task.h>
#include "command_flags.h"
#include "config.h"
#include "gui_link.h"

namespace {

constexpr uint32_t TASK_STACK = 4096;

const Ads1256::Pins ADC_PINS = {
    Pins::ADC_SCK, Pins::ADC_MISO, Pins::ADC_MOSI, Pins::ADC_CS,
    Pins::ADC_DRDY, Pins::ADC_SYNC, Pins::ADC_RESET,
};

// Self-calibration runs on the load cell input at PGA 64, where offset and
// gain errors matter most. Auto-calibration stays off: it would recalibrate at
// every PGA change, i.e. twice per pair.
const Ads1256::Settings ADC_SETTINGS = {
    Config::ADC_STATUS, Config::ADC_DRATE,
    Config::ADC_MUX_FORCE, Config::ADC_ADCON_FORCE,
    Config::ADC_SPI_HZ, Config::ADC_CONVERSION_TIMEOUT_MS,
};

// The second ADC board isn't read any more. If it's still plugged in, keep it
// deselected and powered down (SYNC/PDWN low stops its clock and converter).
void holdUnusedAdcInPowerDown() {
    pinMode(Pins::UNUSED_ADC_CS, OUTPUT);
    digitalWrite(Pins::UNUSED_ADC_CS, HIGH);
    pinMode(Pins::UNUSED_ADC_RESET, OUTPUT);
    digitalWrite(Pins::UNUSED_ADC_RESET, HIGH);
    pinMode(Pins::UNUSED_ADC_SYNC, OUTPUT);
    digitalWrite(Pins::UNUSED_ADC_SYNC, LOW);
}

} // namespace

void SensorReader::begin(GuiLink* gui, int core, unsigned priority) {
    gui_ = gui;
    mailbox_ = xQueueCreate(1, sizeof(SensorSample));
    xTaskCreatePinnedToCore(taskFn, "sensors", TASK_STACK, this, priority, nullptr, core);
}

bool SensorReader::latest(SensorSample& out) const {
    return xQueuePeek((QueueHandle_t)mailbox_, &out, 0) == pdTRUE;
}

void SensorReader::taskFn(void* self) {
    ((SensorReader*)self)->run();
}

void SensorReader::run() {
    holdUnusedAdcInPowerDown();
    for (;;) {
        if (!startAdc()) {
            vTaskDelay(pdMS_TO_TICKS(Config::ADC_RETRY_MS));
            continue;
        }
        sampleUntilFailure();
        gui_->sendText(Cmd::ESP_error, "ADC stopped converting, restarting it");
    }
}

// Reports the first failure, and only a change after that, so a missing
// board doesn't flood the log every couple of seconds.
bool SensorReader::startAdc() {
    const char* why = nullptr;
    if (!adc_.begin(SPI, ADC_PINS, ADC_SETTINGS, why)) {
        if (why != lastFailure_) {
            gui_->sendf(Cmd::ESP_error, "ADC not ready: %s", why);
            lastFailure_ = why;
        }
        return false;
    }
    lastFailure_ = nullptr;
    gui_->sendText(Cmd::ESP_message, "ADC ready: load cell and LVIT on ADS #1");
    return true;
}

void SensorReader::sampleUntilFailure() {
    uint32_t failures = 0;
    while (failures < Config::ADC_MAX_FAILURES) {
        int32_t force;
        int32_t position;
        if (!adc_.convert(Config::ADC_MUX_FORCE, Config::ADC_ADCON_FORCE, force)) {
            failures++;
            continue;
        }
        const uint32_t timeUs = micros();
        if (!adc_.convert(Config::ADC_MUX_POSITION, Config::ADC_ADCON_POSITION, position)) {
            failures++;
            continue;
        }
        publish(position, force, timeUs);
        failures = 0;
    }
}

void SensorReader::publish(int32_t position, int32_t force, uint32_t timeUs) {
    SensorSample sample;
    sample.position = position;
    sample.force = force;
    sample.timeUs = timeUs;
    sample.sequence = ++sequence_;
    xQueueOverwrite((QueueHandle_t)mailbox_, &sample);
}
