#pragma once
#include <stdint.h>
#include <stddef.h>

// Modbus RTU helpers for the TD350 motor drive (manual, chapter 9). Pure
// functions on byte buffers, no Arduino or UART dependency, so the logic can
// be checked on a PC.

constexpr uint8_t DRIVE_ADDRESS = 0x03;              // Modbus slave address of the drive
constexpr uint16_t DRIVE_REG_RUNNING_FREQ = 0x3000;  // read-only, unit 0.01 Hz
constexpr size_t DRIVE_READ_REQUEST_SIZE = 8;

// Modbus RTU CRC-16 (poly 0xA001). In a frame it is sent low byte first.
uint16_t modbusCrc16(const uint8_t* data, size_t length);

// True if the last two bytes of 'frame' are the correct CRC of the bytes
// before them. Needs length >= 4.
bool modbusFrameCrcOk(const uint8_t* frame, size_t length);

// Builds a "read holding registers" (0x03) request:
// [address][0x03][reg hi][reg lo][count hi][count lo][crc lo][crc hi].
// 'out' must hold DRIVE_READ_REQUEST_SIZE bytes.
void buildReadRequest(uint8_t address, uint16_t reg, uint16_t count, uint8_t* out);

// Parses the answer to a one-register read:
// [address][0x03][0x02][value hi][value lo][crc lo][crc hi].
// Returns true and fills 'value' only for a complete, CRC-valid answer from 'address'.
bool parseReadResponseOneRegister(const uint8_t* frame, size_t length, uint8_t address, uint16_t& value);

// Parses an error answer: [address][function | 0x80][error code][crc lo][crc hi].
// Returns true and fills the two outputs (function without the 0x80 flag) for a
// complete, CRC-valid error answer from 'address'.
bool parseExceptionResponse(const uint8_t* frame, size_t length, uint8_t address,
                            uint8_t& functionCode, uint8_t& errorCode);
