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

static IgnitionModuleFn oIgnitionModule = nullptr;
static SimProcessFn oSimProcess = nullptr;

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

        uintptr_t crankshaftPtr = *(uintptr_t*)(a1 + 0x60);
        if (crankshaftPtr) {
            double velocity = *(double*)(crankshaftPtr + 0x30);
            State::currentRpm.store(toRpm(std::fabs(velocity)));
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

// ── Setup ────────────────────────────────────────────────────────────

void SetupHooks() {
    if (MH_Initialize() != MH_OK) {
        std::cout << "[!] Failed to initialize MinHook\n";
        return;
    }

    std::cout << "--- Pattern Scanning ---\n";
    Memory::WriteLog("Base", Memory::getBase());

    uintptr_t ignitionModFunc = Memory::FindPatternIDA(
        "40 53 48 81 EC ? ? ? ? 44 0F 29 54 24 ? 48 8B D9 48 8B 49 60 "
        "44 0F 29 4C 24 ? 45 0F 57 C9 44 0F 29 6C 24 ? 44 0F 28 E9 "
        "0F 57 C9 E8 ? ? ? ?");

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

    // Resume all threads — game continues, now with hooks active
    ResumeThreads(suspended);
    std::cout << "[+] Resumed " << suspended.size() << " threads\n";

    if (ok1) std::cout << "[+] Ignition module hook installed\n";
    if (ok2) std::cout << "[+] SimProcess hook installed\n";

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
