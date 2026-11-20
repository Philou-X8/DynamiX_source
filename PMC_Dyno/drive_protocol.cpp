#include "drive_protocol.h"
#include "bytes.h"

namespace {

void appendCrc(uint8_t* frame, size_t lengthWithoutCrc) {
    const uint16_t crc = modbusCrc16(frame, lengthWithoutCrc);
    frame[lengthWithoutCrc] = (uint8_t)(crc & 0xFF);
    frame[lengthWithoutCrc + 1] = (uint8_t)(crc >> 8);
}

void buildRequest(uint8_t address, uint8_t function, uint16_t reg, uint16_t value, uint8_t* out) {
    out[0] = address;
    out[1] = function;
    writeUint16BE(reg, out + 2);
    writeUint16BE(value, out + 4);
    appendCrc(out, 6);
}

} // namespace

uint16_t modbusCrc16(const uint8_t* data, size_t length) {
    uint16_t crc = 0xFFFF;
    for (size_t pos = 0; pos < length; pos++) {
        crc ^= data[pos];
        for (int bit = 0; bit < 8; bit++) {
            crc = (crc & 0x0001) ? (uint16_t)((crc >> 1) ^ 0xA001) : (uint16_t)(crc >> 1);
        }
    }
    return crc;
}

bool modbusFrameCrcOk(const uint8_t* frame, size_t length) {
    if (length < 4) {
        return false;
    }
    const uint16_t crc = modbusCrc16(frame, length - 2);
    return frame[length - 2] == (crc & 0xFF) && frame[length - 1] == (crc >> 8);
}

void buildWriteRequest(uint8_t address, uint16_t reg, uint16_t value, uint8_t* out) {
    buildRequest(address, MODBUS_WRITE_REGISTER, reg, value, out);
}

void buildReadRequest(uint8_t address, uint16_t reg, uint16_t count, uint8_t* out) {
    buildRequest(address, MODBUS_READ_REGISTERS, reg, count, out);
}

void buildEchoRequest(uint8_t address, uint16_t data, uint8_t* out) {
    buildRequest(address, MODBUS_DIAGNOSTIC, MODBUS_DIAG_ECHO, data, out);
}

bool parseSetFrequencyRequest(const uint8_t* frame, size_t length, uint16_t& centiHz) {
    if (length != MODBUS_REQUEST_SIZE || frame[0] != DRIVE_ADDRESS ||
        frame[1] != MODBUS_WRITE_REGISTER || !modbusFrameCrcOk(frame, length)) {
        return false;
    }
    const uint16_t reg = readUint16BE(frame + 2);
    if (reg != DRIVE_REG_SET_FREQUENCY && reg != DRIVE_REG_SET_FREQUENCY_RAM) {
        return false;
    }
    centiHz = readUint16BE(frame + 4);
    return true;
}

bool parseReadResponseOneRegister(const uint8_t* frame, size_t length, uint8_t address, uint16_t& value) {
    if (length != 7 || frame[0] != address || frame[1] != MODBUS_READ_REGISTERS || frame[2] != 0x02 ||
        !modbusFrameCrcOk(frame, length)) {
        return false;
    }
    value = readUint16BE(frame + 3);
    return true;
}

bool parseExceptionResponse(const uint8_t* frame, size_t length, uint8_t address,
                            uint8_t& functionCode, uint8_t& errorCode) {
    if (length != 5 || frame[0] != address || (frame[1] & 0x80) == 0 || !modbusFrameCrcOk(frame, length)) {
        return false;
    }
    functionCode = frame[1] & 0x7F;
    errorCode = frame[2];
    return true;
}
