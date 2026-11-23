// DynamiX_Simulator.dll: the shock dyno's ESP32 firmware running on the PC, with a simulated machine.
//
// What is the firmware's own, unmodified (compiled from ../DynamiX_Firmware):
//   state_machine.cpp, motor_drive.cpp, drive_protocol.cpp, protocol.cpp
// What is replaced here, because it touches the hardware:
//   GuiLink (USB serial)   -> byte queues the HMI reads and writes through the C functions below
//   DriveLink (RS-485)     -> the simulated drive, answering Modbus frames
//   PointBuffer            -> the same ring without the FreeRTOS lock
//   sensors, relay, IR     -> the simulated machine (Plant)
// main() of the .ino (loop(), every 2 ms) is repeated in tick().
//
// Threads: one worker runs the loop in real time. The C functions are called by the HMI's threads; everything
// shared is behind g_lock.
//
// The numbers of the machine (stroke, forces, sensor scales) are at the top of the Plant section. The sensor
// scales match the HMI's default calibration, so the HMI shows sensible mm and N without calibrating.

#include <algorithm>
#include <atomic>
#include <chrono>
#include <cmath>
#include <cstdint>
#include <cstring>
#include <deque>
#include <memory>
#include <mutex>
#include <thread>
#include <vector>

#define NOMINMAX
#define WIN32_LEAN_AND_MEAN
#include <windows.h>
#include <mmsystem.h>
#pragma comment(lib, "winmm.lib")

#include "../DynamiX_Firmware/bytes.h"
#include "../DynamiX_Firmware/command_flags.h"
#include "../DynamiX_Firmware/config.h"
#include "../DynamiX_Firmware/data_buffer.h"
#include "../DynamiX_Firmware/drive_link.h"
#include "../DynamiX_Firmware/drive_protocol.h"
#include "../DynamiX_Firmware/gui_link.h"
#include "../DynamiX_Firmware/motor_drive.h"
#include "../DynamiX_Firmware/state_machine.h"

namespace {

constexpr double PI = 3.14159265358979323846;

// ======================================================================================
// Shared state
// ======================================================================================

std::mutex g_lock;
std::deque<uint8_t> g_inbound;                      // bytes written by the HMI
std::deque<uint8_t> g_outbound;                     // bytes for the HMI to read
std::deque<std::vector<uint8_t>> g_outFrames;       // command frames waiting to be sent (GuiLink's out queue)
std::deque<Command> g_commands;                     // decoded commands (GuiLink's command queue)
FrameDecoder g_decoder;
uint32_t g_nowMs = 0;                               // the firmware's millis()
constexpr size_t OUTBOUND_LIMIT = 4u * 1024 * 1024; // nobody reads: stop growing

void emit(const uint8_t* bytes, size_t length) {
    if (g_outbound.size() + length > OUTBOUND_LIMIT) {
        g_outbound.clear();
    }
    g_outbound.insert(g_outbound.end(), bytes, bytes + length);
}

struct Reply {
    uint32_t readyMs;
    DriveFrame frame;
};
std::deque<Reply> g_replies;                        // the drive's answers, with a little RS-485 latency

// ======================================================================================
// The machine: drive, scotch yoke, shock, load cell, position sensor, IR thermometer
// ======================================================================================

// Sensor scales: the HMI's default calibration (AppSettings). Counts are what the firmware reports, after
// the load cell sign flip (INVERT_FORCE), so force counts rise with compression.
constexpr double FORCE_ZERO_COUNTS = -18469.0;
constexpr double FORCE_N_PER_COUNT = 0.0050718;
constexpr double POSITION_ZERO_COUNTS = 767909.0;
constexpr double POSITION_MM_PER_COUNT = 0.000056267;

// Mechanics
constexpr double STROKE_MM = 25.0;          // bottom to top
constexpr double BOTTOM_MM = 20.0;          // where the bottom is in the sensor's frame (low position = bottom)
constexpr double DRIVE_TO_YOKE = 10.0;      // drive Hz per yoke revolution per second (2:1 gearbox, 5:1 belt)
constexpr double DRIVE_RAMP_HZ_PER_S = 15.0;

// Load cell
constexpr double FIXTURE_N = 13.4;          // what the load cell reads with nothing mounted (the TAR measures it)

// The shock: static force (preload, then a gas spring) and damping that differs in compression and rebound
constexpr double PRELOAD_N = 100.0;
constexpr double GAS_N_PER_MM = 4.0;
constexpr double COMPRESSION_LINEAR = 1.8;    // N per mm/s
constexpr double COMPRESSION_QUADRATIC = 0.0025; // N per (mm/s)^2
constexpr double REBOUND_LINEAR = 3.5;
constexpr double REBOUND_QUADRATIC = 0.0008;
// A small detail locked to the position, so the graphs have something fine to show
constexpr double SAWTOOTH_N = 2.0;
constexpr double SAWTOOTH_MM = 1.25;

// Noise
constexpr double FORCE_NOISE_N = 0.3;
constexpr double POSITION_NOISE_MM = 0.002;

// Temperature of the damper: heated by the damping power, cooling towards the room
constexpr double AMBIENT_C = 22.0;
constexpr double HEAT_CAPACITY_J_PER_K = 800.0;
constexpr double COOLING_TAU_S = 180.0;

struct Plant {
    bool running = false;
    double setHz = 0;
    double hz = 0;                  // the drive's real output frequency
    double revolutions = 0.03;      // yoke turns; 0 = bottom of the stroke
    bool shockMounted = false;
    bool autoMount = true;          // the shock is taken off for a TAR and put back after it
    double tempC = AMBIENT_C;
    uint32_t noise = 12345;

    double yokeHz() const { return hz / DRIVE_TO_YOKE; }
    double xMm() const { return STROKE_MM / 2 * (1 - std::cos(2 * PI * revolutions)); }  // above the bottom
    double vMmS() const { return STROKE_MM / 2 * 2 * PI * yokeHz() * std::sin(2 * PI * revolutions); } // + = up = compression

    double random() {   // -1 .. 1
        noise = noise * 1103515245u + 12345u;
        return ((noise >> 8) % 20001) / 10000.0 - 1.0;
    }

    // The damper's force at speed v (mm/s, + = compression): harder in rebound than in compression
    double damping(double v) const {
        return v >= 0 ? COMPRESSION_LINEAR * v + COMPRESSION_QUADRATIC * v * v
                      : REBOUND_LINEAR * v - REBOUND_QUADRATIC * v * v;
    }

    double forceN() const {
        double f = FIXTURE_N;
        if (shockMounted) {
            const double x = xMm();
            f += PRELOAD_N + GAS_N_PER_MM * x;
            f += damping(vMmS());
            f += SAWTOOTH_N * (std::fmod(x, SAWTOOTH_MM) / SAWTOOTH_MM - 0.5);
        }
        return f;
    }

    void step(double dt) {
        const double target = running ? setHz : 0.0;
        const double dv = DRIVE_RAMP_HZ_PER_S * dt;
        hz = std::fabs(target - hz) <= dv ? target : hz + (target > hz ? dv : -dv);
        revolutions += yokeHz() * dt;
        if (revolutions > 1e6) {
            revolutions = std::fmod(revolutions, 1.0);
        }
        const double power = shockMounted ? std::fabs(damping(vMmS()) * vMmS()) / 1000.0 : 0.0; // W
        tempC += (power / HEAT_CAPACITY_J_PER_K - (tempC - AMBIENT_C) / COOLING_TAU_S) * dt;
    }

    // What the firmware reads from the ADC for the current instant
    SensorSample sample(uint32_t nowMs, uint32_t sequence) {
        const double posMm = BOTTOM_MM + xMm() + POSITION_NOISE_MM * random();
        const double forceNow = forceN() + FORCE_NOISE_N * random();
        SensorSample s{};
        s.position = clampInt24((int32_t)std::lround(POSITION_ZERO_COUNTS + posMm / POSITION_MM_PER_COUNT));
        s.force = clampInt24((int32_t)std::lround(FORCE_ZERO_COUNTS + forceNow / FORCE_N_PER_COUNT));
        s.timeUs = nowMs * 1000u;
        s.sequence = sequence;
        return s;
    }

    // The drive: Modbus requests in, answers out (the same ones the TD350 gives)
    void onDriveFrame(const uint8_t* f, size_t length) {
        if (length < 8) {
            return;
        }
        const uint16_t reg = readUint16BE(f + 2);
        const uint16_t value = readUint16BE(f + 4);
        Reply r{};
        r.readyMs = g_nowMs + 12; // ~9 ms for the frame at 9600 baud, plus the drive's turnaround
        if (f[1] == MODBUS_WRITE_REGISTER) {
            if (reg == DRIVE_REG_SET_FREQUENCY_RAM || reg == DRIVE_REG_SET_FREQUENCY) {
                setHz = value / 100.0;
            }
            if (reg == DRIVE_REG_CONTROL && value == DRIVE_CONTROL_FORWARD) {
                running = true;
            }
            if (reg == DRIVE_REG_CONTROL && value == DRIVE_CONTROL_STOP) {
                running = false;
            }
            std::memcpy(r.frame.bytes, f, 8); // a write is answered with an echo
            r.frame.length = 8;
        } else if (f[1] == MODBUS_READ_REGISTERS && reg == DRIVE_REG_RUNNING_FREQUENCY) {
            r.frame.bytes[0] = DRIVE_ADDRESS;
            r.frame.bytes[1] = MODBUS_READ_REGISTERS;
            r.frame.bytes[2] = 2;
            writeUint16BE((uint16_t)std::lround(hz * 100), r.frame.bytes + 3);
            const uint16_t crc = modbusCrc16(r.frame.bytes, 5);
            r.frame.bytes[5] = crc & 0xFF;
            r.frame.bytes[6] = crc >> 8;
            r.frame.length = 7;
        } else {
            return; // nothing else is simulated: no answer
        }
        g_replies.push_back(r);
    }
};

Plant g_plant;

// ======================================================================================
// The firmware's hardware layer, replaced
// ======================================================================================

} // namespace

// --- PointBuffer: the ring of the real one, without the lock (one thread runs the firmware) -------------
void PointBuffer::begin() {}

bool PointBuffer::push(const Point& p) {
    if (count_ >= CAPACITY) {
        dropped_ = dropped_ + 1;
        return false;
    }
    points_[(head_ + count_) % CAPACITY] = p;
    count_++;
    return true;
}

size_t PointBuffer::take(Point* out, bool partial) {
    size_t n = count_ >= MAX_POINTS_PER_FRAME ? MAX_POINTS_PER_FRAME : (partial ? count_ : 0);
    for (size_t i = 0; i < n; i++) {
        out[i] = points_[(head_ + i) % CAPACITY];
    }
    head_ = (head_ + n) % CAPACITY;
    count_ -= n;
    return n;
}

void PointBuffer::reset() {
    head_ = 0;
    count_ = 0;
    dropped_ = 0;
}

// --- GuiLink: byte queues instead of the USB serial and its tasks ---------------------------------------
// popCommand() is called every loop: it also moves the outgoing frames and points to the byte queue and
// decodes what the HMI wrote, which the two tasks do on the real ESP32.
void GuiLink::begin(PointBuffer* points, uint32_t, int, unsigned) {
    points_ = points;
    outQueue_ = &g_outFrames;
    commandQueue_ = &g_commands;
}

bool GuiLink::send(uint8_t flag, const uint8_t* payload, size_t size) {
    uint8_t frame[MAX_FRAME_SIZE];
    const size_t length = encodeCommandFrame(flag, payload, size, frame, sizeof(frame));
    if (length == 0) {
        return false;
    }
    g_outFrames.emplace_back(frame, frame + length);
    return true;
}

void GuiLink::flushPoints(uint8_t thenFlag) {
    afterFlushFlag_ = thenFlag;
    flushRequested_ = true;
}

bool GuiLink::popCommand(Command& out) {
    sendLoop();
    recvLoop();
    if (g_commands.empty()) {
        return false;
    }
    out = g_commands.front();
    g_commands.pop_front();
    return true;
}

void GuiLink::sendTaskFn(void*) {}
void GuiLink::recvTaskFn(void*) {}

// The send task's loop, for as long as there is something to send: commands first, then points.
void GuiLink::sendLoop() {
    Point points[MAX_POINTS_PER_FRAME];
    uint8_t frame[MAX_FRAME_SIZE];
    for (;;) {
        if (!g_outFrames.empty()) {
            emit(g_outFrames.front().data(), g_outFrames.front().size());
            g_outFrames.pop_front();
            continue;
        }
        const bool flushing = flushRequested_;
        const size_t count = points_->take(points, flushing);
        if (count > 0) {
            const size_t length = encodeDataFrame(points, count, frame, sizeof(frame));
            emit(frame, length);
            continue;
        }
        if (flushing) {
            flushRequested_ = false;
            const uint8_t flag = afterFlushFlag_;
            afterFlushFlag_ = 0;
            if (flag != 0) {
                const size_t length = encodeCommandFrame(flag, nullptr, 0, frame, sizeof(frame));
                emit(frame, length);
            }
            continue;
        }
        break;
    }
}

// The receive task: what the HMI wrote, cut into commands.
void GuiLink::recvLoop() {
    Command command;
    while (!g_inbound.empty()) {
        const uint8_t byte = g_inbound.front();
        g_inbound.pop_front();
        if (g_decoder.feed(byte, command)) {
            if (g_commands.size() >= 40) {
                sendf(Cmd::ESP_error, "Command %u dropped: too many waiting", (unsigned)command.flag);
            } else {
                g_commands.push_back(command);
            }
        }
    }
}

// --- DriveLink: the simulated drive ---------------------------------------------------------------------
void DriveLink::begin(int8_t, int8_t, int8_t, uint32_t, int, unsigned) {}

bool DriveLink::sendFrame(const uint8_t* frame, size_t length, bool) {
    if (length < 4 || length > DRIVE_MAX_FRAME || !modbusFrameCrcOk(frame, length)) {
        return false;
    }
    g_plant.onDriveFrame(frame, length);
    return true;
}

bool DriveLink::popResponse(DriveFrame& out) {
    if (g_replies.empty() || g_replies.front().readyMs > g_nowMs) {
        return false;
    }
    out = g_replies.front().frame;
    g_replies.pop_front();
    return true;
}

void DriveLink::taskFn(void*) {}
void DriveLink::run() {}

namespace {

// ======================================================================================
// The firmware's main loop
// ======================================================================================

constexpr uint32_t LOOP_MS = Config::LOOP_PERIOD_MS;
constexpr uint32_t REBOOT_DELAY_MS = 300;

struct Firmware {
    PointBuffer points;
    GuiLink gui;
    DriveLink driveLink;
    MotorDrive drive;
    StateMachine sm;
    uint32_t sequence = 0;

    Firmware() {
        points.begin();
        gui.begin(&points, Config::GUI_BAUD, 0, 0);
        drive.begin(&driveLink);
        sm.begin(&gui, &drive, &points);
        gui.sendf(Cmd::ESP_message, "BOOT: SIMULATOR");
        sm.prepareDrive(); // RS-485 timeout on
    }
};

std::unique_ptr<Firmware> g_fw;
uint32_t g_rebootAtMs = 0;
State g_lastState = State::Idle;

void clearLinks() {
    g_inbound.clear();
    g_outFrames.clear();
    g_commands.clear();
    g_replies.clear();
    g_decoder.reset();
}

// One iteration of loop() in DynamiX_Firmware.ino
void tick() {
    g_nowMs += LOOP_MS;
    const uint32_t nowMs = g_nowMs;
    g_plant.step(LOOP_MS / 1000.0);

    Firmware& fw = *g_fw;
    fw.sm.pollDrive();
    fw.sm.onSafetyInput(true, nowMs); // the safety circuit is always closed

    Command command;
    while (fw.gui.popCommand(command)) {
        fw.sm.handleCommand(command, nowMs);
    }
    if (fw.sm.rebootRequested() && g_rebootAtMs == 0) {
        g_rebootAtMs = nowMs + REBOOT_DELAY_MS;
    }

    fw.sm.onSample(g_plant.sample(nowMs, ++fw.sequence), nowMs);

    fw.sm.setTemperature((int32_t)std::lround(g_plant.tempC * 100.0));
    fw.sm.tick(nowMs);
    fw.drive.keepAlive(nowMs, Config::HEARTBEAT_PERIOD_MS);

    // The shock is taken off for a TAR (nothing mounted: the empty load) and put back after it
    const State state = fw.sm.state();
    if (g_plant.autoMount && state != g_lastState) {
        if (state == State::Tar) {
            g_plant.shockMounted = false;
        } else if (g_lastState == State::Tar) {
            g_plant.shockMounted = true;
        }
    }
    g_lastState = state;

    if (g_rebootAtMs != 0 && nowMs >= g_rebootAtMs) {
        g_rebootAtMs = 0;
        clearLinks();
        g_fw = std::make_unique<Firmware>(); // the ESP32 restarts: new state, the machine goes on turning
        g_lastState = State::Idle;
    }
}

// ======================================================================================
// The worker thread
// ======================================================================================

std::thread g_thread;
std::atomic<bool> g_running{false};
std::atomic<double> g_speed{1.0};

void worker() {
    timeBeginPeriod(1);
    auto last = std::chrono::steady_clock::now();
    double budgetMs = 0;
    while (g_running) {
        const auto now = std::chrono::steady_clock::now();
        budgetMs += std::chrono::duration<double, std::milli>(now - last).count() * g_speed;
        last = now;
        budgetMs = std::min(budgetMs, 200.0); // after a stall: don't try to catch up with all of it
        {
            std::lock_guard<std::mutex> guard(g_lock);
            for (int i = 0; budgetMs >= LOOP_MS && i < 200; i++) {
                tick();
                budgetMs -= LOOP_MS;
            }
        }
        std::this_thread::sleep_for(std::chrono::milliseconds(1));
    }
    timeEndPeriod(1);
}

void stopWorker() {
    g_running = false;
    if (g_thread.joinable()) {
        g_thread.join();
    }
}

} // namespace

// ======================================================================================
// The C interface the HMI uses
// ======================================================================================

extern "C" {

// Starts the simulated ESP32 and machine (a new one if one was running). Returns 1 on success.
__declspec(dllexport) int dynamix_sim_open() {
    stopWorker();
    {
        std::lock_guard<std::mutex> guard(g_lock);
        g_plant = Plant();
        g_nowMs = 1000;
        g_rebootAtMs = 0;
        g_lastState = State::Idle;
        clearLinks();
        g_outbound.clear();
        g_fw = std::make_unique<Firmware>();
    }
    g_running = true;
    g_thread = std::thread(worker);
    return 1;
}

__declspec(dllexport) void dynamix_sim_close() {
    stopWorker();
    std::lock_guard<std::mutex> guard(g_lock);
    g_fw.reset();
    clearLinks();
    g_outbound.clear();
}

// Bytes from the HMI to the ESP32 (what it would write to the serial port). Returns how many were taken.
__declspec(dllexport) int dynamix_sim_write(const uint8_t* data, int length) {
    if (data == nullptr || length <= 0 || !g_running) {
        return 0;
    }
    std::lock_guard<std::mutex> guard(g_lock);
    g_inbound.insert(g_inbound.end(), data, data + length);
    return length;
}

// Bytes from the ESP32 to the HMI. Returns how many were copied (0 = nothing waiting).
__declspec(dllexport) int dynamix_sim_read(uint8_t* buffer, int capacity) {
    if (buffer == nullptr || capacity <= 0) {
        return 0;
    }
    std::lock_guard<std::mutex> guard(g_lock);
    const int n = (int)std::min<size_t>((size_t)capacity, g_outbound.size());
    for (int i = 0; i < n; i++) {
        buffer[i] = g_outbound.front();
        g_outbound.pop_front();
    }
    return n;
}

__declspec(dllexport) int dynamix_sim_available() {
    std::lock_guard<std::mutex> guard(g_lock);
    return (int)g_outbound.size();
}

// 1 = a shock is mounted on the machine, 0 = nothing mounted. By default the simulator takes the shock off
// during a TAR and puts it back after; this sets it by hand and turns that off.
__declspec(dllexport) void dynamix_sim_set_shock_mounted(int mounted) {
    std::lock_guard<std::mutex> guard(g_lock);
    g_plant.shockMounted = mounted != 0;
    g_plant.autoMount = false;
}

// Simulation speed against real time (1 = real time). Only for tests: the HMI's heartbeat runs on the real clock.
__declspec(dllexport) void dynamix_sim_set_speed(double factor) {
    g_speed = std::clamp(factor, 0.1, 20.0);
}

__declspec(dllexport) int dynamix_sim_version() {
    return 1;
}

} // extern "C"
