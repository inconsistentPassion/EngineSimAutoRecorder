#include "common.h"
#include "hooks.h"
#include "pipe.h"
#include "log.h"

// ── State definitions ────────────────────────────────────────────────

namespace State {
    std::atomic<uintptr_t> appInstance{0};
    std::atomic<uintptr_t> simulatorInstance{0};
    std::atomic<uintptr_t> engineInstance{0};
    std::atomic<uintptr_t> ignitionInstance{0};

    std::atomic<double> currentRpm{0.0};
    std::atomic<double> maxRpm{0.0};
    std::atomic<double> torqueLbft{0.0};

    std::mutex throttleMutex;
    double targetThrottle = 0.0;
    bool throttleOverride = false;

    std::atomic<bool> attached{false};
    std::atomic<bool> running{true};
}

// ── Initialization thread (avoid loader lock in DllMain) ─────────────

static void InitThread() {
    LogInit();
    Log("InitThread started");
    Log("Waiting 3s for game init...");
    Sleep(3000);
    Log("Calling SetupHooks...");
    SetupHooks();
    Log("SetupHooks returned, attached=%d", State::attached.load() ? 1 : 0);

    if (State::attached.load()) {
        Log("Starting pipe server...");
        StartPipeServer();
        Log("Pipe server started");
    } else {
        Log("Hooks not attached, pipe server NOT started");
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
            if (lpReserved == NULL) {
                StopPipeServer();
                CleanupHooks();
            }
            break;
        }
    }
    return TRUE;
}
