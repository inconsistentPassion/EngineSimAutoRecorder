#include "pipe.h"
#include "common.h"
#include <Windows.h>
#include "log.h"
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
    Log("[+] Pipe server starting on %s", PIPE_NAME);

    while (pipeRunning.load()) {
      HANDLE hPipe = CreateNamedPipeA(
            PIPE_NAME,
     PIPE_ACCESS_DUPLEX,
      PIPE_TYPE_MESSAGE | PIPE_READMODE_MESSAGE | PIPE_WAIT,
            1, PIPE_BUFFER_SIZE, PIPE_BUFFER_SIZE, 0, NULL);

        if (hPipe == INVALID_HANDLE_VALUE) {
  Log("[!] CreateNamedPipe failed: %lu", GetLastError());
  Sleep(1000);
        continue;
   }

        Log("[+] Waiting for client connection...");
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

        Log("[+] Client connected to pipe");

  DWORD lastRpmSend = 0;
        double lastMaxRpmSent = 0.0;
        double lastTorqueSent = 0.0;

        while (pipeRunning.load()) {
        DWORD now = GetTickCount();

    // ── Send RPM every 10ms (~100Hz) ──────────────────────────
        if (now - lastRpmSend >= 10) {
    MsgRpmUpdate msg;
      msg.type = MSG_RPM_UPDATE;
         msg.rpm = State::currentRpm.load();

      DWORD written;
             if (!WriteFile(hPipe, &msg, sizeof(msg), &written, NULL)) {
       DWORD err = GetLastError();
           if (err == ERROR_BROKEN_PIPE || err == ERROR_NO_DATA) {
         Log("[!] Pipe write failed, client disconnected");
        break;
            }
                }
     lastRpmSend = now;
   }

    // ── Send max RPM when it changes (once after hook reads it) ──
    {
        double curMax = State::maxRpm.load();
        if (curMax > 0 && curMax != lastMaxRpmSent) {
            MsgMaxRpm maxMsg;
            maxMsg.type = MSG_MAX_RPM;
            maxMsg.maxRpm = curMax;
            DWORD written;
            WriteFile(hPipe, &maxMsg, sizeof(maxMsg), &written, NULL);
            lastMaxRpmSent = curMax;
        }
    }

    // ── Send torque when it changes significantly ──────────────
    {
        double curTorque = State::torqueLbft.load();
        if (std::abs(curTorque - lastTorqueSent) > 0.5) {
            MsgTorqueUpdate tMsg;
            tMsg.type = MSG_TORQUE_UPDATE;
            tMsg.torqueLbft = curTorque;
            DWORD written;
            WriteFile(hPipe, &tMsg, sizeof(tMsg), &written, NULL);
            lastTorqueSent = curTorque;
        }
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
      Log("[+] Throttle command: %f", cmd->throttle);
       ApplyThrottleDirect(cmd->throttle);
            {
       std::lock_guard<std::mutex> lock(State::throttleMutex);
    State::targetThrottle = cmd->throttle;
           State::throttleOverride = true;
   }
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
             Log("[+] Kill command received");
          pipeRunning.store(false);
     break;
         }
   default:
                Log("[!] Unknown msg: 0x%X (%lu bytes)", (int)msgType, bytesRead);
 break;
   }
      }
        else if (!ok) {
      DWORD err = GetLastError();
     if (err == ERROR_BROKEN_PIPE) {
    {
        std::lock_guard<std::mutex> lock(State::throttleMutex);
        State::throttleOverride = false;
    }
       Log("[+] Client disconnected");
        break;
                }
       // ERROR_NO_DATA is normal for non-blocking read with no data
          }

         Sleep(10);
      }

        DisconnectNamedPipe(hPipe);
        CloseHandle(hPipe);
  }

    Log("[+] Pipe server stopped");
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
