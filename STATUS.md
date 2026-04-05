# STATUS.md — EngineSimAutoRecorder

> This file is for future AI sessions to understand the project state, what's been done, and what comes next.

## What this is

An automated tool that records engine audio from [Engine Simulator](https://github.com/ange-yaghi/engine-sim). It reads RPM, holds the engine at target RPMs with a PID controller, and captures system audio as WAV files for use in FMOD/Assetto Corsa.

## Current state (2026-04-06)

### Completed

- **Injection backend** (DLL + memory):
  - C++ hook DLL using MinHook (vendored)
  - Hooks ignition module for RPM (via byte pattern scanning)
  - Hooks simProcess for engine instance pointers
  - Named pipe IPC (`\\.\pipe\es-recorder-pipe`) — RPM at 50ms, throttle commands
  - Direct memory writes for: throttle, ignition, dyno, starter
  - Thread-safe hook installation (suspends game threads before patching) — fixes ES-Studio's ~75% crash rate
  - Engine startup sequence: ignition ON → dyno ON → starter ON → wait RPM>200 → starter OFF

- **UI**: WinForms with process selector, output dir, RPM targets, PID tuning, progress/log

- **Recording**: NAudio WASAPI loopback, 44100Hz stereo 16-bit PCM

### Tech stack

- .NET 8 (C#), WinForms
- NAudio 2.2.1
- C++20 (hook DLL), CMake, MinHook (vendored)

### Known issues

- Byte patterns in `hooks.cpp` are for current Engine Simulator version — may break on game updates
- No auto-detection of rotary vs piston engine (piston assumed)
- Injection DLL must be built separately with CMake + MSVC before C# build

## Future directions

1. **Auto-detect engine type** — check if engine is rotary vs piston, adjust throttle offset accordingly
2. **Recording quality options** — sample rate, bit depth, format selector in UI
3. **Batch mode / CLI** — headless operation for automation scripts
4. **Multi-engine presets** — save/load RPM lists + PID gains per car/engine
5. **Progressive RPM sweep** — continuous recording while RPM ramps up/down, not just discrete targets
6. **Pattern update mechanism** — auto-download new byte patterns when ES-Studio updates them
7. **Live RPM graph** — show RPM vs time in the UI during recording

## File map

```
EngineSimAutoRecorder/
├── README.md
├── STATUS.md                          ← this file
├── EngineSimRecorder/
│   ├── EngineSimRecorder.csproj       ← .NET 8, WinForms
│   ├── Form1.cs                       ← main UI + recording loop
│   ├── Form1.Designer.cs              ← WinForms designer
│   ├── Program.cs
│   ├── Core/
│   │   ├── IEngineBackend.cs
│   │   ├── PidController.cs
│   │   └── RecorderConfig.cs
│   └── Backends/
│       └── Injection/
│           └── InjectionBackend.cs
└── EngineSimHook/                     ← C++ DLL (injection mode)
    ├── CMakeLists.txt
    ├── src/
    │   ├── common.h
    │   ├── dllmain.cpp
    │   ├── hooks.cpp
    │   ├── hooks.h
    │   ├── memory.cpp
    │   ├── memory.h
    │   ├── pipe.cpp
    │   └── pipe.h
    └── vendor/minhook/                ← vendored
```

## How to pick up

- Read this file first
- Check `git log --oneline` for recent changes
- Backend implements `IEngineBackend` — new modes can be added as new classes
- Hook DLL byte patterns: check if Engine Simulator updated; if so, re-scan with IDA/Ghidra or check ES-Studio for updated patterns
