#include "pipe.h"
#include "common.h"
#include <Windows.h>
#include <iostream>
#include <thread>
#include <atomic>

// Forward declarations from hooks.cpp
void ApplyThrottleDirect(double throttle);
void SetDyno(bool enabled);
void SetIgnition(bool enabled);
void SetStarter(bool enabled);

static std::thread pipeThread;
static std::atomic<bool> pipeRunning{false};

static void PipeServerLoop() {
    std::cout << "[+] Pipe server starting on " << PIPE_NAME << "\n";

    while (pipeRunning.load()) {
      HANDLE hPipe = CreateNamedPipeA(
            PIPE_NAME,
     PIPE_ACCESS_DUPLEX,
      PIPE_TYPE_MESSAGE | PIPE_READMODE_MESSAGE | PIPE_WAIT,
            1, PIPE_BUFFER_SIZE, PIPE_BUFFER_SIZE, 0, NULL);

        if (hPipe == INVALID_HANDLE_VALUE) {
  std::cout << "[!] CreateNamedPipe failed: " << GetLastError() << "\n";
  Sleep(1000);
        continue;
   }

        std::cout << "[+] Waiting for client connection...\n";
        BOOL connected = ConnectNamedPipe(hPipe, NULL) ?
          TRUE : (GetLastError() == ERROR_PIPE_CONNECTED);

    if (!connected || !pipeRunning.load()) {
            CloseHandle(hPipe);
            if (!pipeRunning.load()) break;
       continue;
     }

  // Switch to non-blocking reads after connection so we can
        // interleave RPM sends with command reads without deadlocking
 DWORD mode = PIPE_READMODE_MESSAGE | PIPE_NOWAIT;
        SetNamedPipeHandleState(hPipe, &mode, NULL, NULL);

        std::cout << "[+] Client connected to pipe\n";

  DWORD lastRpmSend = 0;

        while (pipeRunning.load()) {
        DWORD now = GetTickCount();

    // ── Send RPM every 50ms ──────────────────────────────────
        if (now - lastRpmSend >= 50) {
    MsgRpmUpdate msg;
      msg.type = MSG_RPM_UPDATE;
         msg.rpm = State::currentRpm.load();

      DWORD written;
             if (!WriteFile(hPipe, &msg, sizeof(msg), &written, NULL)) {
       DWORD err = GetLastError();
           if (err == ERROR_BROKEN_PIPE || err == ERROR_NO_DATA) {
         std::cout << "[!] Pipe write failed, client disconnected\n";
        break;
            }
                }
     lastRpmSend = now;
   }

      // ── Read incoming commands (non-blocking) ─────────────────
uint8_t buf[PIPE_BUFFER_SIZE];
          DWORD bytesRead = 0;
     BOOL ok = ReadFile(hPipe, buf, sizeof(buf), &bytesRead, NULL);

            if (ok && bytesRead > 0) {
                uint8_t msgType = buf[0];

    switch (msgType) {
   case MSG_CMD_THROTTLE: {
    if (bytesRead >= sizeof(MsgCmdThrottle)) {
        auto* cmd = (MsgCmdThrottle*)buf;
      std::cout << "[+] Throttle command: " << cmd->throttle << "\n";
       ApplyThrottleDirect(cmd->throttle);
            {
       std::lock_guard<std::mutex> lock(State::throttleMutex);
    State::targetThrottle = cmd->throttle;
   }
           State::throttleOverride = true;
         }
        break;
          }
           case MSG_CMD_DYNO: {
       if (bytesRead >= sizeof(MsgCmdBool)) {
       auto* cmd = (MsgCmdBool*)buf;
     SetDyno(cmd->enabled != 0);
 }
          break;
           }
 case MSG_CMD_IGNITION: {
        if (bytesRead >= sizeof(MsgCmdBool)) {
          auto* cmd = (MsgCmdBool*)buf;
  SetIgnition(cmd->enabled != 0);
         }
 break;
         }
           case MSG_CMD_STARTER: {
           if (bytesRead >= sizeof(MsgCmdBool)) {
   auto* cmd = (MsgCmdBool*)buf;
          SetStarter(cmd->enabled != 0);
           }
  break;
   }
    case MSG_CMD_KILL: {
             std::cout << "[+] Kill command received\n";
          pipeRunning.store(false);
     break;
         }
   default:
                std::cout << "[!] Unknown msg: 0x"
    << std::hex << (int)msgType << std::dec
   << " (" << bytesRead << " bytes)\n";
 break;
   }
      }
        else if (!ok) {
      DWORD err = GetLastError();
     if (err == ERROR_BROKEN_PIPE) {
    State::throttleOverride = false;
       std::cout << "[+] Client disconnected\n";
        break;
                }
       // ERROR_NO_DATA is normal for non-blocking read with no data
          }

         Sleep(10);
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
