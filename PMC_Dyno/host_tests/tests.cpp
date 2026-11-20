// Host tests: the firmware's logic compiled for the PC (run_tests.bat).
//
// The pure helpers are checked directly. The state machine runs unchanged
// against a simulated machine: a TD350 that ramps toward its set frequency and
// answers Modbus, a scotch yoke whose position follows the motor, and a load
// cell. Each test drives it the way the GUI does and checks what the ESP sends.
#include <cmath>
#include <cstdio>
#include <cstring>
#include <memory>
#include <string>
#include <vector>

#include "../bytes.h"
#include "../command_flags.h"
#include "../config.h"
#include "../cycle_counter.h"
#include "../drive_protocol.h"
#include "../gui_link.h"
#include "../data_buffer.h"
#include "../motor_drive.h"
#include "../protocol.h"
#include "../sequence_step.h"
#include "../signal_filters.h"
#include "../state_machine.h"
#include "fakes.h"

// --- Minimal test framework ---------------------------------------------------

static int g_checks = 0;
static int g_failures = 0;
static const char* g_test = "";

#define CHECK(cond) check((cond), #cond, __FILE__, __LINE__)
static void check(bool ok, const char* what, const char* file, int line) {
    g_checks++;
    if (!ok) {
        g_failures++;
        std::printf("  FAIL [%s] %s (%s:%d)\n", g_test, what, file, line);
    }
}

#define TEST(name) static void name(); static Registrar reg_##name(#name, name); static void name()
struct TestCase { const char* name; void (*fn)(); };
static std::vector<TestCase>& tests() { static std::vector<TestCase> t; return t; }
struct Registrar { Registrar(const char* n, void (*f)()) { tests().push_back({n, f}); } };

static std::vector<uint8_t> bytes(std::initializer_list<int> list) {
    std::vector<uint8_t> v;
    for (int b : list) v.push_back((uint8_t)b);
    return v;
}

// --- Pure helpers -------------------------------------------------------------

TEST(int24_round_trip) {
    uint8_t b[3];
    for (int32_t v : {0, 1, -1, 655737, -8388608, 8388607, 123456, -123456}) {
        writeInt24BE(v, b);
        CHECK(readInt24BE(b) == v);
    }
    CHECK(clampInt24(9000000) == INT24_MAX);
    CHECK(clampInt24(-9000000) == INT24_MIN);
}

// Same frames the old firmware had hard-coded (and tested on the drive).
TEST(modbus_frames_match_the_tested_ones) {
    uint8_t f[MODBUS_REQUEST_SIZE];
    buildWriteRequest(DRIVE_ADDRESS, DRIVE_REG_CONTROL, DRIVE_CONTROL_STOP, f);
    CHECK(std::vector<uint8_t>(f, f + 8) == bytes({0x03, 0x06, 0x20, 0x00, 0x00, 0x05, 0x43, 0xEB}));
    buildWriteRequest(DRIVE_ADDRESS, DRIVE_REG_CONTROL, DRIVE_CONTROL_FORWARD, f);
    CHECK(std::vector<uint8_t>(f, f + 8) == bytes({0x03, 0x06, 0x20, 0x00, 0x00, 0x01, 0x42, 0x28}));
    buildWriteRequest(DRIVE_ADDRESS, DRIVE_REG_SET_FREQUENCY_RAM, 0, f);
    CHECK(std::vector<uint8_t>(f, f + 8) == bytes({0x03, 0x06, 0x80, 0x0A, 0x00, 0x00, 0x81, 0xEA}));
    buildWriteRequest(DRIVE_ADDRESS, DRIVE_REG_SET_FREQUENCY_RAM, 1000, f);
    CHECK(std::vector<uint8_t>(f, f + 8) == bytes({0x03, 0x06, 0x80, 0x0A, 0x03, 0xE8, 0x81, 0x54}));
    buildReadRequest(DRIVE_ADDRESS, DRIVE_REG_RUNNING_FREQUENCY, 1, f);
    CHECK(std::vector<uint8_t>(f, f + 8) == bytes({0x03, 0x03, 0x30, 0x00, 0x00, 0x01, 0x8A, 0xE8}));
}

TEST(modbus_answers) {
    uint8_t answer[7] = {0x03, 0x03, 0x02, 0x03, 0xE8, 0, 0};
    const uint16_t crc = modbusCrc16(answer, 5);
    answer[5] = crc & 0xFF;
    answer[6] = crc >> 8;
    uint16_t value = 0;
    CHECK(parseReadResponseOneRegister(answer, 7, DRIVE_ADDRESS, value) && value == 1000);
    answer[4] ^= 1; // corrupted
    CHECK(!parseReadResponseOneRegister(answer, 7, DRIVE_ADDRESS, value));

    uint8_t error[5] = {0x03, 0x86, 0x03, 0, 0};
    const uint16_t crc2 = modbusCrc16(error, 3);
    error[3] = crc2 & 0xFF;
    error[4] = crc2 >> 8;
    uint8_t function = 0, code = 0;
    CHECK(parseExceptionResponse(error, 5, DRIVE_ADDRESS, function, code) && function == 0x06 && code == 0x03);
}

// GUI_send_sequence payload exactly as SequencePanel.MakeSequence builds it.
static std::vector<uint8_t> guiStep(uint8_t order, uint8_t cycles, double hz) {
    uint8_t frame[8];
    buildWriteRequest(DRIVE_ADDRESS, DRIVE_REG_SET_FREQUENCY_RAM, (uint16_t)std::lround(hz * 100.0), frame);
    std::vector<uint8_t> payload = {order, cycles};
    payload.insert(payload.end(), frame, frame + 8);
    return payload;
}

TEST(sequence_step_parsing) {
    auto p = guiStep(2, 5, 10.0);
    uint8_t order = 0;
    SequenceStep step{};
    CHECK(parseSequenceStep(p.data(), p.size(), order, step));
    CHECK(order == 2 && step.cycles == 5 && step.targetCentiHz == 1000);
    auto zeroCycles = guiStep(0, 0, 10.0);
    CHECK(!parseSequenceStep(zeroCycles.data(), zeroCycles.size(), order, step));
    auto badCrc = p;
    badCrc[9] ^= 0xFF;
    CHECK(!parseSequenceStep(badCrc.data(), badCrc.size(), order, step));
    CHECK(!parseSequenceStep(p.data(), 9, order, step));
}

TEST(frame_decoder_round_trip) {
    const uint8_t payload[3] = {1, 2, 3};
    uint8_t frame[MAX_FRAME_SIZE];
    const size_t n = encodeCommandFrame(Cmd::GUI_run_request, payload, 3, frame, sizeof(frame));
    CHECK(n == 8);
    FrameDecoder decoder;
    Command c{};
    bool got = false;
    // garbage first, then a data frame (skipped), then the command
    for (uint8_t b : bytes({0x12, 0xFF, 0xFF, 0x00, 0xAA, 0x02, 0x00, 0x00})) got = decoder.feed(b, c) || got;
    CHECK(!got);
    for (size_t i = 0; i < n; i++) got = decoder.feed(frame[i], c);
    CHECK(got && c.flag == Cmd::GUI_run_request && c.payloadSize == 3 && c.payload[2] == 3);

    Point points[2] = {{-1, 655737, 0x01020304}, {8388607, -8388608, 7}};
    const size_t dn = encodeDataFrame(points, 2, frame, sizeof(frame));
    CHECK(dn == 4 + 20);
    CHECK(frame[2] == TYPE_DATA && frame[3] == 20);
    CHECK(frame[4] == 0xFF && frame[5] == 0xFF && frame[6] == 0xFF); // -1
    CHECK(frame[10] == 0x01 && frame[13] == 0x04);                   // time
}

TEST(median5_rejects_up_to_two_bad_readings) {
    Median5 m;
    for (int i = 0; i < 5; i++) m.update(100);
    CHECK(m.update(8388607) == 100);
    CHECK(m.update(8388607) == 100);
    CHECK(m.update(100) == 100);
    Median5 m2;
    for (int i = 0; i < 5; i++) m2.update(100);
    m2.update(5000);
    m2.update(5000);
    CHECK(m2.update(5000) == 5000); // a real change shows on the 3rd sample
}

TEST(cycle_counter) {
    CycleCounter c;
    c.start(0, 1000, 30, 70);
    int cycles = 0;
    // start mid-stroke going up: the partial first cycle isn't counted
    for (int i = 0; i < 4000; i++) {
        const double pos = 500 + 500 * std::sin(i * 2 * 3.14159265 / 1000.0);
        if (c.update((int32_t)pos)) cycles++;
    }
    CHECK(cycles == 3); // 4 periods seen, the first one partial
}

TEST(stroke_finder) {
    StrokeFinder f;
    f.start(0, 20000, 3000, 2000, 20000);
    uint32_t t = 0;
    for (; t < 10000; t += 2) {
        f.update((int32_t)(400000 + 300000 * std::sin(t / 1000.0 * 2 * 3.14159265 * 0.5)), t);
        if (f.result(t) != StrokeFinder::Result::Searching) break;
    }
    CHECK(f.result(t) == StrokeFinder::Result::Found);
    CHECK(std::abs(f.min() - 100000) < 2000 && std::abs(f.max() - 700000) < 2000);
    StrokeFinder still;
    still.start(0, 20000, 3000, 2000, 20000);
    for (t = 0; t <= 20000; t += 2) still.update(400000 + (t % 7), t);
    CHECK(still.result(20000) == StrokeFinder::Result::Failed);

    // Slow crank (10 s per turn): must not stop half way through the first sweep.
    StrokeFinder slow;
    slow.start(0, 20000, 3000, 2000, 30000);
    for (t = 0; t < 30000; t += 2) {
        slow.update((int32_t)(400000 - 300000 * std::cos(t / 10000.0 * 2 * 3.14159265)), t);
        if (slow.result(t) != StrokeFinder::Result::Searching) break;
    }
    CHECK(slow.result(t) == StrokeFinder::Result::Found);
    CHECK(std::abs(slow.max() - 700000) < 2000);
}

// --- Simulated machine ----------------------------------------------------------

struct Machine {
    // drive
    bool running = false;
    double setHz = 0;
    double hz = 0;
    double rampHzPerS = 20;
    bool answerQueries = true;
    int exceptionsToSend = 0;
    std::vector<std::vector<uint8_t>> framesReceived;
    // mechanics: one revolution of the crank = one cycle of the yoke
    double cyclesPerSecondPerHz = 0.1; // 10 Hz at the drive -> 1 cycle per second
    double revolutions = 0;            // 0 = top of the stroke
    int32_t center = 400000;
    int32_t amplitude = 300000;        // stroke 100000 .. 700000, bottom = high values
    // load cell
    int32_t emptyLoad = 14000;
    bool shockMounted = false;
    int32_t shockForce = 60000;
    uint32_t noise = 12345;

    void onFrame(const uint8_t* f, size_t, bool) {
        framesReceived.emplace_back(f, f + 8);
        const uint16_t reg = readUint16BE(f + 2);
        const uint16_t value = readUint16BE(f + 4);
        if (exceptionsToSend > 0) {
            exceptionsToSend--;
            DriveFrame e{{0x03, (uint8_t)(f[1] | 0x80), 0x03}, 5};
            const uint16_t crc = modbusCrc16(e.bytes, 3);
            e.bytes[3] = crc & 0xFF;
            e.bytes[4] = crc >> 8;
            g_driveReplies.push_back(e);
            return;
        }
        if (f[1] == MODBUS_WRITE_REGISTER) {
            if (reg == DRIVE_REG_SET_FREQUENCY_RAM || reg == DRIVE_REG_SET_FREQUENCY) setHz = value / 100.0;
            if (reg == DRIVE_REG_CONTROL && value == DRIVE_CONTROL_FORWARD) running = true;
            if (reg == DRIVE_REG_CONTROL && value == DRIVE_CONTROL_STOP) running = false;
            DriveFrame echo{};
            std::memcpy(echo.bytes, f, 8);
            echo.length = 8;
            g_driveReplies.push_back(echo);
        } else if (f[1] == MODBUS_READ_REGISTERS && reg == DRIVE_REG_RUNNING_FREQUENCY && answerQueries) {
            DriveFrame a{{0x03, 0x03, 0x02}, 7};
            writeUint16BE((uint16_t)std::lround(hz * 100), a.bytes + 3);
            const uint16_t crc = modbusCrc16(a.bytes, 5);
            a.bytes[5] = crc & 0xFF;
            a.bytes[6] = crc >> 8;
            g_driveReplies.push_back(a);
        }
    }

    void step(double dt) {
        const double target = running ? setHz : 0;
        const double dv = rampHzPerS * dt;
        hz = std::fabs(target - hz) <= dv ? target : hz + (target > hz ? dv : -dv);
        revolutions += hz * cyclesPerSecondPerHz * dt;
    }

    int32_t jitter(int32_t span) {
        noise = noise * 1103515245u + 12345u;
        return (int32_t)((noise >> 8) % (uint32_t)(2 * span + 1)) - span;
    }
    int32_t position() {
        return center - (int32_t)(amplitude * std::cos(revolutions * 2 * 3.14159265358979)) + jitter(300);
    }
    int32_t force() {
        int32_t f = emptyLoad + jitter(200);
        if (shockMounted) f += (int32_t)(shockForce * std::sin(revolutions * 2 * 3.14159265358979));
        return f;
    }
};

struct Sim {
    GuiLink gui;
    DriveLink link;
    MotorDrive drive;
    PointBuffer points;
    StateMachine sm;
    Machine m;
    uint32_t nowMs = 1000;
    uint32_t sequence = 0;
    bool sensorsOn = true;
    std::function<void(SensorSample&)> sampleHook;
    // plant revolutions at each sample's run-relative time, to check the sections
    std::vector<std::pair<uint32_t, double>> revolutionsAt;

    Sim() {
        resetFakes();
        g_onDriveFrame = [this](const uint8_t* f, size_t n, bool u) { m.onFrame(f, n, u); };
        drive.begin(&link);
        sm.begin(&gui, &drive, &points);
    }
    Sim(const Sim&) = delete;

    void command(uint8_t flag, std::vector<uint8_t> payload = {}) {
        Command c{};
        c.flag = flag;
        c.payloadSize = payload.size();
        if (!payload.empty()) std::memcpy(c.payload, payload.data(), payload.size());
        sm.handleCommand(c, nowMs);
    }

    void tickOnce() {
        nowMs += 2;
        m.step(0.002);
        sm.pollDrive();
        if (sensorsOn) {
            SensorSample s{m.position(), m.force(), nowMs * 1000u, ++sequence};
            if (sampleHook) sampleHook(s);
            sm.onSample(s, nowMs);
            revolutionsAt.emplace_back(nowMs * 1000u, m.revolutions);
        }
        sm.tick(nowMs);
    }
    void run(uint32_t ms) {
        for (uint32_t i = 0; i < ms / 2; i++) tickOnce();
    }
    bool runUntil(const std::function<bool()>& done, uint32_t maxMs) {
        for (uint32_t i = 0; i < maxMs / 2; i++) {
            tickOnce();
            if (done()) return true;
        }
        return false;
    }
    bool runUntilState(State s, uint32_t maxMs) {
        return runUntil([&] { return sm.state() == s; }, maxMs);
    }

    // --- what the ESP sent
    int count(uint8_t flag) const {
        int n = 0;
        for (const auto& e : g_guiEvents) n += (e.kind == GuiEvent::Command && e.flag == flag);
        return n;
    }
    const GuiEvent* last(uint8_t flag) const {
        for (auto it = g_guiEvents.rbegin(); it != g_guiEvents.rend(); ++it)
            if (it->kind == GuiEvent::Command && it->flag == flag) return &*it;
        return nullptr;
    }
    bool anyText(uint8_t flag, const char* contains) const {
        for (const auto& e : g_guiEvents)
            if (e.kind == GuiEvent::Command && e.flag == flag && e.text().find(contains) != std::string::npos)
                return true;
        return false;
    }
    void printLog() const {
        for (const auto& e : g_guiEvents) {
            if (e.kind == GuiEvent::Flush) std::printf("    [flush then %u]\n", e.flag);
            else if (e.flag <= 4) std::printf("    [%u] %s\n", e.flag, e.text().c_str());
            else std::printf("    [%u] (%zu bytes)\n", e.flag, e.payload.size());
        }
    }

    // Crank turns at a run time (run time 0 = the run's first point).
    double revolutionsAtRunUs(uint32_t runUs) const {
        for (const auto& r : revolutionsAt)
            if (r.first >= firstRunSampleUs + runUs) return r.second;
        return revolutionsAt.back().second;
    }
    uint32_t firstRunSampleUs = 0;

    // GUI flow: run request, calibration, steps, start
    void startRun(const std::vector<std::pair<double, int>>& steps) {
        command(Cmd::GUI_run_request, {(uint8_t)steps.size()});
        uint8_t limit[3];
        writeInt24BE((int32_t)(3000.0 / 0.004575), limit);
        command(Cmd::GUI_calibrate, {limit[0], limit[1], limit[2]});
        runUntilState(State::Armed, 40000);
        for (size_t i = 0; i < steps.size(); i++)
            command(Cmd::GUI_send_sequence, guiStep((uint8_t)i, (uint8_t)steps[i].second, steps[i].first));
        command(Cmd::GUI_run_start);
        firstRunSampleUs = (nowMs + 2) * 1000u;
    }
};

static std::unique_ptr<Sim> tarredSim() {
    auto sim = std::make_unique<Sim>();
    sim->run(200);
    sim->command(Cmd::GUI_TAR_request);
    sim->runUntilState(State::Idle, 60000);
    sim->m.shockMounted = true;
    g_guiEvents.clear();
    return sim;
}

// --- State machine scenarios ------------------------------------------------------

TEST(connect) {
    Sim sim;
    sim.command(Cmd::GUI_check_connection);
    CHECK(sim.count(Cmd::ESP_good_connection) == 1);
    CHECK(sim.sm.state() == State::Idle);
    CHECK(!sim.m.running);
}

TEST(tar_measures_empty_load_and_bottoms_out) {
    Sim sim;
    sim.run(200);
    sim.command(Cmd::GUI_TAR_request);
    CHECK(sim.sm.state() == State::Tar);
    bool reachedTarSpeed = false;
    CHECK(sim.runUntil([&] {
        reachedTarSpeed = reachedTarSpeed || sim.m.setHz == Config::TAR_CENTIHZ / 100.0;
        return sim.sm.state() != State::Tar;
    }, 60000));
    CHECK(sim.sm.state() == State::Idle);
    CHECK(reachedTarSpeed);
    CHECK(!sim.m.running && sim.m.setHz == 0);
    CHECK(sim.count(Cmd::ESP_emergency) == 0);
    CHECK(sim.anyText(Cmd::ESP_message, "TAR done"));
    if (const GuiEvent* done = sim.last(Cmd::ESP_message)) std::printf("    %s\n", done->text().c_str());
    const GuiEvent* cal = sim.last(Cmd::ESP_calibration_data);
    CHECK(cal != nullptr && cal->payload.size() == 9);
    if (cal) {
        CHECK(std::abs(readInt24BE(&cal->payload[0]) - 100000) < 3000);
        CHECK(std::abs(readInt24BE(&cal->payload[3]) - 700000) < 3000);
        CHECK(std::abs(readInt24BE(&cal->payload[6]) - 14000) < 50);
    }
    // stopped on the bottom side of the stroke
    const double turn = std::fmod(sim.m.revolutions, 1.0);
    CHECK(turn > 0.25 && turn < 0.75);
    if (g_failures) sim.printLog();
}

TEST(run_counts_the_requested_cycles) {
    auto sim = tarredSim();
    sim->startRun({{10.0, 5}, {20.0, 3}});
    CHECK(sim->count(Cmd::ESP_run_accepted) == 1);
    CHECK(sim->count(Cmd::ESP_calibration_data) == 1);
    CHECK(sim->count(Cmd::ESP_run_starting) == 1);
    CHECK(sim->sm.state() == State::Operating);
    CHECK(sim->runUntilState(State::Idle, 60000));
    CHECK(sim->count(Cmd::ESP_emergency) == 0);
    CHECK(sim->count(Cmd::ESP_sequence_separator) == 2);
    CHECK(!sim->m.running);

    // run_finished goes out after the points (flush), after both separators
    CHECK(!g_guiEvents.empty() && g_guiEvents.back().kind == GuiEvent::Flush &&
          g_guiEvents.back().flag == Cmd::ESP_run_finished);

    // each section holds the requested cycles: at least that many crank turns,
    // and less than one more (the partial first one isn't counted)
    const int wanted[2] = {5, 3};
    int i = 0;
    for (const auto& e : g_guiEvents) {
        if (e.kind != GuiEvent::Command || e.flag != Cmd::ESP_sequence_separator) continue;
        const uint32_t start = readUint32BE(&e.payload[0]);
        const uint32_t end = readUint32BE(&e.payload[4]);
        const double turns = sim->revolutionsAtRunUs(end) - sim->revolutionsAtRunUs(start);
        std::printf("    step %d: %.2f turns between %.3f s and %.3f s\n", i + 1, turns, start / 1e6, end / 1e6);
        CHECK(turns >= wanted[i] - 0.05 && turns < wanted[i] + 1.05);
        i++;
    }
    // points: raw values, times from 0, increasing
    CHECK(g_pushedPoints.size() > 1000);
    CHECK(g_pushedPoints.front().timeUs == 0);
    bool increasing = true;
    for (size_t k = 1; k < g_pushedPoints.size(); k++)
        increasing = increasing && g_pushedPoints[k].timeUs > g_pushedPoints[k - 1].timeUs;
    CHECK(increasing);
    if (g_failures) sim->printLog();
}

TEST(single_spikes_dont_trip_the_failsafe) {
    auto sim = tarredSim();
    int n = 0;
    sim->sampleHook = [&](SensorSample& s) {
        n++;
        if (n % 50 == 0) s.force = INT24_MAX;              // one bad reading
        if (n % 77 == 0) { s.force = INT24_MIN; }          // two in a row
        if (n % 77 == 1) { s.force = INT24_MIN; }
        if (n % 91 == 0) s.position = INT24_MAX;           // position glitch
    };
    sim->startRun({{10.0, 3}});
    CHECK(sim->runUntilState(State::Idle, 60000));
    CHECK(sim->count(Cmd::ESP_emergency) == 0);
    CHECK(sim->count(Cmd::ESP_sequence_separator) == 1);
    if (g_failures) sim->printLog();
}

TEST(sustained_overload_trips_the_failsafe) {
    auto sim = tarredSim();
    sim->startRun({{10.0, 20}});
    sim->run(3000);
    int burst = 0;
    sim->sampleHook = [&](SensorSample& s) {
        if (burst++ < 5) s.force += 700000; // ~3200 N for 10 ms
    };
    sim->run(100);
    CHECK(sim->sm.state() == State::Panic);
    CHECK(sim->anyText(Cmd::ESP_error, "PANIC: excessive force"));
    CHECK(sim->count(Cmd::ESP_emergency) == 1);
    CHECK(!sim->m.running);
    sim->sampleHook = nullptr;
    sim->command(Cmd::GUI_abort);
    CHECK(sim->sm.state() == State::Idle);
    CHECK(sim->count(Cmd::ESP_emergency_clear) == 1);
}

TEST(sensor_stall_stops_the_run) {
    auto sim = tarredSim();
    sim->startRun({{10.0, 20}});
    sim->run(2000);
    sim->sensorsOn = false;
    sim->run(200);
    CHECK(sim->sm.state() == State::Panic);
    CHECK(sim->anyText(Cmd::ESP_error, "PANIC: sensor stalled"));
    CHECK(!sim->m.running);
}

TEST(drive_error_stops_the_run) {
    auto sim = tarredSim();
    sim->startRun({{10.0, 20}, {20.0, 5}});
    sim->run(2000);
    // an error answer arriving while counting
    DriveFrame e{{0x03, 0x86, 0x03}, 5};
    const uint16_t crc = modbusCrc16(e.bytes, 3);
    e.bytes[3] = crc & 0xFF;
    e.bytes[4] = crc >> 8;
    g_driveReplies.push_back(e);
    sim->run(10);
    CHECK(sim->sm.state() == State::Panic);
    CHECK(sim->anyText(Cmd::ESP_error, "Drive error 0x03 (invalid data) on function 0x06"));
    CHECK(sim->anyText(Cmd::ESP_error, "PANIC: drive fault"));
    CHECK(!sim->m.running);
}

TEST(drive_refusing_the_start_is_reported) {
    auto sim = tarredSim();
    sim->m.exceptionsToSend = 0;
    sim->command(Cmd::GUI_run_request, {1});
    uint8_t limit[3];
    writeInt24BE(655737, limit);
    sim->command(Cmd::GUI_calibrate, {limit[0], limit[1], limit[2]});
    sim->runUntilState(State::Armed, 40000);
    sim->command(Cmd::GUI_send_sequence, guiStep(0, 5, 10.0));
    sim->m.exceptionsToSend = 2; // refuses the frequency and the start
    sim->command(Cmd::GUI_run_start);
    sim->run(10);
    CHECK(sim->sm.state() == State::Panic);
    CHECK(sim->anyText(Cmd::ESP_error, "last sent: 0x06 reg 0x2000 = 0x0001 ; 0x06 reg 0x800A = 0x03E8"));
}

TEST(drive_that_never_answers_is_assumed_at_speed) {
    auto sim = tarredSim();
    sim->m.answerQueries = false;
    sim->startRun({{10.0, 3}});
    CHECK(sim->runUntilState(State::Idle, 60000));
    CHECK(sim->count(Cmd::ESP_emergency) == 0);
    CHECK(sim->count(Cmd::ESP_sequence_separator) == 1);
}

TEST(run_requests_that_are_refused) {
    Sim sim;
    sim.command(Cmd::GUI_run_request, {1}); // no sensor data yet
    CHECK(sim.count(Cmd::ESP_run_denied) == 1);
    sim.run(100);
    sim.command(Cmd::GUI_run_request, {0});
    sim.command(Cmd::GUI_run_request, {(uint8_t)(MAX_SEQUENCE_STEPS + 1)});
    sim.command(Cmd::GUI_run_request, {});
    CHECK(sim.count(Cmd::ESP_run_denied) == 4);
    CHECK(sim.sm.state() == State::Idle);

    // the stroke isn't known yet (no TAR, no calibration)
    sim.command(Cmd::GUI_run_request, {1});
    CHECK(sim.sm.state() == State::Armed);
    sim.command(Cmd::GUI_send_sequence, guiStep(0, 5, 10.0));
    sim.command(Cmd::GUI_run_start);
    CHECK(sim.count(Cmd::ESP_run_denied) == 5);
    CHECK(sim.sm.state() == State::Idle);
    CHECK(!sim.m.running);

    // incomplete sequence
    sim.command(Cmd::GUI_run_request, {2});
    sim.command(Cmd::GUI_send_sequence, guiStep(0, 5, 10.0));
    sim.command(Cmd::GUI_run_start);
    CHECK(sim.count(Cmd::ESP_run_denied) == 6);
    CHECK(sim.anyText(Cmd::ESP_error, "sequence incomplete, 1 of 2"));
}

TEST(armed_without_start_times_out) {
    Sim sim;
    sim.run(100);
    sim.command(Cmd::GUI_run_request, {1});
    CHECK(sim.sm.state() == State::Armed);
    sim.run(Config::ARMED_TIMEOUT_MS + 100);
    CHECK(sim.sm.state() == State::Idle);
    CHECK(sim.count(Cmd::ESP_run_denied) == 1);
}

TEST(busy_machine_refuses_without_stopping) {
    Sim sim;
    sim.run(100);
    sim.command(Cmd::GUI_TAR_request);
    sim.run(2000);
    sim.command(Cmd::GUI_run_request, {1});
    CHECK(sim.count(Cmd::ESP_run_denied) == 1);
    CHECK(sim.sm.state() == State::Tar);
    sim.command(Cmd::GUI_set_rpm, guiStep(0, 1, 30.0) /* not a valid frame size anyway */);
    CHECK(sim.anyText(Cmd::ESP_error, "Drive frame refused"));
    CHECK(sim.runUntilState(State::Idle, 60000));
    CHECK(sim.count(Cmd::ESP_emergency) == 0);
}

TEST(abort_stops_everything) {
    auto sim = tarredSim();
    sim->startRun({{10.0, 20}});
    sim->run(3000);
    CHECK(sim->m.running);
    sim->command(Cmd::GUI_abort);
    CHECK(sim->sm.state() == State::Idle);
    CHECK(!sim->m.running);
    CHECK(g_guiEvents.back().kind == GuiEvent::Flush); // the points so far still go out
}

TEST(bottom_out_only) {
    Sim sim;
    sim.run(100);
    sim.command(Cmd::GUI_bottom_out);
    CHECK(sim.runUntilState(State::Idle, 60000));
    CHECK(sim.anyText(Cmd::ESP_message, "Bottom out done"));
    CHECK(sim.count(Cmd::ESP_emergency) == 0);
    const double turn = std::fmod(sim.m.revolutions, 1.0);
    CHECK(turn > 0.25 && turn < 0.75);
}

TEST(debug_buttons_in_idle) {
    Sim sim;
    sim.run(500);
    sim.command(Cmd::GUI_calibrate); // no payload: report, no motion
    const GuiEvent* cal = sim.last(Cmd::ESP_calibration_data);
    CHECK(cal != nullptr && std::abs(readInt24BE(&cal->payload[6]) - 14000) < 100);
    CHECK(sim.sm.state() == State::Idle && !sim.m.running);

    uint8_t frame[8];
    buildWriteRequest(DRIVE_ADDRESS, DRIVE_REG_SET_FREQUENCY_RAM, 1234, frame);
    sim.command(Cmd::GUI_set_rpm, std::vector<uint8_t>(frame, frame + 8));
    CHECK(sim.m.setHz == 12.34);

    sim.command(Cmd::GUI_force_reboot);
    CHECK(sim.sm.rebootRequested());
}

int main() {
    for (const auto& t : tests()) {
        g_test = t.name;
        const int before = g_failures;
        t.fn();
        std::printf("%s %s\n", g_failures == before ? "ok  " : "FAIL", t.name);
    }
    std::printf("\n%d checks, %d failed\n", g_checks, g_failures);
    return g_failures == 0 ? 0 : 1;
}
