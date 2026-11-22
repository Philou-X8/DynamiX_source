#include "ads1256.h"
#include <Arduino.h>
#include <SPI.h>
#include <freertos/FreeRTOS.h>
#include <freertos/task.h>

namespace {

// Commands (datasheet table 24)
constexpr uint8_t CMD_WAKEUP  = 0x00;
constexpr uint8_t CMD_RDATA   = 0x01;
constexpr uint8_t CMD_SDATAC  = 0x0F;
constexpr uint8_t CMD_RREG    = 0x10;
constexpr uint8_t CMD_WREG    = 0x50;
constexpr uint8_t CMD_SELFCAL = 0xF0;
constexpr uint8_t CMD_SYNC    = 0xFC;

// Registers
constexpr uint8_t REG_STATUS = 0x00;
constexpr uint8_t REG_MUX    = 0x01;
constexpr uint8_t REG_ADCON  = 0x02;
constexpr uint8_t REG_DRATE  = 0x03;
constexpr uint8_t REG_COUNT  = 5;   // STATUS, MUX, ADCON, DRATE, IO
constexpr uint8_t CHIP_ID    = 3;   // STATUS bits 7..4

// Command-to-data delay t6 is 50 ADC clocks (6.5 us at 7.68 MHz).
constexpr uint32_t T6_US = 10;
constexpr uint32_t COMMAND_GAP_US = 5;

// A settled conversion after SYNC/WAKEUP takes far longer than this at any
// rate used here (> 0.4 ms up to 3750 SPS). A DRDY edge seen sooner belongs to
// a conversion started before the restart.
constexpr uint32_t MIN_SETTLE_US = 100;

} // namespace

void IRAM_ATTR Ads1256::onDrdy(void* self) {
    TaskHandle_t task = (TaskHandle_t)((Ads1256*)self)->task_;
    if (task == nullptr) {
        return;
    }
    BaseType_t woken = pdFALSE;
    vTaskNotifyGiveFromISR(task, &woken);
    if (woken == pdTRUE) {
        portYIELD_FROM_ISR();
    }
}

void Ads1256::select() {
    spi_->beginTransaction(SPISettings(settings_.spiHz, MSBFIRST, SPI_MODE1));
    digitalWrite(pins_.cs, LOW);
}

void Ads1256::deselect() {
    digitalWrite(pins_.cs, HIGH);
    spi_->endTransaction();
}

void Ads1256::sendCommand(uint8_t command) {
    select();
    spi_->transfer(command);
    delayMicroseconds(T6_US);
    deselect();
}

uint8_t Ads1256::readRegister(uint8_t reg) {
    select();
    spi_->transfer(CMD_RREG | reg);
    spi_->transfer(0x00); // one register
    delayMicroseconds(T6_US);
    const uint8_t value = spi_->transfer(0xFF);
    delayMicroseconds(T6_US);
    deselect();
    return value;
}

void Ads1256::writeRegister(uint8_t reg, uint8_t value) {
    select();
    spi_->transfer(CMD_WREG | reg);
    spi_->transfer(0x00); // one register
    spi_->transfer(value);
    delayMicroseconds(T6_US);
    deselect();
}

int32_t Ads1256::readData() {
    select();
    spi_->transfer(CMD_RDATA);
    delayMicroseconds(T6_US);
    uint32_t raw = (uint32_t)spi_->transfer(0xFF) << 16;
    raw |= (uint32_t)spi_->transfer(0xFF) << 8;
    raw |= spi_->transfer(0xFF);
    deselect();
    return (int32_t)(raw << 8) >> 8; // sign-extend from bit 23
}

// Start-up only: polls once per tick. A running ADS1256 keeps DRDY low most
// of the time, so this catches it.
bool Ads1256::waitDrdyLow(uint32_t timeoutMs) {
    const uint32_t start = millis();
    while (digitalRead(pins_.drdy) == HIGH) {
        if (millis() - start > timeoutMs) {
            return false;
        }
        vTaskDelay(1);
    }
    return true;
}

bool Ads1256::begin(SPIClass& spi, const Pins& pins, const Settings& settings, const char*& whyFailed) {
    spi_ = &spi;
    pins_ = pins;
    settings_ = settings;
    task_ = xTaskGetCurrentTaskHandle();

    // Every control line idle (high) before any SPI traffic. pinMode first:
    // the ESP32 core ignores writes to pins that aren't configured yet.
    pinMode(pins_.cs, OUTPUT);
    digitalWrite(pins_.cs, HIGH);
    pinMode(pins_.sync, OUTPUT);
    digitalWrite(pins_.sync, HIGH);
    pinMode(pins_.reset, OUTPUT);
    digitalWrite(pins_.reset, HIGH);
    pinMode(pins_.drdy, INPUT_PULLUP);
    spi_->begin(pins_.sck, pins_.miso, pins_.mosi, -1); // CS is driven by hand
    if (!interruptAttached_) {
        attachInterruptArg(digitalPinToInterrupt(pins_.drdy), onDrdy, this, FALLING);
        interruptAttached_ = true;
    }

    // 1. Hardware reset: DRDY must start toggling.
    digitalWrite(pins_.reset, LOW);
    vTaskDelay(pdMS_TO_TICKS(10));
    digitalWrite(pins_.reset, HIGH);
    vTaskDelay(pdMS_TO_TICKS(100));
    if (!waitDrdyLow(1000)) {
        whyFailed = "DRDY never went low after reset (check 5 V/GND at the ADC board, the ribbon, "
                    "and that DRDY, RESET and SYNC reach it)";
        return false;
    }

    // 2. Registers readable and chip ID right: the SPI wiring works.
    sendCommand(CMD_SDATAC);
    vTaskDelay(pdMS_TO_TICKS(1));
    uint8_t regs[REG_COUNT];
    bool allFF = true;
    bool all00 = true;
    for (uint8_t i = 0; i < REG_COUNT; i++) {
        regs[i] = readRegister(i);
        allFF = allFF && regs[i] == 0xFF;
        all00 = all00 && regs[i] == 0x00;
    }
    if (allFF) {
        whyFailed = "registers read as FF (DOUT doesn't reach the ESP32, or CS doesn't reach the ADC)";
        return false;
    }
    if (all00) {
        whyFailed = "registers read as 00 (DOUT stuck low, or DIN/SCK don't reach the ADC)";
        return false;
    }
    if ((regs[REG_STATUS] >> 4) != CHIP_ID) {
        whyFailed = "wrong chip ID (not an ADS1256, or SCK/DIN wiring)";
        return false;
    }

    // 3. Configuration, read back. The final conversion rate is set before
    // the self-calibration, which depends on it.
    const uint8_t values[4] = {settings_.status, settings_.calMux, settings_.calAdcon, settings_.drate};
    for (uint8_t reg = REG_STATUS; reg <= REG_DRATE; reg++) {
        if (!waitDrdyLow(1000)) {
            whyFailed = "DRDY stuck high while configuring";
            return false;
        }
        writeRegister(reg, values[reg]);
        vTaskDelay(pdMS_TO_TICKS(5));
    }
    waitDrdyLow(1000);
    const uint8_t status = readRegister(REG_STATUS);
    const uint8_t mux = readRegister(REG_MUX);
    const uint8_t adcon = readRegister(REG_ADCON);
    const uint8_t drate = readRegister(REG_DRATE);
    if ((status & 0x0E) != (settings_.status & 0x0E) || mux != settings_.calMux ||
        (adcon & 0x07) != (settings_.calAdcon & 0x07) || drate != settings_.drate) {
        whyFailed = "configuration didn't read back (DIN or SCK wiring)";
        return false;
    }

    // 4. Self-calibration (offset and gain) on the calibration input and PGA.
    sendCommand(CMD_SELFCAL);
    vTaskDelay(pdMS_TO_TICKS(20));
    if (!waitDrdyLow(2000)) {
        whyFailed = "self-calibration never finished";
        return false;
    }
    return true;
}

bool Ads1256::convert(uint8_t mux, uint8_t adcon, int32_t& value) {
    // MUX and ADCON in one write, then SYNC + WAKEUP restart the converter so
    // the first result is settled on the new input.
    select();
    spi_->transfer(CMD_WREG | REG_MUX);
    spi_->transfer(0x01); // two registers: MUX, ADCON
    spi_->transfer(mux);
    spi_->transfer(adcon);
    delayMicroseconds(COMMAND_GAP_US);
    spi_->transfer(CMD_SYNC);
    delayMicroseconds(COMMAND_GAP_US);
    spi_->transfer(CMD_WAKEUP);
    deselect();
    const uint32_t restartUs = micros();

    // Sleep until the DRDY edge of the restarted conversion.
    ulTaskNotifyTake(pdTRUE, 0); // forget edges from before the restart
    const TickType_t timeout = pdMS_TO_TICKS(settings_.conversionTimeoutMs);
    for (;;) {
        if (ulTaskNotifyTake(pdTRUE, timeout) == 0) {
            return false; // no conversion: unplugged, or stuck
        }
        if (micros() - restartUs >= MIN_SETTLE_US) {
            break;
        }
    }
    value = readData();
    return true;
}
