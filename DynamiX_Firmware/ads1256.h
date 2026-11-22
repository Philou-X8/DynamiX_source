#pragma once
#include <stdint.h>

class SPIClass;

// Minimal ADS1256 driver on raw SPI, ported from the bring-up test
// (adc_test.ino, single-board mode). Every wait in here is bounded, so a
// missing or unplugged ADC makes calls fail instead of hanging the task.
//
// convert() sleeps until the DRDY interrupt wakes the calling task, so the
// sensor task leaves the CPU free while the ADC converts. begin() and
// convert() must both be called from that same task.
class Ads1256 {
public:
    struct Pins {
        int8_t sck, miso, mosi, cs, drdy, sync, reset;
    };

    struct Settings {
        uint8_t status;   // STATUS register (byte order, auto-calibration, input buffer)
        uint8_t drate;    // DRATE register (conversion rate)
        uint8_t calMux;   // input pair and PGA used for the start-up self-calibration
        uint8_t calAdcon;
        uint32_t spiHz;
        uint32_t conversionTimeoutMs;
    };

    // Brings the chip up: hardware reset, chip ID check, configuration (read
    // back to check the wiring), self-calibration. On failure returns false
    // and points 'whyFailed' at a short explanation for the GUI log.
    bool begin(SPIClass& spi, const Pins& pins, const Settings& settings, const char*& whyFailed);

    // Selects an input pair and PGA (MUX and ADCON registers), restarts the
    // converter and returns its first settled result (signed 24-bit counts).
    // Returns false if the conversion didn't finish in time.
    bool convert(uint8_t mux, uint8_t adcon, int32_t& value);

private:
    void sendCommand(uint8_t command);
    uint8_t readRegister(uint8_t reg);
    void writeRegister(uint8_t reg, uint8_t value);
    int32_t readData();
    bool waitDrdyLow(uint32_t timeoutMs);
    void select();
    void deselect();

    static void onDrdy(void* self); // DRDY falling edge interrupt

    SPIClass* spi_ = nullptr;
    Pins pins_{};
    Settings settings_{};
    void* task_ = nullptr;          // TaskHandle_t woken by the DRDY interrupt
    bool interruptAttached_ = false;
};
