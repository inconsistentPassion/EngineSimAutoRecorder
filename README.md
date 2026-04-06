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

### Publish (single executable)

```bash
cd EngineSimRecorder
dotnet publish -c Release
```

Output: `EngineSimRecorder.exe` + `es_hook.dll` + `icon.ico`

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
7. Output folder opens automatically when recording finishes.

### UI options

| Element | Description |
|---|---|
| **Process** | Select the running Engine Simulator process |
| **Output Dir** | Destination folder for WAV recordings |
| **📂** | Open the output folder in File Explorer |
| **Browse…** | Pick output folder via dialog |
| **Car Name** | Custom name prepended to output filenames |
| **Prefix** | Custom prefix before the car name (e.g. `ext_`) |
| **RPM Presets** | One-click buttons: 1K, 2K, 3K, 4K, 5K, 6K, 7K, 8K |
| **RPM Targets** | List of RPM points to record |
| **Sort ↑** | Sort RPM list ascending |
| **Clear All** | Remove all RPM targets |
| **Stay on Top** | Keep the recorder window above all others |
| **Status** | Current RPM, progress bar, and log output |

### RPM list management

- **Add** — type a value in the numeric input and click Add
- **Presets** — click any 1K–8K button for quick add (no duplicates)
- **Remove** — select an item and click Del, or right-click → Remove
- **Sort** — orders all RPMs ascending
- **Clear All** — wipes the entire list

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

WAV filenames are built from your Car Name, Prefix, and RPM values.

### Naming format

| Car Name | Prefix | RPM | Output |
|---|---|---|---|
| `supra` | `ext_` | 3000 | `ext_supra_3000_on.wav` |
| `supra` | *(empty)* | 3000 | `supra_3000_on.wav` |
| *(empty)* | *(empty)* | 3000 | `3000_on.wav` |

Each target RPM produces two files — `_on` (throttle held) and `_off` (throttle released).

### Example output

```
recordings/
├── ext_supra_1500_on.wav
├── ext_supra_1500_off.wav
├── ext_supra_3000_on.wav
├── ext_supra_3000_off.wav
├── ext_supra_6000_on.wav
└── ext_supra_6000_off.wav
```

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
