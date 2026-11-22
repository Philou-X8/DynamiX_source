#pragma once
#include <stdint.h>
#include <stddef.h>

// MLX90614 (GY-906) IR thermometer, on SMBus (I2C). The pure part: checksum
// and conversion, no Arduino, so the host tests check it. The bus itself is
// read by temperature_reader.

constexpr uint8_t MLX_ADDRESS = 0x5A;    // factory address
constexpr uint8_t MLX_REG_OBJECT = 0x07; // RAM: temperature of what the sensor points at

// SMBus packet error code: CRC-8, polynomial x^8 + x^2 + x + 1 (0x07), starting at 0.
inline uint8_t smbusCrc8(const uint8_t* data, size_t length) {
    uint8_t crc = 0;
    for (size_t i = 0; i < length; i++) {
        crc ^= data[i];
        for (int bit = 0; bit < 8; bit++) {
            crc = (crc & 0x80) ? (uint8_t)((crc << 1) ^ 0x07) : (uint8_t)(crc << 1);
        }
    }
    return crc;
}

// A register read answers [low byte][high byte][PEC]. The PEC covers the whole
// exchange: address (write), register, address (read), low, high.
// Returns true and the temperature in 0.01 degC if the PEC matches and the
// sensor doesn't flag an error (bit 15). The register holds 0.02 K per count.
inline bool mlxParseTemperature(uint8_t address, uint8_t reg, const uint8_t reply[3], int32_t& centiCelsius) {
    const uint8_t covered[5] = {
        (uint8_t)(address << 1), reg, (uint8_t)((address << 1) | 1), reply[0], reply[1],
    };
    if (smbusCrc8(covered, sizeof(covered)) != reply[2]) {
        return false;
    }
    const uint16_t raw = (uint16_t)(reply[0] | (reply[1] << 8));
    if (raw & 0x8000) {
        return false;
    }
    centiCelsius = (int32_t)raw * 2 - 27315;
    return true;
}
