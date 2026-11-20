#pragma once
#include <stdint.h>

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
// Drive relay "ready to run" (P06 = 12): HIGH when the safety circuit is closed
// and the drive has no fault. Goes LOW on the e-stop, the door, STO (40), a
// drive fault, or the RS-485 timeout (CE, 18). Input-only pin, no internal
// pull resistor: the wiring must pull it LOW when the relay is open.
constexpr int8_t DRIVE_READY = 35;

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

// --- Sensor scale (only used in messages; everything on the wire is raw counts) --
constexpr float FORCE_NEWTON_PER_COUNT = 0.004575f; // load cell, PGA 64 (GUI uses the same value)

// --- Safety ----------------------------------------------------------------------
// Force failsafe: stops the motor (Panic) when the load cell reads more than
// the limit away from the empty load (or from 0 before a TAR). The GUI sends
// the limit with GUI_calibrate; until then this default is used.
constexpr bool FORCE_FAILSAFE_ENABLED = true;
constexpr int32_t DEFAULT_FORCE_LIMIT_COUNTS = (int32_t)(3000.0f / FORCE_NEWTON_PER_COUNT); // ~3000 N

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
// sends a fault reset. LOW this long -> Panic (filters a relay glitch).
constexpr uint32_t SAFETY_TRIP_MS = 10;
// HIGH this long after the reset button -> a Panic caused by the safety
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
constexpr uint16_t CALIBRATION_CENTIHZ = 1000;      // 10 Hz while looking for the stroke
constexpr uint32_t CALIBRATION_REST_SAMPLES = 250;  // force averaged at rest (~0.5 s)

// --- Run --------------------------------------------------------------------------
// Cycle counting thresholds, in % of the stroke: a cycle is the position going
// above the high one and then back below the low one.
constexpr int32_t CYCLE_LOW_PERCENT  = 30;
constexpr int32_t CYCLE_HIGH_PERCENT = 70;
// While counting, Panic if no cycle completes for this long.
constexpr uint32_t CYCLE_TIMEOUT_MS = 10000;
// Armed (run accepted) but the run never started: back to Idle after this long.
constexpr uint32_t ARMED_TIMEOUT_MS = 30000;

// --- TAR / bottom out (GUI_TAR_request, GUI_bottom_out) ---------------------------------
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
