#pragma once
#ifndef COMMON_H
#define COMMON_H

#include <Windows.h>
#include <atomic>
#include <mutex>

// ── Shared state between hook thread and pipe server thread ──────────

namespace State {
    // Engine Simulator instance pointers (set by hooks)
    extern std::atomic<uintptr_t> appInstance;
    extern std::atomic<uintptr_t> simulatorInstance;
    extern std::atomic<uintptr_t> engineInstance;
    extern std::atomic<uintptr_t> ignitionInstance;

    // Latest RPM from ignition hook
    extern std::atomic<double> currentRpm;

    // Throttle override (written by pipe thread, applied in simProcess hook)
    extern std::mutex throttleMutex;
    extern double targetThrottle;
    extern bool throttleOverride;

    // Flags
    extern std::atomic<bool> attached;
    extern std::atomic<bool> running;
}

// ── Pipe protocol messages ───────────────────────────────────────────

#pragma pack(push, 1)

constexpr uint8_t MSG_RPM_UPDATE    = 0x01;
constexpr uint8_t MSG_CMD_THROTTLE  = 0x10;
constexpr uint8_t MSG_CMD_STARTER   = 0x11;
constexpr uint8_t MSG_CMD_IGNITION  = 0x12;
constexpr uint8_t MSG_CMD_DYNO      = 0x13;
constexpr uint8_t MSG_CMD_KILL      = 0x1F;

struct MsgRpmUpdate {
    uint8_t type;       // MSG_RPM_UPDATE
    double rpm;
};

struct MsgCmdThrottle {
    uint8_t type;       // MSG_CMD_THROTTLE
    double throttle;    // 0.0 – 1.0
};

struct MsgCmdBool {
    uint8_t type;       // MSG_CMD_STARTER / IGNITION / DYNO
    uint8_t enabled;    // 0 or 1
};

struct MsgCmdKill {
    uint8_t type;       // MSG_CMD_KILL
};

#pragma pack(pop)

constexpr const char* PIPE_NAME = "\\\\.\\pipe\\es-recorder-pipe";
constexpr int PIPE_BUFFER_SIZE = 256;

#endif // COMMON_H
