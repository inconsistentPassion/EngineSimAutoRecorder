#include "hooks.h"
#include "common.h"
#include "memory.h"
#include <MinHook.h>
#include <cmath>
#include <iostream>

// ── Function pointer types ───────────────────────────────────────────

typedef __int64(__fastcall* IgnitionModuleFn)(__int64 a1, double a2);
typedef void(__fastcall* SimProcessFn)(__int64 a1, float a2);

// ── Original function pointers (populated by MinHook) ────────────────

static IgnitionModuleFn oIgnitionModule = nullptr;
static SimProcessFn oSimProcess = nullptr;

// ── RPM conversion (rad/s → RPM) ────────────────────────────────────

static constexpr double toRpm(double rad_s) {
    return rad_s / 0.104719755;
}

// ── Hook: ignition module ────────────────────────────────────────────
// Called every frame. Captures ignition instance and reads RPM.

__int64 __fastcall ignitionModuleHk(__int64 a1, double a2) {
    if (State::attached.load()) {
        State::ignitionInstance.store(a1);

        // RPM at: *(double*)(*(QWORD*)(ignitionInstance + 0x60) + 0x30)
        uintptr_t crankshaftPtr = *(QWORD*)(a1 + 0x60);
        if (crankshaftPtr) {
            double velocity = *(double*)(crankshaftPtr + 0x30);
            double rpm = toRpm(std::fabs(velocity));
            State::currentRpm.store(rpm);
        }
    }
    return oIgnitionModule(a1, a2);
}

// ── Hook: sim process (main game tick) ───────────────────────────────
// Captures instance pointers and applies throttle override.

void __fastcall simProcessHk(__int64 a1, float a2) {
    if (State::attached.load()) {
        State::appInstance.store(a1);

        uintptr_t simInst = *(QWORD*)(a1 + 0x1618);
        uintptr_t engInst = *(QWORD*)(a1 + 0x1600);

        if (simInst) State::simulatorInstance.store(simInst);
        if (engInst) State::engineInstance.store(engInst);

        // Apply throttle override if active
        if (State::throttleOverride && engInst) {
            double throttle;
            {
                std::lock_guard<std::mutex> lock(State::throttleMutex);
                throttle = State::targetThrottle;
            }
            *(double*)(engInst + 0x188) = 1.0 - throttle;
        }
    }
    oSimProcess(a1, a2);
}

// ── Setup ────────────────────────────────────────────────────────────

void SetupHooks() {
    if (MH_Initialize() != MH_OK) {
        std::cout << "[!] Failed to initialize MinHook\n";
        return;
    }

    std::cout << "--- Pattern Scanning ---\n";
    uintptr_t base = Memory::getBase();
    Memory::WriteLog("Base", base);

    // Ignition module function
    uintptr_t ignitionModFunc = Memory::FindPatternIDA(
        "40 53 48 81 EC ? ? ? ? 44 0F 29 54 24 ? 48 8B D9 48 8B 49 60 "
        "44 0F 29 4C 24 ? 45 0F 57 C9 44 0F 29 6C 24 ? 44 0F 28 E9 "
        "0F 57 C9 E8 ? ? ? ?");

    // Main sim process function
    uintptr_t processFunc = Memory::FindPatternIDA(
        "48 8B C4 48 89 58 10 48 89 70 18 48 89 78 20 55 41 54 41 55 "
        "41 56 41 57 48 8D 68 A1 48 81 EC ? ? ? ? 0F 29 70 C8 0F 29 78 B8 "
        "44 0F 29 40 ? 44");

    Memory::WriteLog("Ignition Module", ignitionModFunc);
    Memory::WriteLog("SimProcess", processFunc);

    if (!ignitionModFunc || !processFunc) {
        std::cout << "[!] Critical pattern not found — wrong Engine Simulator version?\n";
        return;
    }

    // Install hooks
    if (MH_CreateHook((LPVOID)ignitionModFunc, &ignitionModuleHk,
                      (LPVOID*)&oIgnitionModule) == MH_OK) {
        MH_EnableHook((LPVOID)ignitionModFunc);
        std::cout << "[+] Ignition module hook installed\n";
    }

    if (MH_CreateHook((LPVOID)processFunc, &simProcessHk,
                      (LPVOID*)&oSimProcess) == MH_OK) {
        MH_EnableHook((LPVOID)processFunc);
        std::cout << "[+] SimProcess hook installed\n";
    }

    State::attached.store(true);
    std::cout << "[+] Hooks active\n";
}

void CleanupHooks() {
    State::attached.store(false);
    State::running.store(false);
    MH_DisableHook(MH_ALL_HOOKS);
    MH_Uninitialize();
    std::cout << "[+] Hooks removed\n";
}

// ═══════════════════════════════════════════════════════════════════════
// Engine control — direct memory writes (from ES-Studio)
// ═══════════════════════════════════════════════════════════════════════

void ApplyThrottleDirect(double throttle) {
    uintptr_t engInst = State::engineInstance.load();
    if (engInst) {
        *(double*)(engInst + 0x188) = 1.0 - throttle;
    }
}

void SetDyno(bool enabled) {
    uintptr_t simInst = State::simulatorInstance.load();
    if (simInst) {
        *(bool*)(simInst + 0xE1) = enabled;
        std::cout << "[+] Dyno " << (enabled ? "enabled" : "disabled") << "\n";
    }
}

void SetIgnition(bool enabled) {
    uintptr_t ignInst = State::ignitionInstance.load();
    if (ignInst) {
        *(bool*)(ignInst + 0x50) = enabled;
        std::cout << "[+] Ignition " << (enabled ? "enabled" : "disabled") << "\n";
    }
}

void SetStarter(bool enabled) {
    uintptr_t simInst = State::simulatorInstance.load();
    if (simInst) {
        *(bool*)(simInst + 0x1C0) = enabled;
        std::cout << "[+] Starter " << (enabled ? "engaged" : "disengaged") << "\n";
    }
}

double GetStarterRPM() {
    uintptr_t simInst = State::simulatorInstance.load();
    if (simInst) {
        return toRpm(std::fabs(*(double*)(simInst + 0x1B8)));
    }
    return 0.0;
}
