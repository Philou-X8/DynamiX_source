#include "drive_protocol.h"

uint16_t modbusCrc16(const uint8_t* data, size_t length) {
    uint16_t crc = 0xFFFF;
    for (size_t pos = 0; pos < length; pos++) {
        crc ^= data[pos];
        for (int i = 0; i < 8; i++) {
            if (crc & 0x0001) {
                crc >>= 1;
                crc ^= 0xA001;
            } else {
                crc >>= 1;
            }
        }
    }
    return crc;
}

bool modbusFrameCrcOk(const uint8_t* frame, size_t length) {
    if (length < 4) {
        return false;
    }
    uint16_t crc = modbusCrc16(frame, length - 2);
    return frame[length - 2] == (crc & 0xFF) && frame[length - 1] == (crc >> 8);
}

void buildReadRequest(uint8_t address, uint16_t reg, uint16_t count, uint8_t* out) {
    out[0] = address;
    out[1] = 0x03;
    out[2] = (uint8_t)(reg >> 8);
    out[3] = (uint8_t)(reg & 0xFF);
    out[4] = (uint8_t)(count >> 8);
    out[5] = (uint8_t)(count & 0xFF);
    uint16_t crc = modbusCrc16(out, 6);
    out[6] = (uint8_t)(crc & 0xFF);
    out[7] = (uint8_t)(crc >> 8);
}

bool parseReadResponseOneRegister(const uint8_t* frame, size_t length, uint8_t address, uint16_t& value) {
    if (length != 7 || frame[0] != address || frame[1] != 0x03 || frame[2] != 0x02) {
        return false;
    }
    if (!modbusFrameCrcOk(frame, length)) {
        return false;
    }
    value = (uint16_t)(((uint16_t)frame[3] << 8) | frame[4]);
    return true;
}

bool parseExceptionResponse(const uint8_t* frame, size_t length, uint8_t address,
                            uint8_t& functionCode, uint8_t& errorCode) {
    if (length != 5 || frame[0] != address || (frame[1] & 0x80) == 0) {
        return false;
    }
    if (!modbusFrameCrcOk(frame, length)) {
        return false;
    }
    functionCode = frame[1] & 0x7F;
    errorCode = frame[2];
    return true;
}
