// Talks to DynamiX_Simulator.dll the way the HMI does: handshake, TAR, a run of two speeds with its calibration,
// then a reboot. Prints what happened and returns 0 if everything came back as it should.
// The simulation runs 4x faster than real time here, so the heartbeat is sent every 50 ms.
#include <windows.h>

#include <chrono>
#include <cstdint>
#include <cstdio>
#include <cstring>
#include <functional>
#include <string>
#include <thread>
#include <vector>

#include "../DynamiX_Firmware/command_flags.h"

typedef int (*OpenFn)();
typedef void (*CloseFn)();
typedef int (*WriteFn)(const uint8_t*, int);
typedef int (*ReadFn)(uint8_t*, int);
typedef void (*SpeedFn)(double);

static OpenFn sim_open;
static CloseFn sim_close;
static WriteFn sim_write;
static ReadFn sim_read;
static SpeedFn sim_speed;

struct Frame {
    uint8_t flag;
    std::vector<uint8_t> payload;
};

static std::vector<uint8_t> g_stream;
static std::vector<Frame> g_commands;
static int g_dataFrames = 0;
static int g_points = 0;
static int g_failures = 0;

static void check(bool ok, const char* what) {
    std::printf("%s %s\n", ok ? "ok  " : "FAIL", what);
    if (!ok) g_failures++;
}

static void send(uint8_t flag, const std::vector<uint8_t>& payload = {}) {
    std::vector<uint8_t> frame = {0xFF, 0x00, 0x55, (uint8_t)(1 + payload.size()), flag};
    frame.insert(frame.end(), payload.begin(), payload.end());
    sim_write(frame.data(), (int)frame.size());
}

static void parse() {
    size_t i = 0;
    while (i + 4 <= g_stream.size()) {
        if (g_stream[i] != 0xFF || g_stream[i + 1] != 0x00) { i++; continue; }
        const size_t size = g_stream[i + 3];
        if (i + 4 + size > g_stream.size()) break;
        const uint8_t type = g_stream[i + 2];
        if (type == 0xAA) {
            g_dataFrames++;
            g_points += (int)(size / 10);
        } else if (type == 0x55 && size >= 1) {
            Frame f;
            f.flag = g_stream[i + 4];
            f.payload.assign(g_stream.begin() + i + 5, g_stream.begin() + i + 4 + size);
            g_commands.push_back(f);
        }
        i += 4 + size;
    }
    g_stream.erase(g_stream.begin(), g_stream.begin() + i);
}

static int count(uint8_t flag) {
    int n = 0;
    for (const Frame& f : g_commands) n += f.flag == flag;
    return n;
}

static const Frame* last(uint8_t flag) {
    for (auto it = g_commands.rbegin(); it != g_commands.rend(); ++it)
        if (it->flag == flag) return &*it;
    return nullptr;
}

// Reads and sends heartbeats until done() or the timeout (real time).
static bool wait(const std::function<bool()>& done, int timeoutMs) {
    auto start = std::chrono::steady_clock::now();
    auto lastBeat = start;
    for (;;) {
        uint8_t buf[4096];
        int n;
        while ((n = sim_read(buf, sizeof(buf))) > 0) g_stream.insert(g_stream.end(), buf, buf + n);
        parse();
        if (done()) return true;
        auto now = std::chrono::steady_clock::now();
        if (std::chrono::duration_cast<std::chrono::milliseconds>(now - start).count() > timeoutMs) return false;
        if (std::chrono::duration_cast<std::chrono::milliseconds>(now - lastBeat).count() >= 50) {
            send(Cmd::GUI_heartbeat);
            lastBeat = now;
        }
        std::this_thread::sleep_for(std::chrono::milliseconds(2));
    }
}

static int32_t int24(const std::vector<uint8_t>& p, size_t at) {
    int32_t u = (p[at] << 16) | (p[at + 1] << 8) | p[at + 2];
    return (u ^ 0x800000) - 0x800000;
}

static uint16_t crc16(const uint8_t* d, size_t n) {
    uint16_t crc = 0xFFFF;
    for (size_t i = 0; i < n; i++) {
        crc ^= d[i];
        for (int b = 0; b < 8; b++) crc = (crc & 1) ? (crc >> 1) ^ 0xA001 : crc >> 1;
    }
    return crc;
}

static std::vector<uint8_t> step(uint8_t order, uint8_t cycles, double driveHz) {
    uint8_t f[8] = {0x03, 0x06, 0x80, 0x0A, 0, 0, 0, 0};
    const uint16_t centi = (uint16_t)(driveHz * 100 + 0.5);
    f[4] = centi >> 8;
    f[5] = centi & 0xFF;
    const uint16_t crc = crc16(f, 6);
    f[6] = crc & 0xFF;
    f[7] = crc >> 8;
    std::vector<uint8_t> p = {order, cycles};
    p.insert(p.end(), f, f + 8);
    return p;
}

static std::string text(const Frame& f) { return std::string(f.payload.begin(), f.payload.end()); }

int main() {
    HMODULE dll = LoadLibraryA("DynamiX_Simulator.dll");
    if (!dll) { std::printf("DynamiX_Simulator.dll not found next to selftest.exe\n"); return 2; }
    sim_open = (OpenFn)GetProcAddress(dll, "dynamix_sim_open");
    sim_close = (CloseFn)GetProcAddress(dll, "dynamix_sim_close");
    sim_write = (WriteFn)GetProcAddress(dll, "dynamix_sim_write");
    sim_read = (ReadFn)GetProcAddress(dll, "dynamix_sim_read");
    sim_speed = (SpeedFn)GetProcAddress(dll, "dynamix_sim_set_speed");
    if (!sim_open || !sim_close || !sim_write || !sim_read || !sim_speed) { std::printf("missing exports\n"); return 2; }

    check(sim_open() == 1, "open");
    sim_speed(4.0);

    // the boot message, then the handshake
    check(wait([] { return count(Cmd::ESP_message) >= 1; }, 3000) && text(*last(Cmd::ESP_message)).find("BOOT") == 0, "boot message");
    send(Cmd::GUI_check_connection);
    check(wait([] { return count(Cmd::ESP_good_connection) == 1; }, 3000), "ESP_good_connection");
    check(wait([] { return count(Cmd::ESP_live_data) >= 3; }, 3000), "live data every heartbeat");
    {
        const Frame* live = last(Cmd::ESP_live_data);
        check(live && live->payload.size() == 15, "live data is 15 bytes");
        if (live && live->payload.size() == 15)
            std::printf("     live: position %d force %d temperature %d\n", int24(live->payload, 0), int24(live->payload, 9), int24(live->payload, 12));
    }

    // failsafe window like the HMI sends it: +-3000 N around the zero, in counts
    {
        const int lo = (int)(-18469 - 3000 / 0.0050718), hi = (int)(-18469 + 3000 / 0.0050718);
        send(Cmd::GUI_set_failsafe, {(uint8_t)(lo >> 16), (uint8_t)(lo >> 8), (uint8_t)lo, (uint8_t)(hi >> 16), (uint8_t)(hi >> 8), (uint8_t)hi});
    }

    // TAR
    send(Cmd::GUI_TAR_request);
    check(wait([] { return count(Cmd::ESP_tar_data) == 1; }, 90000), "TAR finished");
    check(count(Cmd::ESP_emergency) == 0, "no panic during the TAR");
    if (const Frame* tar = last(Cmd::ESP_tar_data)) {
        const int32_t lo = int24(tar->payload, 0), hi = int24(tar->payload, 3), empty = int24(tar->payload, 6);
        std::printf("     TAR: position %d..%d (%.1f mm), empty load %d counts (%.1f N)\n", lo, hi, (hi - lo) * 0.000056267, empty, (empty + 18469) * 0.0050718);
        check(std::abs((hi - lo) * 0.000056267 - 25.0) < 1.0, "TAR stroke is 25 mm");
        check(std::abs((empty + 18469) * 0.0050718 - 13.4) < 1.0, "TAR empty load is the fixture's 13.4 N");
    }
    wait([] { return count(Cmd::ESP_message) >= 2; }, 1500);

    // a run: request, calibration, two speeds, start
    send(Cmd::GUI_run_request, {2});
    check(wait([] { return count(Cmd::ESP_run_accepted) == 1; }, 3000), "run accepted");
    send(Cmd::GUI_calibrate);
    check(wait([] { return count(Cmd::ESP_calibration_data) == 1; }, 90000), "calibration done");
    if (const Frame* cal = last(Cmd::ESP_calibration_data)) {
        check(cal->payload.size() == 12, "calibration data is 12 bytes");
        if (cal->payload.size() == 12)
            std::printf("     calibration: bottom force %.1f N, top force %.1f N\n", (int24(cal->payload, 6) + 18469) * 0.0050718, (int24(cal->payload, 9) + 18469) * 0.0050718);
    }
    send(Cmd::GUI_send_sequence, step(0, 3, 20.0));
    send(Cmd::GUI_send_sequence, step(1, 3, 40.0));
    send(Cmd::GUI_run_start);
    check(wait([] { return count(Cmd::ESP_run_starting) == 1; }, 5000), "run starting");
    check(wait([] { return count(Cmd::ESP_run_finished) == 1; }, 120000), "run finished");
    check(count(Cmd::ESP_sequence_separator) == 2, "two separators");
    check(count(Cmd::ESP_emergency) == 0, "no panic in the run");
    std::printf("     %d data frames, %d points\n", g_dataFrames, g_points);
    check(g_points > 2000, "points streamed during the run");

    // reboot
    const int boots = count(Cmd::ESP_message);
    send(Cmd::GUI_force_reboot);
    check(wait([&] { const Frame* m = last(Cmd::ESP_message); return count(Cmd::ESP_message) > boots && m && text(*m).find("BOOT") == 0; }, 5000), "reboot: boots again");

    // a heartbeat to a rebooted ESP32 is answered with ESP_not_connected
    check(wait([] { return count(Cmd::ESP_not_connected) >= 1; }, 3000), "heartbeat after the reboot: ESP_not_connected");

    for (const Frame& f : g_commands)
        if (f.flag == Cmd::ESP_error) std::printf("     ESP error: %s\n", text(f).c_str());

    sim_close();
    std::printf(g_failures == 0 ? "SIM OK\n" : "%d FAILED\n", g_failures);
    return g_failures;
}
