#pragma once
#include <stdint.h>

// Command flags: the first byte of every command frame body (see protocol.h).
//
// Must mirror the CommandFlags enum in DynamiX_HMI/Serial/SerialDecoder.cs.
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
constexpr uint8_t ESP_run_finished    = 7;   // every step done and the yoke bottomed out; sent after the
                                             // run's last points
constexpr uint8_t ESP_emergency       = 8;   // the ESP went to Panic: motor stopped. The reason is
                                             // in the ESP_error sent just before. Cleared by GUI_abort.
constexpr uint8_t ESP_emergency_clear = 9;   // left Panic (after GUI_abort or GUI_check_connection)
constexpr uint8_t ESP_door_open       = 10;  // (not used yet)
constexpr uint8_t ESP_door_close      = 11;  // (not used yet)
constexpr uint8_t ESP_calibration_data = 50; // end of a run calibration. payload: pos min, pos max, static
                                             // force at the bottom and at the top of the stroke (a line fitted
                                             // through one slow cycle, see StaticForceFit); int24 each, raw counts
constexpr uint8_t ESP_sequence_separator = 51; // payload: start(4), end(4) of a step's section, in
                                               // microseconds since the run started (same clock as the points)
constexpr uint8_t ESP_live_data       = 52;  // every HEARTBEAT_PERIOD_MS, in every state (it is also what tells the
                                             // GUI the ESP is alive, see GUI_heartbeat). payload: position,
                                             // stroke min, stroke max, force (int24 each, raw counts),
                                             // temperature (int24, 0.01 degC, IR sensor). 0 for what isn't
                                             // known (no stroke before a TAR or a calibration, sensors not
                                             // delivering, no valid temperature read for 1 s).
constexpr uint8_t ESP_tar_data        = 53;  // end of a TAR or a GUI_bottom_out. payload: pos min, pos max,
                                             // force (TAR: the empty load; bottom out: the current force);
                                             // int24 each, raw counts
constexpr uint8_t ESP_run_denied      = 55;  // the run request / calibration / start was refused or failed
                                             // before the run started; the ESP is back to Idle (or Panic)
constexpr uint8_t ESP_not_connected   = 56;  // answer to a GUI_heartbeat when no GUI was linked: the ESP rebooted,
                                             // or it stopped the motor after GUI_TIMEOUT_MS of silence. The GUI
                                             // answers with GUI_check_connection.

// --- GUI -> ESP ------------------------------------------------------------------
constexpr uint8_t GUI_check_connection = 12; // ping; also stops everything and returns to Idle
constexpr uint8_t GUI_run_request      = 14; // payload: number of GUI_send_sequence steps that will follow (1 byte)
constexpr uint8_t GUI_run_start        = 15;
constexpr uint8_t GUI_wave_format      = 16; // (not used)
constexpr uint8_t GUI_TAR_request      = 17; // measure the empty load, find the stroke, bottom out (machine
                                             // must be empty). Ends with ESP_tar_data
constexpr uint8_t GUI_bottom_out       = 18; // same without the empty load measurement. Ends with ESP_tar_data
constexpr uint8_t GUI_abort            = 19; // stop the motor, back to Idle (also clears Panic)
constexpr uint8_t GUI_calibrate        = 40; // while Armed: run calibration (no payload), ends with
                                             // ESP_calibration_data (or ESP_run_denied)
constexpr uint8_t GUI_set_rpm          = 41; // debug, Idle only: payload is a complete Modbus frame
                                             // (CRC included) forwarded to the motor drive
constexpr uint8_t GUI_send_sequence    = 42; // payload (10 bytes): order(1), number of cycles(1),
                                             // Modbus frame setting the drive frequency(8)
constexpr uint8_t GUI_force_reboot     = 43; // stop the motor and restart the ESP
constexpr uint8_t GUI_set_failsafe     = 44; // any state. payload: force min(int24), force max(int24), raw
                                             // counts: the motor stops (Panic) when the force leaves it
constexpr uint8_t GUI_heartbeat        = 45; // no payload. The GUI sends it every 200 ms from its UI thread, so a
                                             // closed, crashed or frozen GUI stops sending it. Any command counts
                                             // as a sign of life; once a GUI has sent one, GUI_TIMEOUT_MS of
                                             // silence stops the motor (config.h)

} // namespace Cmd
