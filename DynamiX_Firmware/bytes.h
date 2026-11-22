#pragma once
#include <stdint.h>

// Big-endian (most significant byte first) helpers, the byte order of every
// multi-byte value on the GUI link and on the Modbus link.

constexpr int32_t INT24_MIN = -8388608;
constexpr int32_t INT24_MAX = 8388607;

inline int32_t clampInt24(int32_t v) {
    if (v > INT24_MAX) return INT24_MAX;
    if (v < INT24_MIN) return INT24_MIN;
    return v;
}

inline void writeUint16BE(uint16_t v, uint8_t* out) {
    out[0] = (uint8_t)(v >> 8);
    out[1] = (uint8_t)v;
}

inline uint16_t readUint16BE(const uint8_t* in) {
    return (uint16_t)(((uint16_t)in[0] << 8) | in[1]);
}

inline void writeUint32BE(uint32_t v, uint8_t* out) {
    out[0] = (uint8_t)(v >> 24);
    out[1] = (uint8_t)(v >> 16);
    out[2] = (uint8_t)(v >> 8);
    out[3] = (uint8_t)v;
}

inline uint32_t readUint32BE(const uint8_t* in) {
    return ((uint32_t)in[0] << 24) | ((uint32_t)in[1] << 16) | ((uint32_t)in[2] << 8) | in[3];
}

// 24-bit two's complement (-8388608..8388607). Only the low 24 bits of 'v'
// are written, so an out-of-range value can't spill into the next field.
inline void writeInt24BE(int32_t v, uint8_t* out) {
    const uint32_t u = (uint32_t)v;
    out[0] = (uint8_t)(u >> 16);
    out[1] = (uint8_t)(u >> 8);
    out[2] = (uint8_t)u;
}

inline int32_t readInt24BE(const uint8_t* in) {
    const uint32_t u = ((uint32_t)in[0] << 16) | ((uint32_t)in[1] << 8) | in[2];
    return (int32_t)((u ^ 0x800000u) - 0x800000u); // sign-extend from bit 23
}
