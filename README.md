# EngineSimAutoRecorder

[![GitHub Release](https://img.shields.io/github/v/release/inconsistentPassion/EngineSimAutoRecorder?style=flat-square)](https://github.com/inconsistentPassion/EngineSimAutoRecorder/releases)
[![GitHub Downloads](https://img.shields.io/github/downloads/inconsistentPassion/EngineSimAutoRecorder/total?style=flat-square&label=Downloads)](https://github.com/inconsistentPassion/EngineSimAutoRecorder/releases)
[![GitHub License](https://img.shields.io/github/license/inconsistentPassion/EngineSimAutoRecorder?style=flat-square)](LICENSE)

Automatically records engine audio from [Engine Simulator](https://github.com/ange-yaghi/engine-sim) at precise RPM points, applies car acoustics processing, and generates ready-to-import FMOD Studio scripts for **Assetto Corsa** car mods.

> **Status:** `2.0-Release` — Rewrite with expanded features.

---

## What It Does

1. Connects to a running **Engine Simulator** instance
2. Holds the engine at your target RPM points automatically
3. Records audio samples at each RPM (on-load + off-load)
4. Applies interior (cabin) or exterior (exhaust) audio processing
5. Generates FMOD Studio scripts that build the RPM-based engine events for you
6. Deploys the compiled sound bank

**The result:** realistic, layered engine sounds for AC mods — without manually recording and importing dozens of samples.

---

## Prerequisites

| Requirement | Details |
|---|---|
| **OS** | Windows 10/11 x64 |
| **.NET** | .NET 8.0 SDK ([download](https://dotnet.microsoft.com/download/dotnet/8.0)) |
| **C++ Build Tools** | Visual Studio 2022 with C++ CMake tools, or CMake 3.20+ |
| **Engine Simulator** | [Engine Simulator](https://github.com/ange-yaghi/engine-sim) |
| **FMOD Studio** | **1.08.12 only** ([FMOD archives](https://www.fmod.com/download)) |
| **Assetto Corsa** | Installed with the AC Audio SDK |
| **Audio Capture** | Stereo Mix, VB-Cable, or VoiceMeeter |

---

## Warnings

> ⚠️ **FMOD Studio must be exactly version 1.08.12.**
> Other versions (1.10, 2.0, etc.) are incompatible with Assetto Corsa and scripting APIs and will not work.

> ⚠️ **Always make a copy of your AC Audio SDK folder before use.**
> The tool writes to `Metadata/`, `Assets/`, and `Build/` inside the SDK project. Working on the original risks corrupting it.

> ⚠️ **DLL injection requires Administrator privileges.**
> Run the recorder as Admin, or run Engine Simulator at the same privilege level. Antivirus may flag the hook DLL — whitelist the build output directory if needed.

---

## Building

```bash
git clone https://github.com/inconsistentPassion/EngineSimAutoRecorder.git
cd EngineSimAutoRecorder
git checkout 2.0-Gamma
```

**Build the Hook DLL: (Just In Case it did not auto build or building is failed)**
```bash
cd EngineSimHook
mkdir build && cd build
cmake .. -G "Visual Studio 17 2022" -A x64
cmake --build . --config Release
```

**Build the Solution:**
```bash
cd EngineSimRecorder
dotnet build -c Release
```

**Publish (creates a clean distribution):**
```bash
cd EngineSimRecorder
dotnet publish -c Release
```

The output goes to `bin/Release/net8.0-windows/win-x64/publish/` with everything organized:
```
publish/
├── EngineSimRecorder.exe    # Main app
├── assets/
│   └── es_hook.dll          # Hook DLL
└── lib/
```

Or just open `EngineSimAutoRecorder.sln` in Visual Studio 2022 and build.

---

## How to Use

### 1. Make a Copy of the AC Audio SDK

```
Original:  C:\...\ac_audio_sdk_1_9\
Your copy: C:\...\ac_audio_sdk_1_9_copy\
```

You'll point the tool at this copy. Never use the original.

### 2. Launch and Connect

1. Run `EngineSimRecorder.exe` **as Administrator**
2. Open Engine Simulator and load your car/engine config
3. In the Recorder page, select the Engine Simulator process
4. Click **Connect** — the app will inject the hook DLL and start reading RPM data

### 3. Configure Recording

Head to the **Options** page:

| Setting | Description | Default |
|---|---|---|
| Sample Rate | 44100 or 48000 Hz | 44100 |
| Channels | Mono or Stereo | Stereo |
| Interior Mode | Cabin acoustics processing | Off |
| Car Type | Cabin preset (Sedan, Coupe, SUV, etc.) | Sedan |
| Exterior Preset | Exhaust processing (Raw, Sport, Race, etc.) | Raw |

You can also create and save **RPM Profiles** to reuse target RPM lists across sessions.

**Typical RPM ranges:**

| Engine Type | Range | Step |
|---|---|---|
| Economy 4-cyl | 800 – 6500 | 500 |
| Sports 6-cyl | 1000 – 8000 | 500 |
| High-rev V8 | 1000 – 9000 | 500 |
| Race car | 2000 – 12000 | 1000 |

### 4. Record

- Start the engine in Engine Simulator (the app can auto-start it, or do it manually)
- Enter your target RPM points in the Recorder page
- Click **Start Recording**

The app will automatically engage dyno mode, hold each RPM, and capture audio. Samples are saved as `{prefix}_on_{rpm}.wav` (on-load) and `{prefix}_off_{rpm}.wav` (off-load).

> **During recording:** Don't interact with the game window, don't alt-tab, and make sure the sim is the active window.

### 5. Generate FMOD Scripts

Go to the **Workspace** page:

1. Set your **Car Name** (must match your FMOD event folder)
2. Set the **FMOD Project Path** to your AC Audio SDK copy
3. Select your exterior/interior recording directories
4. Choose a generation mode:
   - **Use Existing Template** — clones an existing car's event structure
   - **From Scratch** — creates new events from a base template

The tool generates ES3 JavaScript, validates it for FMOD 1.08 compatibility, and copies your recordings into the FMOD project's Assets folder.

### 6. Import into FMOD Studio

1. Open **FMOD Studio 1.08.12** with your AC Audio SDK project
2. Either run the generated script from the Scripts menu, or use the **FmodImport** page:
   - Click **Connect** (TCP to 127.0.0.1:3663)
   - Click **Execute**
3. FMOD will import the WAVs, create events with RPM parameters, and set up crossfades
4. Verify the events look correct in FMOD, then **Build** the project

### 8. Deploy to Assetto Corsa

The post-build handler automatically build the compiled `.bank` file and filtered `GUIDs.txt` to:
```
the Build folder or {AC Content Root}/cars/{CarName}/sfx/
```

Launch Assetto Corsa and select your car — the new engine sounds are live.

---

## Credits

- [Engine Simulator](https://github.com/ange-yaghi/engine-sim) — the engine this tool records from
- [ES-Studio](https://github.com/inconsistentPassion/ES-Studio) — DLL injection approach, byte patterns, memory offsets
- [MinHook](https://github.com/TsudaKageworker/minhook) — function hooking library
- [NAudio](https://github.com/naudio/NAudio) — WASAPI loopback capture
- [FMOD Studio](https://www.fmod.com/) — audio middleware for Assetto Corsa

## License

See [LICENSE](LICENSE) for details.
