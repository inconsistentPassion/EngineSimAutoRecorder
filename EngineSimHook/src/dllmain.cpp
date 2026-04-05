#include "common.h"
#include "hooks.h"
#include "pipe.h"
#include <iostream>
#include <thread>

// ── State definitions ────────────────────────────────────────────────

namespace State {
    std::atomic<uintptr_t> appInstance{0};
    std::atomic<uintptr_t> simulatorInstance{0};
    std::atomic<uintptr_t> engineInstance{0};
    std::atomic<uintptr_t> ignitionInstance{0};

    std::atomic<double> currentRpm{0.0};

    std::mutex throttleMutex;
    double targetThrottle = 0.0;
    bool throttleOverride = false;

    std::atomic<bool> attached{false};
    std::atomic<bool> running{true};
}

// ── Initialization thread (avoid loader lock in DllMain) ─────────────

static void InitThread() {
    // Wait for the game to fully initialize its engine instances.
    // Too short = patterns not found (functions not loaded yet).
    // Too long = user waits. 3s is a safe middle ground.
    Sleep(3000);

    AllocConsole();
    FILE* f;
    freopen_s(&f, "CONOUT$", "w", stdout);

    std::cout << "=== EngineSimRecorder Hook ===\n";

    SetupHooks();

    if (State::attached.load()) {
        StartPipeServer();
        std::cout << "[+] Initialization complete — ready for connections\n";
    } else {
        std::cout << "[!] Hook setup failed — pipe server not started\n";
    }
}

// ── DLL Entry Point ──────────────────────────────────────────────────

BOOL APIENTRY DllMain(HMODULE hModule, DWORD reason, LPVOID lpReserved) {
    switch (reason) {
        case DLL_PROCESS_ATTACH: {
            DisableThreadLibraryCalls(hModule);
            CreateThread(NULL, 0, (LPTHREAD_START_ROUTINE)InitThread, NULL, 0, NULL);
            break;
        }
        case DLL_PROCESS_DETACH: {
            State::running.store(false);
            StopPipeServer();
            CleanupHooks();
            break;
        }
    }
    return TRUE;
}
