#pragma once
#include <stdint.h>

// Command flags: the first byte of every command frame body (see protocol.h).
//
// Must mirror the CommandFlags enum in CSharpProto1/Serial/SerialDecoder.cs.
// Only flags that travel over the wire are listed here; the C# enum also holds
// GUI-internal events (GUI_click_start, GUI_disconnected, ...). If a value
// changes, change it in both places.
//
// Multi-byte payload values are big-endian. "int24" = 24-bit two's complement.

namespace Cmd {

// --- ESP -> GUI ------------------------------------------------------------------
constexpr uint8_t ESP_good_connection = 1;   // answer to GUI_check_connection
constexpr uint8_t ESP_message         = 2;   // payload: text for the log
constexpr uint8_t ESP_error           = 3;   // payload: text for the log
constexpr uint8_t ESP_debug           = 4;   // payload: text for the log (progress details)
constexpr uint8_t ESP_run_accepted    = 5;   // GUI_run_request accepted, the ESP is Armed
constexpr uint8_t ESP_run_starting    = 6;   // GUI_run_start accepted, the motor is starting
constexpr uint8_t ESP_run_finished    = 7;   // every step done; sent after the run's last points
constexpr uint8_t ESP_emergency       = 8;   // the ESP went to Panic: motor stopped. The reason is
                                             // in the ESP_error sent just before. Cleared by GUI_abort.
constexpr uint8_t ESP_emergency_clear = 9;   // left Panic (after GUI_abort or GUI_check_connection)
constexpr uint8_t ESP_door_open       = 10;  // (not used yet)
constexpr uint8_t ESP_door_close      = 11;  // (not used yet)
constexpr uint8_t ESP_calibration_data = 50; // payload: pos min(int24), pos max(int24), force(int24), raw counts.
                                             // After a run calibration: force at rest with the shock mounted.
                                             // After a TAR: the empty load. After GUI_calibrate while Idle: the
                                             // current force (no motion).
constexpr uint8_t ESP_sequence_separator = 51; // payload: start(4), end(4) of a step's section, in
                                               // microseconds since the run started (same clock as the points)
constexpr uint8_t ESP_live_data       = 52;  // every HEARTBEAT_PERIOD_MS, in every state. payload: position,
                                             // stroke min, stroke max, force, temperature (int24 each, raw
                                             // counts). 0 for what isn't known (no stroke before a TAR or a
                                             // calibration, sensors not delivering). Temperature: always 0 for now.
constexpr uint8_t ESP_run_denied      = 55;  // the run request / calibration / start was refused or failed
                                             // before the run started; the ESP is back to Idle (or Panic)

// --- GUI -> ESP ------------------------------------------------------------------
constexpr uint8_t GUI_check_connection = 12; // ping; also stops everything and returns to Idle
constexpr uint8_t GUI_run_request      = 14; // payload: number of GUI_send_sequence steps that will follow (1 byte)
constexpr uint8_t GUI_run_start        = 15;
constexpr uint8_t GUI_wave_format      = 16; // (not used)
constexpr uint8_t GUI_TAR_request      = 17; // measure the empty load, then bottom out (machine must be empty)
constexpr uint8_t GUI_bottom_out       = 18; // bottom out only (no empty load measurement)
constexpr uint8_t GUI_abort            = 19; // stop the motor, back to Idle (also clears Panic)
constexpr uint8_t GUI_calibrate        = 40; // while Armed: run calibration (no payload). While Idle: just
                                             // report the stored stroke and the current force
                                             // (ESP_calibration_data).
constexpr uint8_t GUI_set_rpm          = 41; // debug, Idle only: payload is a complete Modbus frame
                                             // (CRC included) forwarded to the motor drive
constexpr uint8_t GUI_send_sequence    = 42; // payload (10 bytes): order(1), number of cycles(1),
                                             // Modbus frame setting the drive frequency(8)
constexpr uint8_t GUI_force_reboot     = 43; // stop the motor and restart the ESP
constexpr uint8_t GUI_set_failsafe     = 44; // any state. payload: force min(int24), force max(int24), raw
                                             // counts: the motor stops (Panic) when the force leaves it

} // namespace Cmd
