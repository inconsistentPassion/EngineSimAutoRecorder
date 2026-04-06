# Engine Simulator Auto-Recorder

Automates engine-audio recording from [Engine Simulator](https://github.com/ange-yaghi/engine-sim).
Controls the engine via keyboard simulation, reads RPM from memory via a DLL hook, and captures system audio via WASAPI.
Output WAVs are ready for FMOD Studio / Assetto Corsa (or any FMOD-based game).

---

## How it works

The recorder injects a lightweight DLL hook into Engine Simulator's process to read RPM directly from memory. All engine control (throttle, ignition, starter, dyno) is done via keyboard simulation (`PostMessage` WM_KEYDOWN/WM_KEYUP).

- Injects `es_hook.dll` into Engine Simulator's process
- DLL hooks the ignition module (via [MinHook](https://github.com/TsudaKageworker/minhook)) to read RPM
- RPM is streamed back over a named pipe (~50ms update rate)
- Keyboard commands control throttle, ignition, dyno, and starter
- Thread-safe hook installation (suspends game threads before patching)
- Focus monitoring warns if neither window is focused during recording

Requires admin privileges for process injection.

---

## Architecture

```
Core/
  IEngineBackend.cs       ← backend interface
  KeyboardSim.cs          ← PostMessage WM_KEYDOWN/WM_KEYUP simulation
  PidController.cs        ← PID logic (reserved for future use)
  RecorderConfig.cs       ← shared config

Backends/
  Keyboard/
    KeyboardBackend.cs    ← DLL injection (RPM reading) + keyboard control

EngineSimHook/            ← C++ DLL (runs silently, no console)
  src/
    dllmain.cpp           ← entry point + init thread
    hooks.cpp             ← MinHook-based function hooking
    pipe.cpp              ← named pipe server (RPM + commands)
    memory.cpp            ← byte pattern scanning
  vendor/minhook/         ← vendored MinHook library
```

---

## Build

### One-step build

Open `EngineSimAutoRecorder.sln` in Visual Studio and build. The C++ hook DLL is compiled automatically via an MSBuild target — no manual cmake step needed.

**Requirements:**
- Visual Studio 2022 with C++ workload (for MSVC + CMake)
- [.NET 8 SDK](https://dotnet.microsoft.com/download/dotnet/8.0)
- `cmake` on your PATH (see below)

**Adding CMake to PATH:**

VS installs CMake but doesn't add it to PATH. Open a terminal and run:

```powershell
# Find your VS install path
$vs = & "${env:ProgramFiles(x86)}\Microsoft Visual Studio\Installer\vswhere.exe" -latest -property installationPath

# Add cmake to PATH for current session
$env:PATH += ";$vs\Common7\IDE\CommonExtensions\Microsoft\CMake\CMake\bin"

# Verify
cmake --version
```

Or add it permanently via **System Properties → Environment Variables → PATH**.

### Manual build (alternative)

```bash
cmake -S EngineSimHook -B EngineSimHook/build -G "Visual Studio 17 2022" -A x64
cmake --build EngineSimHook/build --config Release
cd EngineSimRecorder
dotnet build -c Release
```

---

## Usage

1. Launch Engine Simulator and load your engine.
2. Open the recorder.
3. Click **Refresh**, pick the process from the dropdown.
4. Set output folder and target RPMs.
5. Click **▶ Start** — the recorder will:
   - Inject the hook DLL (silent, no console window)
   - Force focus onto Engine Sim
   - Start the engine (ignition → starter → dyno)
   - Rev to each target RPM and hold
   - Record **load** and **no-load** WAVs at each RPM
6. Click **■ Stop** to cancel at any time.

### UI options

| Element | Description |
|---|---|
| **Process** | Select the running Engine Simulator process |
| **Output Dir** | Destination folder for WAV recordings |
| **📂** | Open the output folder in File Explorer |
| **Browse…** | Pick output folder via dialog |
| **RPM Targets** | List of RPM points to record (Add / Remove) |
| **Stay on Top** | Keep the recorder window above all others |
| **Status** | Current RPM, progress bar, and log output |

### Focus monitoring

During recording, the app checks window focus every second. If neither the recorder nor Engine Sim is focused, a warning appears in the status bar. Keep one of these windows focused for reliable input.

---

## Key bindings

These are the keyboard commands sent to Engine Simulator via `PostMessage`:

| Key | Action |
|---|---|
| **A** | Ignition (toggle) |
| **S** | Starter (hold to crank) |
| **D** | Dyno mode (toggle) |
| **H** | Hold RPM in dyno mode (toggle) |
| **R** | Throttle (hold to rev) |
| **W** | Throttle tap (used during startup) |

---

## Output

Each target RPM produces two files:

```
recordings/
├── 1500_load.wav
├── 1500_noload.wav
├── 3000_load.wav
├── 3000_noload.wav
├── 6000_load.wav
└── 6000_noload.wav
```

- **`_load.wav`** — engine under throttle at target RPM
- **`_noload.wav`** — engine at target RPM with throttle released

All files: **44 100 Hz, stereo, float PCM** — import directly into FMOD Studio.

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
- [NAudio](https://github.com/naudio/NAudio) — WASAPI loopback capture
