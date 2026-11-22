#pragma once
#include <stdint.h>
#include "ads1256.h"
#include "sensor_sample.h"

class GuiLink;

// Reads the load cell and the LVIT through the single ADS1256, one after the
// other, in its own FreeRTOS task, and publishes the newest pair.
//
// The task starts the ADC (retrying every Config::ADC_RETRY_MS if it isn't
// there), then converts force, then position, over and over (~650 pairs/s at
// 2000 SPS). If conversions keep failing it reports it and starts over.
// Start-up failures and recoveries are sent to the GUI log.
class SensorReader {
public:
    // Starts the task on 'core'. 'gui' receives status messages.
    void begin(GuiLink* gui, int core, unsigned priority);

    // Newest pair. Returns false until the first one is in. Safe from any task.
    bool latest(SensorSample& out) const;

private:
    static void taskFn(void* self);
    void run();
    bool startAdc();
    void sampleUntilFailure();
    void publish(int32_t position, int32_t force, uint32_t timeUs);

    GuiLink* gui_ = nullptr;
    Ads1256 adc_;
    void* mailbox_ = nullptr; // one-slot QueueHandle_t holding the newest SensorSample
    uint32_t sequence_ = 0;
    const char* lastFailure_ = nullptr; // last start-up failure reported to the GUI
};
