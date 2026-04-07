#include "hooks.h"
#include "common.h"
#include "memory.h"
#include <MinHook.h>
#include <TlHelp32.h>
#include <cmath>
#include <iostream>
#include <vector>

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

// ── Thread suspension helpers ────────────────────────────────────────
// Suspend all threads in the current process EXCEPT ours before
// patching function bytes. This prevents the game from being
// mid-execution of a hooked function when MinHook patches it.

static std::vector<DWORD> SuspendOtherThreads() {
    std::vector<DWORD> suspended;
    DWORD currentTid = GetCurrentThreadId();
    DWORD currentPid = GetCurrentProcessId();

    HANDLE snap = CreateToolhelp32Snapshot(TH32CS_SNAPTHREAD, 0);
    if (snap == INVALID_HANDLE_VALUE) return suspended;

    THREADENTRY32 te;
    te.dwSize = sizeof(te);

    if (Thread32First(snap, &te)) {
        do {
            if (te.th32OwnerProcessID == currentPid && te.th32ThreadID != currentTid) {
                HANDLE hThread = OpenThread(THREAD_SUSPEND_RESUME, FALSE, te.th32ThreadID);
                if (hThread) {
                    SuspendThread(hThread);
                    suspended.push_back(te.th32ThreadID);
                    CloseHandle(hThread);
                }
            }
        } while (Thread32Next(snap, &te));
    }

    CloseHandle(snap);
    return suspended;
}

static void ResumeThreads(const std::vector<DWORD>& threadIds) {
    for (DWORD tid : threadIds) {
        HANDLE hThread = OpenThread(THREAD_SUSPEND_RESUME, FALSE, tid);
        if (hThread) {
            ResumeThread(hThread);
            CloseHandle(hThread);
        }
    }
}

// ── Hooks ────────────────────────────────────────────────────────────

__int64 __fastcall ignitionModuleHk(__int64 a1, double a2) {
    if (State::attached.load()) {
        State::ignitionInstance.store(a1);
        // Crankshaft Pointer 
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
            *(double*)(engInst + 0x188) = 1.0 - throttle; //Throttle Position
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

    std::cout << "--- Pattern Scanning ---\n";
    Memory::WriteLog("Base", Memory::getBase());

    const char* ignitionPattern =
        "40 53 48 81 EC ? ? ? ? 44 0F 29 54 24 ? 48 8B D9 48 8B 49 60 "
        "44 0F 29 4C 24 ? 45 0F 57 C9 44 0F 29 6C 24 ? 44 0F 28 E9 "
        "0F 57 C9 E8 ? ? ? ?";

    const char* processPattern =
        "48 8B C4 48 89 58 10 48 89 70 18 48 89 78 20 55 41 54 41 55 "
        "41 56 41 57 48 8D 68 A1 48 81 EC ? ? ? ? 0F 29 70 C8 0F 29 78 B8 "
        "44 0F 29 40 ? 44";

    // Find all matches — reject if ambiguous (more than 1 match)
    auto ignitionMatches = Memory::FindPatternAll(ignitionPattern);
    auto processMatches = Memory::FindPatternAll(processPattern);

    std::cout << "[+] Ignition pattern: " << ignitionMatches.size() << " match(es)\n";
    std::cout << "[+] SimProcess pattern: " << processMatches.size() << " match(es)\n";

    if (ignitionMatches.size() != 1) {
        std::cout << "[!] ERROR: Expected 1 ignition match, got " << ignitionMatches.size()
                  << " — wrong Engine Simulator version?\n";
        return;
    }
    if (processMatches.size() != 1) {
        std::cout << "[!] ERROR: Expected 1 SimProcess match, got " << processMatches.size()
                  << " — wrong Engine Simulator version?\n";
        return;
    }

    uintptr_t ignitionModFunc = ignitionMatches[0];
    uintptr_t processFunc = processMatches[0];

    // Validate function prologs — catch cases where the pattern matched data, not code
    if (!Memory::ValidateFunctionProlog(ignitionModFunc)) {
        std::cout << "[!] WARNING: Ignition match at 0x" << std::hex << ignitionModFunc
                  << " doesn't look like a function prolog\n";
    }
    if (!Memory::ValidateFunctionProlog(processFunc)) {
        std::cout << "[!] WARNING: SimProcess match at 0x" << std::hex << processFunc
                  << " doesn't look like a function prolog\n";
    }

    Memory::WriteLog("Ignition Module", ignitionModFunc);
    Memory::WriteLog("SimProcess", processFunc);

    // ── SAFETY: Suspend all game threads before patching ─────────────
    // Without this, MinHook writes a JMP instruction over the function
    // prologue. If a thread is mid-execution of that function, the
    // instruction stream becomes inconsistent → crash.
    //
    // This is the main cause of ES-Studio's ~75% crash rate with
    // high-cylinder engines (more frames = more time in hooked functions).

    std::cout << "[+] Suspending game threads for safe hook install...\n";
    auto suspended = SuspendOtherThreads();
    std::cout << "[+] Suspended " << suspended.size() << " threads\n";

    // Now it's safe to patch — no thread can be executing these functions
    bool ok1 = MH_CreateHook((LPVOID)ignitionModFunc, &ignitionModuleHk,
                              (LPVOID*)&oIgnitionModule) == MH_OK;
    if (ok1) MH_EnableHook((LPVOID)ignitionModFunc);

    bool ok2 = MH_CreateHook((LPVOID)processFunc, &simProcessHk,
                              (LPVOID*)&oSimProcess) == MH_OK;
    if (ok2) MH_EnableHook((LPVOID)processFunc);

    // Optional: updateHpAndTorque hook for torque reading
    const char* updateHpPattern =
        "40 53 48 83 EC 40 48 8B D9 48 8B 89 ? ? ? ? 48 85 C9 0F 84 ? ? ? ? "
        "48 8B 01 0F 29 74 24 ? 0F 29 7C 24 ? 0F 57 FF F3 0F 5A F9 0F 28 C7 "
        "F2 0F 58 05 ? ? ? ? F2";
    auto updateHpMatches = Memory::FindPatternAll(updateHpPattern);
    std::cout << "[+] UpdateHpAndTorque pattern: " << updateHpMatches.size() << " match(es)\n";

    bool ok3 = false;
    if (updateHpMatches.size() == 1) {
        uintptr_t updateHpFunc = updateHpMatches[0];
        Memory::WriteLog("UpdateHpAndTorque", updateHpFunc);
        ok3 = MH_CreateHook((LPVOID)updateHpFunc, &updateHpAndTorqueHk,
                              (LPVOID*)&oUpdateHpAndTorque) == MH_OK;
        if (ok3) MH_EnableHook((LPVOID)updateHpFunc);
    } else {
        std::cout << "[!] WARNING: UpdateHpAndTorque not found — torque reading disabled\n";
    }

    // Resume all threads — game continues, now with hooks active
    ResumeThreads(suspended);
    std::cout << "[+] Resumed " << suspended.size() << " threads\n";

    if (ok1) std::cout << "[+] Ignition module hook installed\n";
    if (ok2) std::cout << "[+] SimProcess hook installed\n";
    if (ok3) std::cout << "[+] UpdateHpAndTorque hook installed\n";

    State::attached.store(true);
    std::cout << "[+] Hooks active\n";
}

void CleanupHooks() {
    State::attached.store(false);
    State::running.store(false);

    // Same safety: suspend before unhooking
    auto suspended = SuspendOtherThreads();
    MH_DisableHook(MH_ALL_HOOKS);
    MH_Uninitialize();
    ResumeThreads(suspended);

    std::cout << "[+] Hooks removed\n";
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
    if (simInst) {
        *(bool*)(simInst + 0xE1) = enabled;
        std::cout << "[+] Dyno " << (enabled ? "ON" : "OFF") << "\n";
    }
}

void SetIgnition(bool enabled) {
    uintptr_t ignInst = State::ignitionInstance.load();
    if (ignInst) {
        *(bool*)(ignInst + 0x50) = enabled;
        std::cout << "[+] Ignition " << (enabled ? "ON" : "OFF") << "\n";
    }
}

void SetStarter(bool enabled) {
    uintptr_t simInst = State::simulatorInstance.load();
    if (simInst) {
        *(bool*)(simInst + 0x1C0) = enabled;
        std::cout << "[+] Starter " << (enabled ? "engaged" : "OFF") << "\n";
    }
}

double GetStarterRPM() {
    uintptr_t simInst = State::simulatorInstance.load();
    if (simInst) return toRpm(std::fabs(*(double*)(simInst + 0x1B8)));
    return 0.0;
}
