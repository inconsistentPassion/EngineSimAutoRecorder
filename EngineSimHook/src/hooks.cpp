#include "log.h"
#include "hooks.h"
#include "common.h"
#include "memory.h"
#include <MinHook.h>
#include <cmath>

// ── Function pointer types ───────────────────────────────────────────

typedef __int64(__fastcall* IgnitionModuleFn)(__int64 a1, double a2);
typedef void(__fastcall* SimProcessFn)(__int64 a1, float a2);
typedef unsigned __int64*(__fastcall* UpdateHpAndTorqueFn)(__int64 instance, float dt);

static IgnitionModuleFn oIgnitionModule = nullptr;
static SimProcessFn oSimProcess = nullptr;
static UpdateHpAndTorqueFn oUpdateHpAndTorque = nullptr;

static constexpr double toRpm(double rad_s) {
    return rad_s / 0.104719755;
}

// ── Hooks ────────────────────────────────────────────────────────────

__int64 __fastcall ignitionModuleHk(__int64 a1, double a2) {
    if (State::attached.load()) {
        State::ignitionInstance.store(a1);
        uintptr_t crankshaftPtr = 0;
        if (Memory::SafeReadUintptr(a1, 0x60, crankshaftPtr) && crankshaftPtr) {
            double velocity = 0;
            if (Memory::SafeReadDouble(crankshaftPtr, 0x30, velocity)) {
                State::currentRpm.store(toRpm(std::fabs(velocity)));
            }
        }

        // Read max RPM (redline) from ignition instance +0x88
        double maxRpmRad = 0;
        if (Memory::SafeReadDouble(a1, 0x88, maxRpmRad) && maxRpmRad > 0) {
            State::maxRpm.store(toRpm(maxRpmRad));
        }
    }
    return oIgnitionModule(a1, a2);
}

void __fastcall simProcessHk(__int64 a1, float a2) {
    if (State::attached.load()) {
        State::appInstance.store(a1);
        
        uintptr_t simInst = 0, engInst = 0;
        Memory::SafeReadUintptr(a1, 0x1618, simInst);
        Memory::SafeReadUintptr(a1, 0x1600, engInst);

        if (simInst) State::simulatorInstance.store(simInst);
        if (engInst) State::engineInstance.store(engInst);

        // FIX: read both throttleOverride and targetThrottle under mutex
        bool doOverride;
        double throttle;
        {
            std::lock_guard<std::mutex> lock(State::throttleMutex);
            doOverride = State::throttleOverride;
            throttle = State::targetThrottle;
        }

        if (doOverride && engInst) {
            Memory::SafeWriteDouble(engInst, 0x188, 1.0 - throttle);
        }
    }
    oSimProcess(a1, a2);
}

unsigned __int64* __fastcall updateHpAndTorqueHk(__int64 instance, float dt) {
    if (State::attached.load()) {
        // instance + 0xA0 = power (hp), instance + 0xA8 = torque (lb·ft)
        double torque = 0;
        if (Memory::SafeReadDouble(instance, 0xA8, torque)) {
            State::torqueLbft.store(torque);
        }
    }
    return oUpdateHpAndTorque(instance, dt);
}

// ── Setup ────────────────────────────────────────────────────────────

void SetupHooks() {
    Log("=== SetupHooks starting ===");
    
    if (MH_Initialize() != MH_OK) {
        Log("[!] Failed to initialize MinHook");
        return;
    }
    Log("MinHook initialized OK");

    Log("--- Pattern Scanning ---");
    uintptr_t base = Memory::getBase();
    Log("Base address: 0x%llX", (unsigned long long)base);

    const char* ignitionPattern =
        "40 53 48 81 EC ? ? ? ? 44 0F 29 54 24 ? 48 8B D9 48 8B 49 60 "
        "44 0F 29 4C 24 ? 45 0F 57 C9 44 0F 29 6C 24 ? 44 0F 28 E9 "
        "0F 57 C9 E8 ? ? ? ?";

    const char* processPattern =
        "48 8B C4 48 89 58 10 48 89 70 18 48 89 78 20 55 41 54 41 55 "
        "41 56 41 57 48 8D 68 A1 48 81 EC ? ? ? ? 0F 29 70 C8 0F 29 78 B8 "
        "44 0F 29 40 ? 44";

    auto ignitionMatches = Memory::FindPatternAll(ignitionPattern);
    auto processMatches = Memory::FindPatternAll(processPattern);

    Log("[+] Ignition pattern: %zu match(es)", ignitionMatches.size());
    Log("[+] SimProcess pattern: %zu match(es)", processMatches.size());

    if (ignitionMatches.size() != 1) {
        Log("[!] ERROR: Expected 1 ignition match, got %zu", ignitionMatches.size());
        return;
    }
    if (processMatches.size() != 1) {
        Log("[!] ERROR: Expected 1 SimProcess match, got %zu", processMatches.size());
        return;
    }

    uintptr_t ignitionModFunc = ignitionMatches[0];
    uintptr_t processFunc = processMatches[0];

    Log("Ignition Module: 0x%llX", (unsigned long long)ignitionModFunc);
    Log("SimProcess: 0x%llX", (unsigned long long)processFunc);

    // Create all hooks first, then enable atomically.
    // MinHook suspends/resumes threads internally during MH_EnableHook —
    // do NOT suspend manually, it causes deadlocks in Release builds.
    bool ok1 = MH_CreateHook((LPVOID)ignitionModFunc, &ignitionModuleHk,
                              (LPVOID*)&oIgnitionModule) == MH_OK;
    Log("Ignition hook create: %s", ok1 ? "OK" : "FAILED");

    bool ok2 = MH_CreateHook((LPVOID)processFunc, &simProcessHk,
                              (LPVOID*)&oSimProcess) == MH_OK;
    Log("SimProcess hook create: %s", ok2 ? "OK" : "FAILED");

    // Optional torque hook
    const char* updateHpPattern =
        "40 53 48 83 EC 40 48 8B D9 48 8B 89 ? ? ? ? 48 85 C9 0F 84 ? ? ? ? "
        "48 8B 01 0F 29 74 24 ? 0F 29 7C 24 ? 0F 57 FF F3 0F 5A F9 0F 28 C7 "
        "F2 0F 58 05 ? ? ? ? F2";
    auto updateHpMatches = Memory::FindPatternAll(updateHpPattern);
    Log("[+] UpdateHpAndTorque pattern: %zu match(es)", updateHpMatches.size());

    if (updateHpMatches.size() == 1) {
        uintptr_t updateHpFunc = updateHpMatches[0];
        Log("UpdateHpAndTorque: 0x%llX", (unsigned long long)updateHpFunc);
        bool ok3 = MH_CreateHook((LPVOID)updateHpFunc, &updateHpAndTorqueHk,
                              (LPVOID*)&oUpdateHpAndTorque) == MH_OK;
        Log("Torque hook create: %s", ok3 ? "OK" : "FAILED");
    }

    if (!ok1 || !ok2) {
        Log("[!] CRITICAL: Hook creation failed!");
        MH_Uninitialize();
        State::attached.store(false);
        return;
    }

    // Enable all created hooks in one call — MinHook handles thread suspension
    MH_EnableHook(MH_ALL_HOOKS);

    State::attached.store(true);
    Log("=== Hooks active ===");
}

void CleanupHooks() {
    State::attached.store(false);
    State::running.store(false);

    MH_DisableHook(MH_ALL_HOOKS);
    MH_Uninitialize();

    Log("[+] Hooks removed");
}

// ═══════════════════════════════════════════════════════════════════════
// Engine control
// ═══════════════════════════════════════════════════════════════════════

void ApplyThrottleDirect(double throttle) {
    uintptr_t engInst = State::engineInstance.load();
    if (engInst) Memory::SafeWriteDouble(engInst, 0x188, 1.0 - throttle);
}

void SetDyno(bool enabled) {
    uintptr_t simInst = State::simulatorInstance.load();
    if (simInst) {
        Memory::SafeWriteBool(simInst, 0xE1, enabled);
        Log("[+] Dyno %s", enabled ? "ON" : "OFF");
    }
}

void SetIgnition(bool enabled) {
    uintptr_t ignInst = State::ignitionInstance.load();
    if (ignInst) {
        Memory::SafeWriteBool(ignInst, 0x50, enabled);
        Log("[+] Ignition %s", enabled ? "ON" : "OFF");
    }
}

void SetStarter(bool enabled) {
    uintptr_t simInst = State::simulatorInstance.load();
    if (simInst) {
        Memory::SafeWriteBool(simInst, 0x1C0, enabled);
        Log("[+] Starter %s", enabled ? "engaged" : "OFF");
    }
}

double GetStarterRPM() {
    uintptr_t simInst = State::simulatorInstance.load();
    if (simInst) {
        double val = 0.0;
        if (Memory::SafeReadDouble(simInst, 0x1B8, val)) return toRpm(std::fabs(val));
    }
    return 0.0;
}
