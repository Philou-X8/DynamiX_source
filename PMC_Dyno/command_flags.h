#pragma once
#include <stdint.h>

// Command flag values: the first byte of every 0x55 (command) frame body.
//
// Must mirror the CommandFlags enum in CSharpProto1/Serial/SerialDecoder.cs.
// Only flags that actually travel over the wire are listed here; the C#
// enum also holds GUI-internal events (GUI_click_start, GUI_disconnected,
// BTN_*) that never reach the ESP. Values are subject to change -- if one
// changes, change it in both places.

namespace Cmd {

// ESP -> GUI
constexpr uint8_t ESP_good_connection = 1;   // pong
constexpr uint8_t ESP_message         = 2;
constexpr uint8_t ESP_error           = 3;
constexpr uint8_t ESP_debug           = 4;
constexpr uint8_t ESP_run_accepted    = 5;
constexpr uint8_t ESP_run_denied      = 55;
constexpr uint8_t ESP_run_starting    = 6;
constexpr uint8_t ESP_run_finished    = 7;
constexpr uint8_t ESP_emergency       = 8;
constexpr uint8_t ESP_emergency_clear = 9;
constexpr uint8_t ESP_door_open       = 10;
constexpr uint8_t ESP_door_close      = 11;
constexpr uint8_t ESP_calibration_data = 50;  // payload: pos min(3), pos max(3), idle force(3), big-endian.
constexpr uint8_t ESP_sequence_separator = 51; // payload: start timestamp(4), end timestamp(4), big-endian, in
                                               // microseconds since the run started (same clock as the points)

// GUI -> ESP
constexpr uint8_t GUI_check_connection = 12; // ping
constexpr uint8_t GUI_run_request      = 14; // payload: 1 byte, number of GUI_send_sequence items that will follow
constexpr uint8_t GUI_run_start        = 15;
constexpr uint8_t GUI_wave_format      = 16;
constexpr uint8_t GUI_TAR_request      = 17;
constexpr uint8_t GUI_bottom_out       = 18;
constexpr uint8_t GUI_abort            = 19;
constexpr uint8_t GUI_calibrate        = 40; // request pos min/max + current load cell reading
constexpr uint8_t GUI_set_rpm          = 41; // TEMP debug: payload is a complete Modbus frame (CRC included)
                                             // that the ESP forwards to the motor drive
constexpr uint8_t GUI_send_sequence    = 42; // payload (10 bytes): order(1), number of cycles(1), Modbus frame setting the drive frequency(8)
constexpr uint8_t GUI_force_reboot     = 43;
} // namespace Cmd
