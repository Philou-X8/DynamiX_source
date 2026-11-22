#pragma once
#include <stdint.h>
#include <stddef.h>

// Modbus RTU frames for the TD350 motor drive (manual, chapter 9). Pure
// functions on byte buffers, no Arduino or UART dependency, so they are
// checked on a PC by the host tests.

constexpr uint8_t DRIVE_ADDRESS = 0x03;          // Modbus slave address of the drive

constexpr uint8_t MODBUS_READ_REGISTERS = 0x03;
constexpr uint8_t MODBUS_WRITE_REGISTER = 0x06;
// (0x08 diagnostic/echo is listed in the manual, but our drive answers it
// with exception 0x06 "frame error": don't use it.)
constexpr size_t MODBUS_REQUEST_SIZE = 8;        // read and write requests are both 8 bytes

// Registers (manual 9.4.5)
constexpr uint16_t DRIVE_REG_SET_FREQUENCY = 0x000A;     // P00.10, unit 0.01 Hz
constexpr uint16_t DRIVE_REG_SET_FREQUENCY_RAM = 0x800A; // same, written to RAM only (no EEPROM wear)
constexpr uint16_t DRIVE_REG_CONTROL = 0x2000;           // control command
constexpr uint16_t DRIVE_CONTROL_FORWARD = 0x0001;       //   forward running
constexpr uint16_t DRIVE_CONTROL_STOP = 0x0005;          //   stop (decelerates, P00.12)
// (0x0007 fault reset: never sent. Only the machine's reset button re-arms the drive.)
constexpr uint16_t DRIVE_REG_RUNNING_FREQUENCY = 0x3000; // read only, unit 0.01 Hz
constexpr uint16_t DRIVE_REG_COMM_TIMEOUT_RAM = 0x8E04;  // P14.04 in RAM, unit 0.1 s (0 = off)

// Modbus RTU CRC-16 (poly 0xA001). In a frame it is sent low byte first.
uint16_t modbusCrc16(const uint8_t* data, size_t length);

// True if the last two bytes of 'frame' are the CRC of the bytes before them.
bool modbusFrameCrcOk(const uint8_t* frame, size_t length);

// [address][0x06][reg hi][reg lo][value hi][value lo][crc lo][crc hi]
// 'out' must hold MODBUS_REQUEST_SIZE bytes.
void buildWriteRequest(uint8_t address, uint16_t reg, uint16_t value, uint8_t* out);

// [address][0x03][reg hi][reg lo][count hi][count lo][crc lo][crc hi]
void buildReadRequest(uint8_t address, uint16_t reg, uint16_t count, uint8_t* out);

// True if 'frame' is a valid write of the set frequency (P00.10 or its RAM
// alias) to the drive; fills the frequency (0.01 Hz).
bool parseSetFrequencyRequest(const uint8_t* frame, size_t length, uint16_t& centiHz);

// Answer to a one-register read: [address][0x03][0x02][value hi][value lo][crc][crc].
// Returns true and fills 'value' only for a complete, CRC-valid answer from 'address'.
bool parseReadResponseOneRegister(const uint8_t* frame, size_t length, uint8_t address, uint16_t& value);

// Error answer: [address][function | 0x80][error code][crc][crc]. Returns true
// and fills the outputs (function without the 0x80 flag) for a complete,
// CRC-valid error answer from 'address'.
bool parseExceptionResponse(const uint8_t* frame, size_t length, uint8_t address,
                            uint8_t& functionCode, uint8_t& errorCode);
