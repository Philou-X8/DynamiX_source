// PMC_Dyno.ino -- shock dyno firmware (ESP32)
//
// Who does what:
//   sensor_reader  task (core 0): ADS1256 -> newest force/position pair
//   temperature_reader task (core 0, lowest priority): MLX90614 over I2C ->
//                  newest temperature, every 200 ms
//   gui_link       tasks (core 0): USB serial to the GUI, both directions
//   drive_link     task (core 0): RS-485 Modbus to the TD350 motor drive
//   loop()         (core 1, every 2 ms): GUI commands, drive answers and the
//                  newest sensor pair go to the state machine, which decides
//                  everything (state_machine.h).
//
// Pins and tunable values: config.h. Wire format: protocol.h and
// command_flags.h (mirrored in the GUI's SerialDecoder.cs).
// Logic tests that run on a PC: host_tests/.

#include <Arduino.h>
#include <freertos/FreeRTOS.h>
#include <freertos/task.h>
#include <esp_system.h>

#include "boot_report.h"
#include "command_flags.h"
#include "config.h"
#include "data_buffer.h"
#include "drive_link.h"
#include "gui_link.h"
#include "motor_drive.h"
#include "sensor_reader.h"
#include "temperature_reader.h"
#include "state_machine.h"

static_assert(configTICK_RATE_HZ >= 1000, "the 2 ms main loop needs a 1 kHz FreeRTOS tick");

namespace {

// Every task but the main loop runs on core 0; loop() runs on core 1.
constexpr int TASK_CORE = 0;
constexpr unsigned LINK_TASK_PRIORITY = 2;
constexpr unsigned SENSOR_TASK_PRIORITY = 5; // above the links: woken by DRDY, must read right away
constexpr unsigned TEMPERATURE_TASK_PRIORITY = 1; // below everything: nothing waits for it
constexpr uint32_t REBOOT_DELAY_MS = 300;    // lets the stop frames and the message go out
constexpr uint32_t LATE_REPORT_PERIOD_MS = 1000;

PointBuffer g_points;
GuiLink g_gui;
DriveLink g_driveLink;
MotorDrive g_drive;
SensorReader g_sensors;
TemperatureReader g_temperature;
StateMachine g_stateMachine;

TickType_t g_lastWake = 0;
uint32_t g_lastSequence = 0;   // sequence number of the last sample handed to the state machine
uint32_t g_lateTicks = 0;      // loop iterations that started late since the last report
uint32_t g_lastLateReportMs = 0;

// Once a second at most, so a slow patch can't flood the log.
void reportLateTicks(uint32_t nowMs) {
    if (g_lateTicks == 0 || nowMs - g_lastLateReportMs < LATE_REPORT_PERIOD_MS) {
        return;
    }
    g_gui.sendf(Cmd::ESP_debug, "Main loop late %lu times", (unsigned long)g_lateTicks);
    g_lateTicks = 0;
    g_lastLateReportMs = nowMs;
}

} // namespace

void setup() {
    g_points.begin();
    g_gui.begin(&g_points, Config::GUI_BAUD, TASK_CORE, LINK_TASK_PRIORITY);
    reportBootReason(g_gui);

    g_driveLink.begin(Pins::DRIVE_RX, Pins::DRIVE_TX, Pins::DRIVE_DE_RE, Config::DRIVE_BAUD, TASK_CORE,
                      LINK_TASK_PRIORITY);
    g_drive.begin(&g_driveLink);
    g_drive.stop(); // if the ESP restarted in the middle of a run, the drive is still turning

    g_sensors.begin(&g_gui, TASK_CORE, SENSOR_TASK_PRIORITY);
    g_temperature.begin(TASK_CORE, TEMPERATURE_TASK_PRIORITY);
    g_stateMachine.begin(&g_gui, &g_drive, &g_points);
    g_stateMachine.prepareDrive(); // RS-485 timeout on
    pinMode(Pins::DRIVE_READY, INPUT); // input-only pin, pulled by the wiring
    g_lastWake = xTaskGetTickCount();
}

void loop() {
    // One iteration every 2 ms. After a late one, start counting from now
    // instead of running several back to back to catch up.
    if (xTaskDelayUntil(&g_lastWake, pdMS_TO_TICKS(Config::LOOP_PERIOD_MS)) == pdFALSE) {
        g_lateTicks++;
        g_lastWake = xTaskGetTickCount();
    }
    const uint32_t nowMs = millis();

    g_stateMachine.pollDrive();
    // Before the commands, so a run request sees an opened circuit.
    g_stateMachine.onSafetyInput(digitalRead(Pins::DRIVE_READY) == Pins::DRIVE_READY_LEVEL, nowMs);

    Command command;
    while (g_gui.popCommand(command)) {
        g_stateMachine.handleCommand(command, nowMs);
    }
    if (g_stateMachine.rebootRequested()) {
        vTaskDelay(pdMS_TO_TICKS(REBOOT_DELAY_MS));
        esp_restart();
    }

    // Only a new pair: the ADC makes ~650 per second, this loop runs 500 times.
    SensorSample sample;
    if (g_sensors.latest(sample) && sample.sequence != g_lastSequence) {
        g_lastSequence = sample.sequence;
        g_stateMachine.onSample(sample, nowMs);
    }

    g_stateMachine.setTemperature(g_temperature.centiCelsius());
    g_stateMachine.tick(nowMs);
    // Last, so it only sends when nothing else went to the drive. Here rather
    // than in a task of its own: if this loop stops, so does the keep-alive,
    // and the drive stops the motor on its RS-485 timeout.
    g_drive.keepAlive(nowMs, Config::HEARTBEAT_PERIOD_MS);
    reportLateTicks(nowMs);
}
