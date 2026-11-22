#pragma once
#include <stdint.h>

// Arduino's pin levels (esp32-hal-gpio.h, same values), so this file also
// compiles in the host tests.
#ifndef LOW
#define LOW 0x0
#endif
#ifndef HIGH
#define HIGH 0x1
#endif

// Everything that is wired or tuned lives here: pins, link speeds, ADC
// settings, and the numbers the state machine works with. Values marked TUNE
// still need to be checked on the machine.
//
// No Arduino include on purpose: the logic that uses these values is also
// compiled on a PC by the host tests (host_tests/).

// --- Pins --------------------------------------------------------------------
namespace Pins {

// GUI link: UART0 (GPIO1/3) through the USB connector, nothing to set here.

// ADS1256 #1 (the "force" board), on the VSPI bus. It reads BOTH sensors,
// one after the other (a single ADC is far less noisy than two side by side):
//   load cell: chip AIN0(+) / AIN1(-) = board terminals AIN1 / AIN2
//   LVIT:      chip AIN2(+) / AIN3(-) = board terminals AIN3 / AIN4
constexpr int8_t ADC_SCK   = 18;
constexpr int8_t ADC_MISO  = 19; // ADS DOUT
constexpr int8_t ADC_MOSI  = 23; // ADS DIN
constexpr int8_t ADC_CS    = 25;
constexpr int8_t ADC_DRDY  = 5;  // strapping pin, fine as an input with a pull-up
constexpr int8_t ADC_RESET = 26;
constexpr int8_t ADC_SYNC  = 27;

// ADS1256 #2 (the old "position" board) is not read any more. If it is still
// plugged in, it is held in power-down (SYNC/PDWN low) so its clock can't
// disturb ADS #1. Its SPI pins (SCK 14, DOUT 34, DIN 13) and DRDY (32) are
// left untouched.
constexpr int8_t UNUSED_ADC_CS    = 33;
constexpr int8_t UNUSED_ADC_RESET = 21;
constexpr int8_t UNUSED_ADC_SYNC  = 22;

// Motor drive (TD350): RS-485 through the conversion card, on UART2.
constexpr int8_t DRIVE_RX    = 16; // RO
constexpr int8_t DRIVE_TX    = 17; // DI
constexpr int8_t DRIVE_DE_RE = 4;  // driver enable / receiver enable
// Drive relay "ready to run" (P06.03/P06.04 = 12, contact A-C): closed when
// the safety circuit is closed and the drive has no fault. Opens on the e-stop,
// the door, STO (40), a drive fault, or the RS-485 timeout (CE, 18).
// Input-only pin, no internal pull resistor: the wiring sets the levels.
constexpr int8_t DRIVE_READY = 35;
// Level of DRIVE_READY when the drive is ready (relay closed). The board pulls
// the pin up and the closed relay shorts it to GND -> LOW = ready, and a cut
// wire reads HIGH = not ready (fails safe).
constexpr uint8_t DRIVE_READY_LEVEL = LOW; // HIGH or LOW

// MLX90614 IR thermometer (GY-906 board, with its own I2C pull-ups).
// GPIO 0 is a boot strapping pin: if the sensor ever holds SCL low while the
// ESP resets, the ESP starts in flash mode instead of running this firmware.
constexpr int8_t TEMPERATURE_SDA = 15;
constexpr int8_t TEMPERATURE_SCL = 0;

} // namespace Pins

// --- Links -------------------------------------------------------------------
namespace Config {

constexpr uint32_t GUI_BAUD   = 115200;
constexpr uint32_t DRIVE_BAUD = 9600;   // Modbus RTU, 8E1 (drive manual chapter 9)

// Main loop: one state machine update every 2 ms (500 Hz). It takes the
// newest sensor sample each time, so it never sends the same sample twice.
constexpr uint32_t LOOP_PERIOD_MS = 2;

// Heartbeat, from the main loop, in every state:
//   - ESP_live_data to the GUI (position, stroke min/max, force, temperature);
//   - a keep-alive to the drive whenever nothing else was sent to it for this
//     long. The drive's RS-485 timeout (P14.04) is set just above it, so if
//     the main loop or the cable dies the drive faults (CE) and coasts to a
//     stop (P14.05 = 0). Like any drive fault, only the reset button on the
//     machine clears it. GUI_force_reboot turns the timeout off first, so a
//     requested reboot doesn't leave the drive in CE.
constexpr uint32_t HEARTBEAT_PERIOD_MS = 200;
// P14.04, in 0.1 s: the heartbeat period plus up to 100 ms of slack (the drive
// link spaces frames 100 ms apart), rounded up -> 0.3 s for 200 ms. A single
// lost frame then trips the drive; raise the slack if CE faults show up.
constexpr uint16_t DRIVE_COMM_TIMEOUT_DECISECONDS = (HEARTBEAT_PERIOD_MS + 100 + 99) / 100;
static_assert(DRIVE_COMM_TIMEOUT_DECISECONDS * 100 > HEARTBEAT_PERIOD_MS,
              "the drive timeout must be longer than the heartbeat");

// --- ADC (ADS1256 #1) ----------------------------------------------------------
// Values from the single-board test (adc_test.ino, SINGLE_BOARD mode).
constexpr uint8_t ADC_STATUS = 0x00;   // MSB first, auto-calibration off, input buffer off
                                       // (the buffer clipped the load cell signal on these boards)
constexpr uint8_t ADC_DRATE  = 0xB0;   // 2000 SPS -> ~650 force/position pairs per second.
                                       // 0xA1 = 1000 SPS (~380 pairs/s, less than the loop rate),
                                       // 0xC0 = 3750 SPS (~1000 pairs/s, noisier)
constexpr uint8_t ADC_MUX_FORCE      = 0x01; // AIN0(+) / AIN1(-)
constexpr uint8_t ADC_ADCON_FORCE    = 0x06; // PGA 64, clock output off
constexpr uint8_t ADC_MUX_POSITION   = 0x23; // AIN2(+) / AIN3(-)
constexpr uint8_t ADC_ADCON_POSITION = 0x00; // PGA 1, clock output off
constexpr uint32_t ADC_SPI_HZ = 1000000;

// A conversion that doesn't finish within this time counts as failed. After
// ADC_MAX_FAILURES failures in a row the ADC is restarted from scratch.
constexpr uint32_t ADC_CONVERSION_TIMEOUT_MS = 10;
constexpr uint32_t ADC_MAX_FAILURES = 20;
constexpr uint32_t ADC_RETRY_MS = 2000;  // wait between two start-up attempts

// --- Load cell sign ---------------------------------------------------------------
// true: the force is negated as soon as it is read, so compressing the shock
// gives a positive force. Everything after the ADC (failsafe, TAR, live data,
// points sent to the GUI) sees the flipped value. Same as swapping the load
// cell's signal wires; set it back to false if they ever get swapped.
constexpr bool INVERT_FORCE = true;

// --- Sensor scale (only used in messages; everything on the wire is raw counts) --
constexpr float FORCE_NEWTON_PER_COUNT = 0.004575f; // load cell, PGA 64 (GUI uses the same value)

// --- Temperature (MLX90614, I2C) ---------------------------------------------------
constexpr uint32_t TEMPERATURE_I2C_HZ = 50000;        // sensor allows 100 kHz; margin for long wires
constexpr uint16_t TEMPERATURE_I2C_TIMEOUT_MS = 20;   // one read takes ~1 ms; a stuck bus gives up after this
constexpr uint32_t TEMPERATURE_PERIOD_MS = HEARTBEAT_PERIOD_MS; // one read per live data message
constexpr uint32_t TEMPERATURE_STALE_MS = 1000;       // no valid read for this long -> 0 (unknown)
constexpr uint32_t TEMPERATURE_RECOVERY_FAILURES = 5; // failed reads in a row -> restart the I2C driver

// --- Safety ----------------------------------------------------------------------
// Force failsafe: stops the motor (Panic) when the raw load cell reading leaves
// the window [min, max]. The window protects the load cell itself, so it is
// centred on the load cell's own calibration (the GUI's option page), never on
// the TAR: a TAR run with a shock mounted must not move it. The GUI sends the
// window with GUI_set_failsafe on every connection; until then, this default
// (centred on 0 counts) is used.
constexpr bool FORCE_FAILSAFE_ENABLED = true;
constexpr int32_t DEFAULT_FORCE_LIMIT_COUNTS = (int32_t)(3000.0f / FORCE_NEWTON_PER_COUNT); // ~3000 N
constexpr int32_t DEFAULT_FORCE_MIN_COUNTS = -DEFAULT_FORCE_LIMIT_COUNTS;
constexpr int32_t DEFAULT_FORCE_MAX_COUNTS = DEFAULT_FORCE_LIMIT_COUNTS;

// The safety checks work on a median of the last 5 samples, so a single bad
// reading (or two in a row) can't trigger them. On top of that, the limit
// must be exceeded on this many samples in a row.
constexpr uint32_t SAFETY_CONSECUTIVE_SAMPLES = 3;

// A reading this close to the ends of the 24-bit range means the ADC input is
// saturated (overload, or a sensor wire is off).
constexpr int32_t ADC_RAIL_MARGIN = 16;

// No new sensor sample for this long while the motor is driven -> Panic.
constexpr uint32_t SENSOR_STALL_MS = 100;

// --- Safety circuit (Pins::DRIVE_READY) --------------------------------------------
// Only the yellow reset button on the machine re-arms the drive: the ESP never
// sends a fault reset. Relay open this long -> Panic (filters a relay glitch).
constexpr uint32_t SAFETY_TRIP_MS = 10;
// Relay closed this long after the reset button -> a Panic caused by the safety
// circuit clears by itself (relay bounce, and the drive settling).
constexpr uint32_t SAFETY_CLEAR_MS = 200;

// --- Motor drive -------------------------------------------------------------------
// While waiting for a speed, the drive is asked for its running frequency this
// often; the speed counts as reached within the tolerance.
constexpr uint32_t DRIVE_QUERY_PERIOD_MS = 200;
constexpr int32_t SPEED_TOLERANCE_CENTIHZ = 50;   // 0.50 Hz
// If the drive never answers the query, assume it got there after this long.
constexpr uint32_t SPEEDUP_ASSUMED_MS = 3000;
// If it answers but never gets there, Panic after this long.
constexpr uint32_t SPEEDUP_TIMEOUT_MS = 10000;

// --- Stroke search (TAR and run calibration) -----------------------------------------
// The motor turns slowly while the position min/max are recorded. The stroke
// is found once the range is wide enough and has stopped growing.
constexpr int32_t STROKE_MIN_RANGE_COUNTS = 20000;  // TUNE: ~1 mm on the LVIT (~19,000 counts/mm)
constexpr uint32_t STROKE_MIN_TIME_MS = 3000;       // look for at least this long
constexpr uint32_t STROKE_SETTLE_MS = 2000;         // range unchanged (within 1%) this long = found
constexpr uint32_t STROKE_TIMEOUT_MS = 20000;       // no stroke after this long -> Panic

// --- Run calibration (GUI_calibrate while Armed) -----------------------------------
// The shock was just installed, so the yoke starts near the bottom:
//   1. motor stopped: the bottom force (settle, then average);
//   2. a little over one cycle at CALIBRATION_CENTIHZ: the stroke, from where
//      the yoke turns back at the top and at the bottom;
//   3. on the way up, past CALIBRATION_SLOW_PERMILLE: down to CALIBRATION_TOP_CENTIHZ;
//   4. the top force: averaged while the yoke goes through the top
//      (CALIBRATION_TOP_WINDOW_PERMILLE), then the motor stops.
constexpr uint32_t CALIBRATION_SETTLE_MS = 500;      // motor stopped, before the bottom force
constexpr uint32_t CALIBRATION_REST_SAMPLES = 500;   // bottom force averaged over ~1 s
constexpr uint16_t CALIBRATION_CENTIHZ = 200;        // TUNE: 2 Hz while finding the stroke
constexpr uint16_t CALIBRATION_TOP_CENTIHZ = 100;    // TUNE: 1 Hz through the top
constexpr int32_t CALIBRATION_SLOW_PERMILLE = 800;   // in 0.1 % of the stroke towards the top
// TUNE: the top force is averaged within this distance of the top (0.1 % of
// the stroke). Wider = more samples (less noise) but further from the end.
constexpr int32_t CALIBRATION_TOP_WINDOW_PERMILLE = 20;
// A turn back at an end counts once the position has come back by this much,
// so noise can't make one (~0.5 mm on the LVIT).
constexpr int32_t STROKE_TURN_HYSTERESIS_COUNTS = STROKE_MIN_RANGE_COUNTS / 2;
constexpr uint32_t CALIBRATION_TIMEOUT_MS = 60000;   // TUNE: whole motion, from the start to the top force

// --- Run --------------------------------------------------------------------------
// Cycle counting thresholds, in % of the stroke: a cycle is the position going
// above the high one and then back below the low one.
constexpr int32_t CYCLE_LOW_PERCENT  = 30;
constexpr int32_t CYCLE_HIGH_PERCENT = 70;
// While counting, Panic if no cycle completes for this long.
constexpr uint32_t CYCLE_TIMEOUT_MS = 10000;
// Armed (run accepted) but the run never started: back to Idle after this long.
constexpr uint32_t ARMED_TIMEOUT_MS = 30000;

// --- TAR / bottom out (GUI_TAR_request, GUI_bottom_out, end of a run) ---------------------
// TAR: empty load (motor stopped), then the stroke at TAR_CENTIHZ, then the
// bottom out. The bottom out (also on its own, and after every run) brings the
// motor to TAR_CENTIHZ, then stops it when the yoke passes TAR_STOP_PERMILLE
// on its way down.
constexpr uint32_t TAR_SETTLE_MS = 500;           // motor stopped, wait before measuring the empty load
constexpr uint32_t TAR_EMPTY_LOAD_SAMPLES = 500;  // empty load averaged over ~1 s
constexpr uint16_t TAR_CENTIHZ = 500;             // TUNE: 5 Hz, slow so the stop lands close to the target
// Which end of the stroke is the bottom: true if the LVIT reads its highest
// value at the bottom. TUNE: check the direction on the machine.
constexpr bool BOTTOM_AT_HIGH_POSITION = false;
// TUNE: the stop command is sent when the yoke, moving towards the bottom,
// passes this point (in 0.1 % of the stroke, 1000 = bottom). The motor still
// turns a little while it decelerates; the TAR report gives where it really
// stopped, use it to adjust this.
constexpr int32_t TAR_STOP_PERMILLE = 800;
constexpr uint32_t TAR_SEEK_TIMEOUT_MS = 10000;   // never passed the stop point -> Panic
constexpr uint32_t TAR_STOPPED_SETTLE_MS = 500;   // after the drive reports 0 Hz, before reading the position

} // namespace Config
