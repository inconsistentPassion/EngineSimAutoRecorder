#include "pipe.h"
#include "common.h"
#include <Windows.h>
#include <iostream>
#include <thread>
#include <atomic>

// Forward declaration from hooks.cpp
void ApplyThrottleDirect(double throttle);

static std::thread pipeThread;
static std::atomic<bool> pipeRunning{false};

// ── Pipe server loop ─────────────────────────────────────────────────
// Creates a named pipe, waits for a client (the C# recorder), then
// enters a bidirectional loop:
//   OUT: sends RPM updates every 50ms
//   IN:  receives throttle / engine commands

static void PipeServerLoop() {
    std::cout << "[+] Pipe server starting on " << PIPE_NAME << "\n";

    while (pipeRunning.load()) {
        // Create pipe instance
        HANDLE hPipe = CreateNamedPipeA(
            PIPE_NAME,
            PIPE_ACCESS_DUPLEX,
            PIPE_TYPE_BYTE | PIPE_READMODE_BYTE | PIPE_NOWAIT,
            1,              // max instances
            PIPE_BUFFER_SIZE,
            PIPE_BUFFER_SIZE,
            0,
            NULL
        );

        if (hPipe == INVALID_HANDLE_VALUE) {
            std::cout << "[!] CreateNamedPipe failed: " << GetLastError() << "\n";
            Sleep(1000);
            continue;
        }

        // Wait for client connection (non-blocking, poll every 100ms)
        while (pipeRunning.load()) {
            BOOL connected = ConnectNamedPipe(hPipe, NULL) ?
                TRUE : (GetLastError() == ERROR_PIPE_CONNECTED);
            if (connected) break;
            Sleep(100);
        }

        if (!pipeRunning.load()) {
            CloseHandle(hPipe);
            break;
        }

        std::cout << "[+] Client connected to pipe\n";

        // Connected — enter send/receive loop
        DWORD lastRpmSend = 0;

        while (pipeRunning.load()) {
            DWORD now = GetTickCount();

            // ── Send RPM every 50ms ──────────────────────────────────
            if (now - lastRpmSend >= 50) {
                MsgRpmUpdate msg;
                msg.type = MSG_RPM_UPDATE;
                msg.rpm = State::currentRpm.load();

                DWORD written;
                BOOL ok = WriteFile(hPipe, &msg, sizeof(msg), &written, NULL);
                if (!ok) {
                    std::cout << "[!] Pipe write failed, client disconnected\n";
                    break;
                }
                lastRpmSend = now;
            }

            // ── Check for incoming commands ───────────────────────────
            uint8_t buf[PIPE_BUFFER_SIZE];
            DWORD bytesRead = 0;
            BOOL ok = ReadFile(hPipe, buf, sizeof(buf), &bytesRead, NULL);

            if (ok && bytesRead > 0) {
                uint8_t msgType = buf[0];

                switch (msgType) {
                    case MSG_CMD_THROTTLE: {
                        if (bytesRead >= sizeof(MsgCmdThrottle)) {
                            MsgCmdThrottle* cmd = (MsgCmdThrottle*)buf;
                            // Write throttle directly to engine memory
                            ApplyThrottleDirect(cmd->throttle);
                            // Enable override so game's keyboard input doesn't fight us
                            {
                                std::lock_guard<std::mutex> lock(State::throttleMutex);
                                State::targetThrottle = cmd->throttle;
                            }
                            State::throttleOverride = true;
                        }
                        break;
                    }
                    case MSG_CMD_KILL: {
                        std::cout << "[+] Kill command received\n";
                        pipeRunning.store(false);
                        break;
                    }
                    default:
                        std::cout << "[!] Unknown message type: 0x"
                                  << std::hex << (int)msgType << std::dec << "\n";
                        break;
                }
            }
            else if (!ok) {
                DWORD err = GetLastError();
                if (err == ERROR_BROKEN_PIPE || err == ERROR_NO_DATA) {
                    // Client disconnected — disable throttle override
                    State::throttleOverride = false;
                    std::cout << "[+] Client disconnected\n";
                    break;
                }
                // ERROR_NO_DATA with NOWAIT is normal when no data pending
            }

            Sleep(10); // ~100Hz command poll rate
        }

        DisconnectNamedPipe(hPipe);
        CloseHandle(hPipe);
    }

    std::cout << "[+] Pipe server stopped\n";
}

void StartPipeServer() {
    pipeRunning.store(true);
    pipeThread = std::thread(PipeServerLoop);
}

void StopPipeServer() {
    pipeRunning.store(false);
    if (pipeThread.joinable()) {
        pipeThread.join();
    }
}
