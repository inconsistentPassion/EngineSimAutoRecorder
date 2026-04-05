# Engine Simulator Auto-Recorder

Automates engine-audio recording from [Engine Simulator](https://github.com/ange-yaghi/engine-sim).  
Reads RPM, holds the engine at each target with a PID controller, and captures system audio via WASAPI.  
Output WAVs are ready for FMOD Studio / Assetto Corsa (or any FMOD-based game).

---

## Two modes

| Mode | RPM source | Throttle control | Admin? | Focus? |
|------|-----------|-----------------|--------|--------|
| **Injection** | Memory read via DLL hook + named pipe | Memory write to engine struct | Yes | No |
| **OCR** | PaddleOCR screen capture | `SendInput` W/S key simulation | No | Yes |

Pick one in the UI radio buttons at the top. Both share the same PID controller, recording loop, and output.

### Injection mode (precise)

- Injects `es_hook.dll` into Engine Simulator's process
- DLL hooks the ignition module (via [MinHook](https://github.com/TsudaKageworker/minhook)) to read RPM directly from memory
- Byte pattern scanning finds functions at runtime (ES-Studio approach)
- Throttle, ignition, dyno, starter all controlled via direct memory writes
- ~50ms update rate, exact float values, works in background
- Thread-safe hook installation (suspends game threads before patching)

### OCR mode (non-invasive)

- [PaddleOCR](https://github.com/PaddlePaddle/PaddleOCR) (via [Sdcb.PaddleInference](https://github.com/sdcb/Sdcb.PaddleInference)) reads RPM from a screen region
- Simulates W/S keypresses for throttle — window must be focused
- No injection, no admin rights needed
- ~200ms update rate, depends on OCR accuracy

---

## Architecture

```
Core/
  IEngineBackend.cs       ← interface both modes implement
  PidController.cs        ← shared PID logic
  RecorderConfig.cs       ← shared config + BackendMode enum

Backends/
  Injection/
    InjectionBackend.cs   ← DLL injection + named pipe client
  Ocr/
    OcrBackend.cs         ← PaddleOCR + SendInput keystrokes

EngineSimHook/            ← C++ DLL (injection mode only)
  src/
    hooks.cpp             ← MinHook-based function hooking
    pipe.cpp              ← named pipe server
    memory.cpp            ← byte pattern scanning
    dllmain.cpp           ← entry point + init thread
  vendor/minhook/         ← vendored MinHook library
```

---

## Build

### 1. Hook DLL (injection mode only)

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

### Injection mode

1. Launch Engine Simulator and load your engine.
2. Open the recorder, select **Injection** mode.
3. Click **Refresh**, pick the process from the dropdown.
4. Set output folder, target RPMs, PID gains, hold/record times.
5. Click **▶ Start** — the recorder will:
   - Inject the DLL
   - Start the engine (ignition → dyno → starter → wait for RPM > 200)
   - PID-control to each target RPM
   - Record WAV while stable

### OCR mode

1. Launch Engine Simulator and load your engine.
2. Select **OCR** mode.
3. Use a screen ruler (e.g. [ShareX](https://getsharex.com/)) to find the RPM digits bounding box.
4. Set X/Y/W/H in the OCR Region group.
5. Click **▶ Start** — the recorder will:
   - Warm up PaddleOCR
   - Press Enter to start the engine
   - OCR-read RPM and PID-control via W/S keys
   - Record WAV while stable
6. **Keep the game window focused** — keystrokes go to the focused window.

---

## PID tuning

The PID controller converts RPM error into throttle commands.

| Symptom | Fix |
|---|---|
| Engine overshoots and hunts | Reduce `Kp` |
| Too slow to reach target | Increase `Kp` |
| Steady-state offset remains | Increase `Ki` |
| Oscillation / jitter | Reduce `Ki` or increase `Kd` |
| Noisy OCR spikes (OCR mode) | Increase `RPM_TOLERANCE` |

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
- [BetterGI](https://github.com/babalae/better-genshin-impact) — PaddleOCR + OpenCV integration pattern
- [MinHook](https://github.com/TsudaKageworker/minhook) — function hooking library
- [Sdcb.PaddleInference](https://github.com/sdcb/Sdcb.PaddleInference) — C# PaddleOCR bindings
