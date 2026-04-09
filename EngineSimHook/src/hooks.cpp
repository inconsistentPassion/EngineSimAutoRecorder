#include "hooks.h"
#include "common.h"
#include "memory.h"
#include <MinHook.h>
#include <cmath>
#include <iostream>

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

        uintptr_t crankshaftPtr = *(uintptr_t*)(a1 + 0x60);
        if (crankshaftPtr) {
            double velocity = *(double*)(crankshaftPtr + 0x30);
            State::currentRpm.store(toRpm(std::fabs(velocity)));
        }

        // Read max RPM (redline) from ignition instance +0x88
        double maxRpmRad = *(double*)(a1 + 0x88);
        if (maxRpmRad > 0) {
            State::maxRpm.store(toRpm(maxRpmRad));
        }
    }
    return oIgnitionModule(a1, a2);
}

void __fastcall simProcessHk(__int64 a1, float a2) {
    if (State::attached.load()) {
        State::appInstance.store(a1);

        uintptr_t simInst = *(uintptr_t*)(a1 + 0x1618);
        uintptr_t engInst = *(uintptr_t*)(a1 + 0x1600);

        if (simInst) State::simulatorInstance.store(simInst);
        if (engInst) State::engineInstance.store(engInst);

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

unsigned __int64* __fastcall updateHpAndTorqueHk(__int64 instance, float dt) {
    if (State::attached.load()) {
        // instance + 0xA0 = power (hp), instance + 0xA8 = torque (lb·ft)
        double torque = *(double*)(instance + 0xA8);
        State::torqueLbft.store(torque);
    }
    return oUpdateHpAndTorque(instance, dt);
}

// ── Setup ────────────────────────────────────────────────────────────

void SetupHooks() {
    if (MH_Initialize() != MH_OK) {
        std::cout << "[!] Failed to initialize MinHook\n";
        return;
    }

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

    if (ignitionMatches.size() != 1 || processMatches.size() != 1) {
        std::cout << "[!] Pattern mismatch: ignition=" << ignitionMatches.size()
                  << ", process=" << processMatches.size() << "\n";
        return;
    }

    uintptr_t ignitionModFunc = ignitionMatches[0];
    uintptr_t processFunc = processMatches[0];

    // MinHook handles atomic patching — no thread suspension needed
    bool ok1 = MH_CreateHook((LPVOID)ignitionModFunc, &ignitionModuleHk,
                              (LPVOID*)&oIgnitionModule) == MH_OK;
    if (ok1) MH_EnableHook((LPVOID)ignitionModFunc);

    bool ok2 = MH_CreateHook((LPVOID)processFunc, &simProcessHk,
                              (LPVOID*)&oSimProcess) == MH_OK;
    if (ok2) MH_EnableHook((LPVOID)processFunc);

    // Optional torque hook
    const char* updateHpPattern =
        "40 53 48 83 EC 40 48 8B D9 48 8B 89 ? ? ? ? 48 85 C9 0F 84 ? ? ? ? "
        "48 8B 01 0F 29 74 24 ? 0F 29 7C 24 ? 0F 57 FF F3 0F 5A F9 0F 28 C7 "
        "F2 0F 58 05 ? ? ? ? F2";
    auto updateHpMatches = Memory::FindPatternAll(updateHpPattern);

    bool ok3 = false;
    if (updateHpMatches.size() == 1) {
        uintptr_t updateHpFunc = updateHpMatches[0];
        ok3 = MH_CreateHook((LPVOID)updateHpFunc, &updateHpAndTorqueHk,
                              (LPVOID*)&oUpdateHpAndTorque) == MH_OK;
        if (ok3) MH_EnableHook((LPVOID)updateHpFunc);
    }

    if (!ok1 || !ok2) {
        std::cout << "[!] Hook installation failed\n";
        return;
    }

    State::attached.store(true);
    std::cout << "[+] Hooks active (ignition=" << ok1 << " process=" << ok2 << " torque=" << ok3 << ")\n";
}

void CleanupHooks() {
    State::attached.store(false);
    State::running.store(false);
    MH_DisableHook(MH_ALL_HOOKS);
    MH_Uninitialize();
}

// ═══════════════════════════════════════════════════════════════════════
// Engine control
// ═══════════════════════════════════════════════════════════════════════

void ApplyThrottleDirect(double throttle) {
    uintptr_t engInst = State::engineInstance.load();
    if (engInst) *(double*)(engInst + 0x188) = 1.0 - throttle;
}

void SetDyno(bool enabled) {
    uintptr_t simInst = State::simulatorInstance.load();
    if (simInst) *(bool*)(simInst + 0xE1) = enabled;
}

void SetIgnition(bool enabled) {
    uintptr_t ignInst = State::ignitionInstance.load();
    if (ignInst) *(bool*)(ignInst + 0x50) = enabled;
}

void SetStarter(bool enabled) {
    uintptr_t simInst = State::simulatorInstance.load();
    if (simInst) *(bool*)(simInst + 0x1C0) = enabled;
}
