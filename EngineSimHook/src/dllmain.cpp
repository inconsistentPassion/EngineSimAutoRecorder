#include "common.h"
#include "hooks.h"
#include "pipe.h"
#include "log.h"
#include <stdlib.h>

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
            // Suppress the "abort() has been called" dialog — equivalent to
            // clicking Ignore so it silently terminates instead of showing
            // the retry/debug dialog.
            _set_abort_behavior(0, _WRITE_ABORT_MSG | _CALL_REPORTFAULT);
            CreateThread(NULL, 0, (LPTHREAD_START_ROUTINE)InitThread, NULL, 0, NULL);
            break;
        }
        case DLL_PROCESS_DETACH: {
            State::running.store(false);
            State::attached.store(false);

            // Always disable hooks first to restore original code bytes.
            // During process termination (lpReserved != NULL), the C++ runtime
            // and threads are being destroyed, so complex cleanup will crash.
            __try {
                MH_DisableHook(MH_ALL_HOOKS);
            }
            __except (EXCEPTION_EXECUTE_HANDLER) {
                // MinHook may already be partially torn down during process exit.
                // Swallow any exceptions to avoid abort().
            }

            // Only do full cleanup (thread join, MH_Uninitialize) on explicit
            // FreeLibrary. During process termination, skip it to avoid abort().
            if (lpReserved == NULL) {
                Log("[+] DLL unloaded via FreeLibrary - cleaning up");
                __try {
                    StopPipeServer();
                    MH_Uninitialize();
                }
                __except (EXCEPTION_EXECUTE_HANDLER) {
                    // Swallow exceptions during cleanup
                }
            } else {
                Log("[+] DLL unloaded via process termination - hooks disabled, skipping cleanup");
            }
            break;
        }
    }
    return TRUE;
}
