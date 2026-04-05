# Engine Simulator Auto-Recorder

Automates engine-audio recording from [Engine Simulator](https://github.com/ange-yaghi/engine-sim).
Reads RPM, holds the engine at each target with a PID controller, and captures system audio via WASAPI.
Output WAVs are ready for FMOD Studio / Assetto Corsa (or any FMOD-based game).

---

## How it works

The recorder injects a DLL hook into Engine Simulator's process to read RPM directly from memory and control throttle via memory writes. This gives ~50ms update rate, exact float values, and works in the background (no focused window needed).

- Injects `es_hook.dll` into Engine Simulator's process
- DLL hooks the ignition module (via [MinHook](https://github.com/TsudaKageworker/minhook)) to read RPM directly from memory
- Byte pattern scanning finds functions at runtime (ES-Studio approach)
- Throttle, ignition, dyno, starter all controlled via direct memory writes
- Thread-safe hook installation (suspends game threads before patching)

Requires admin privileges for process injection.

---

## Architecture

```
Core/
  IEngineBackend.cs       ← interface for backend implementations
  PidController.cs        ← shared PID logic
  RecorderConfig.cs       ← shared config

Backends/
  Injection/
    InjectionBackend.cs   ← DLL injection + named pipe client

EngineSimHook/            ← C++ DLL
  src/
    hooks.cpp             ← MinHook-based function hooking
    pipe.cpp              ← named pipe server
    memory.cpp            ← byte pattern scanning
    dllmain.cpp           ← entry point + init thread
  vendor/minhook/         ← vendored MinHook library
```

---

## Build

### 1. Hook DLL

Requires CMake + MSVC (Visual Studio with C++ workload).

```bash
cmake -S EngineSimHook -B EngineSimHook/build
cmake --build EngineSimHook/build --config Release
```

Output: `EngineSimHook/bin/es_hook.dll`

### 2. C# Application

Requires [.NET 8 SDK](https://dotnet.microsoft.com/download/dotnet/8.0).

```bash
cd EngineSimRecorder
dotnet restore
dotnet build -c Release
dotnet run -c Release
```

---

## Usage

1. Launch Engine Simulator and load your engine.
2. Open the recorder.
3. Click **Refresh**, pick the process from the dropdown.
4. Set output folder, target RPMs, PID gains, hold/record times.
5. Click **▶ Start** — the recorder will:
   - Inject the DLL
   - Start the engine (ignition → dyno → starter → wait for RPM > 200)
   - PID-control to each target RPM
   - Record WAV while stable

---

## PID tuning

The PID controller converts RPM error into throttle commands.

| Symptom | Fix |
|---|---|
| Engine overshoots and hunts | Reduce `Kp` |
| Too slow to reach target | Increase `Kp` |
| Steady-state offset remains | Increase `Ki` |
| Oscillation / jitter | Reduce `Ki` or increase `Kd` |

---

## Output

```
recordings/
├── rpm_800.wav
├── rpm_1500.wav
├── rpm_3000.wav
├── rpm_4500.wav
├── rpm_6000.wav
└── rpm_7500.wav
```

All files: **44 100 Hz, stereo, 16-bit PCM** — import directly into FMOD Studio.

### FMOD / Assetto Corsa setup

1. Import WAVs into FMOD Studio.
2. Create a parameter named `RPM` (range 0 → redline).
3. Assign each sample to its RPM range, enable cross-fading.
4. Add pitch-shifting automation for realism.
5. Build `.bank` → drop into `assettocorsa/content/cars/<car>/sfx/`.

---

## Credits

- [ES-Studio](https://github.com/inconsistentPassion/ES-Studio) — DLL injection approach, byte patterns, memory offsets
- [MinHook](https://github.com/TsudaKageworker/minhook) — function hooking library
