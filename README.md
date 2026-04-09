# EngineSimAutoRecorder

Automatically records engine audio samples from [Engine Simulator](https://github.com/ange-yaghi/engine-sim) at precise RPM points, applies real-time car acoustics processing, and generates FMOD Studio scripts to build sound banks for Assetto Corsa car mods.

> **Status:** 2.0-Gamma Branch — WPF rewrite with more features. Work in progress -> ESAR2.

---

## Table of Contents

- [What It Does](#what-it-does)
- [Architecture](#architecture)
- [Prerequisites](#prerequisites)
- [Warnings](#warnings)
- [Building](#building)
- [How to Use](#how-to-use)
  - [Step 1: Prepare Your Environment](#step-1-prepare-your-environment)
  - [Step 2: Build the Hook DLL](#step-2-build-the-hook-dll)
  - [Step 3: Launch the Recorder](#step-3-launch-the-recorder)
  - [Step 4: Connect to Engine Simulator](#step-4-connect-to-engine-simulator)
  - [Step 5: Configure Recording](#step-5-configure-recording)
  - [Step 6: Record](#step-6-record)
  - [Step 7: Generate FMOD Scripts](#step-7-generate-fmod-scripts)
  - [Step 8: Import into FMOD Studio](#step-8-import-into-fmod-studio)
  - [Step 9: Build and Deploy to Assetto Corsa](#step-9-build-and-deploy-to-assetto-corsa)
- [Audio Processing](#audio-processing)
  - [Interior (Cabin) Processing](#interior-cabin-processing)
  - [Exterior (Exhaust) Processing](#exterior-exhaust-processing)
- [RPM Profiles](#rpm-profiles)
- [Configuration Reference](#configuration-reference)
- [Troubleshooting](#troubleshooting)
- [License](#license)

---

## What It Does

The workflow in a nutshell:

1. **Inject** a small C++ DLL into Engine Simulator to read live RPM/torque data
2. **Control** the game engine via simulated keystrokes (throttle, ignition, starter, dyno)
3. **Hold** the engine at each target RPM and **record** the audio output
4. **Process** recordings with real-time DSP — simulating cabin acoustics or exhaust character
5. **Generate** FMOD Studio JavaScript (ES3) that automatically imports the recordings and builds RPM-based engine events
6. **Deploy** the compiled FMOD bank and GUIDs to an Assetto Corsa car's `sfx/` folder

---

## Architecture

```
EngineSimAutoRecorder.sln
│
├── EngineSimHook/              ← C++ DLL (injected into game process)
│   ├── src/
│   │   ├── dllmain.cpp         ← DLL entry point, hook setup
│   │   ├── hooks.cpp           ← MinHook function hooks
│   │   ├── memory.cpp/.h       ← Pattern scanning, memory read
│   │   └── pipe.cpp/.h         ← Named pipe server (streams RPM data)
│   ├── vendor/minhook/         ← MinHook library (vendored)
│   └── CMakeLists.txt
│
├── EngineSimRecorder/          ← WPF Desktop Application (.NET 8)
│   ├── Core/
│   │   ├── IEngineBackend.cs       ← Backend abstraction
│   │   ├── KeyboardSim.cs          ← Win32 PostMessage keyboard control
│   │   ├── InteriorProcessor.cs    ← Cabin acoustics DSP chain
│   │   ├── ExteriorProcessor.cs    ← Exhaust acoustics DSP chain
│   │   ├── RecorderConfig.cs       ← Recording configuration
│   │   ├── RpmProfile.cs           ← Saveable RPM target profiles
│   │   └── AppSettings.cs          ← Persistent app settings
│   ├── Backends/Keyboard/
│   │   └── KeyboardBackend.cs      ← DLL injection + keyboard control backend
│   ├── Services/
│   │   ├── FmodTcpService.cs       ← TCP connection to FMOD Studio (127.0.0.1:3663)
│   │   ├── FmodScriptGenerator.cs  ← Generates FMOD 1.08 ES3 import scripts
│   │   ├── FmodProjectCloner.cs    ← Clones FMOD event folder structures
│   │   ├── FmodEs3Validator.cs     ← Validates JS for FMOD 1.08 compatibility
│   │   ├── FmodPostBuildHandler.cs ← Copies bank + GUIDs to AC content folder
│   │   └── FmodImportResult.cs     ← Import result model
│   ├── View/Pages/                 ← WPF XAML pages
│   │   ├── RecorderPage            ← Main recording controls
│   │   ├── OptionsPage             ← Settings (sample rate, channels, DSP)
│   │   ├── ScriptsPage             ← FMOD script generation
│   │   ├── FmodImportPage          ← FMOD project import + build
│   │   └── LogPage                 ← Live log output
│   ├── ViewModel/Pages/            ← ViewModels (MVVM with CommunityToolkit)
│   ├── Helpers/
│   │   └── MouseWheelHelper.cs
│   └── App.xaml(.cs)               ← DI host, startup
│
└── assets/
    ├── impulses/
    │   └── exhaust_small.wav   ← Impulse response for FMOD convolution reverb
    └── fmod_import_recordings.js  ← Standalone FMOD 1.08 menu script
```

---

## Prerequisites

| Requirement | Details |
|---|---|
| **OS** | Windows 10/11 x64 |
| **.NET** | .NET 8.0 SDK ([download](https://dotnet.microsoft.com/download/dotnet/8.0)) |
| **C++ Build Tools** | Visual Studio 2022 with C++ CMake tools, or standalone CMake 3.20+ |
| **Engine Simulator** | [Engine Simulator](https://github.com/ange-yaghi/engine-sim) running |
| **FMOD Studio** | **1.08.12** (see [Warnings](#warnings) below) |
| **Assetto Corsa** | Installed with the **AC Audio SDK** (see [Warnings](#warnings) below) |
| **Audio** | A way to capture system audio (Stereo Mix, virtual audio cable, or VB-Cable) |

---

## Warnings

> [!CAUTION]
> ### FMOD Studio Version — Must Be Exactly 1.08.12
>
> This tool generates JavaScript that targets **FMOD Studio 1.08's ES3 engine**. It explicitly avoids ES6 features (`const`, `let`, arrow functions, template literals, `for...of`, etc.) because FMOD 1.08 does not support them.
>
> **Do NOT use FMOD Studio 1.10, 2.0, or any other version.** The scripting API changed significantly between versions. Using a different version will result in:
> - Scripts that fail to execute or throw cryptic errors
> - Missing API methods (e.g., `addGameParameter` behaves differently)
> - Event structure incompatibilities (track/model differences)
>
> Download FMOD Studio 1.08.12 from the [FMOD archives](https://www.fmod.com/download). You may need an FMOD account.

> [!CAUTION]
> ### Assetto Corsa Audio SDK — Make a Copy First
>
> The FMOD import and post-build steps read and write files inside the **Assetto Corsa Audio SDK** folder (the `.fspro` project). This includes:
> - `Metadata/` — XML files defining events, folders, parameters
> - `Assets/` — Audio asset files
> - `Build/` — Compiled bank files and GUIDs
>
> **Always work on a copy of the SDK folder, never the original.** The tool:
> - Creates new XML files in `Metadata/` when cloning event folders
> - Modifies parent relationship XML entries
> - Copies WAV files into `Assets/{Vendor}/{CarName}/`
> - Writes to `Build/` during FMOD project compilation
>
> If something goes wrong, you could corrupt your SDK project. Make a backup copy before pointing this tool at it.

> [!WARNING]
> ### DLL Injection Requires Admin Privileges
>
> The tool injects `es_hook.dll` into the Engine Simulator process using `VirtualAllocEx` + `CreateRemoteThread`. This requires:
> - Running the recorder as **Administrator**
> - Or running Engine Simulator at the same privilege level as the recorder
>
> Antivirus software may flag this as suspicious behavior. Whitelist the build output directory if needed.

---

## Building

### 1. Clone the Repository

```bash
git clone https://github.com/inconsistentPassion/EngineSimAutoRecorder.git
cd EngineSimAutoRecorder
git checkout 2.0-Gamma
```

### 2. Build the Hook DLL

```bash
cd EngineSimHook
mkdir build && cd build
cmake .. -G "Visual Studio 17 2022" -A x64
cmake --build . --config Release
```

This produces `EngineSimHook/bin/Release/es_hook.dll`. The WPF project also has MSBuild targets to auto-build this if CMake is available.

### 3. Build the WPF Application

```bash
cd EngineSimRecorder
dotnet build -c Release
```

Or open `EngineSimAutoRecorder.sln` in Visual Studio 2022 and build from there.

### 4. Output

The compiled application is at:
```
EngineSimRecorder/bin/Release/net8.0-windows/win-x64/EngineSimRecorder.exe
```

Make sure `es_hook.dll` is in the same directory or reachable by the application.

---

## How to Use

### Step 1: Prepare Your Environment

1. **Install FMOD Studio 1.08.12** — not any other version. Verify by opening FMOD Studio and checking Help → About.

2. **Make a copy of the Assetto Corsa Audio SDK folder.** This is typically found in the AC modding SDK distribution:
   ```
   Original:  C:\...\ac_audio_sdk_1_9\
   Your copy: C:\...\ac_audio_sdk_1_9_copy\
   ```
   You will point the tool at this copy. Never use the original.

3. **Set up audio capture.** You need a way for the recorder to capture Engine Simulator's audio output. Options:
   - **Stereo Mix** — Enable in Windows Sound settings (Recording tab)
   - **VB-Cable** — Free virtual audio cable ([download](https://vb-audio.com/Cable/))
   - **VoiceMeeter** — Advanced virtual audio mixer

   Set the virtual cable or Stereo Mix as your default recording device, or route Engine Simulator's audio output through it.

### Step 2: Build the Hook DLL

Build `es_hook.dll` following the [Building](#building) instructions above. The application searches for it in:
1. The application's own directory
2. `EngineSimHook/bin/{Configuration}/`
3. `EngineSimHook/build/bin/`

### Step 3: Launch the Recorder

Run `EngineSimRecorder.exe` **as Administrator** (required for DLL injection).

The app opens to the **Recorder** page by default.

### Step 4: Connect to Engine Simulator

1. Open **Engine Simulator** and load a car/eng configuration
2. In the Recorder page, select the Engine Simulator process from the process list
3. Click **Connect**. The app will:
   - Find the game's main window
   - Inject `es_hook.dll` into the process
   - Connect to the named pipe for RPM data
   - Start background RPM reading and throttle control threads
4. Check the log output for confirmation. You should see:
   ```
   ✓ Found Engine Simulator window handle: 0x...
   ✓ DLL injected successfully
   ✓ Pipe connection attempt 1/10... connected
   ✓ RPM reader thread started
   ✓ Backend ready - keyboard control + pipe RPM
   ```

### Step 5: Configure Recording

Go to the **Options** page to configure:

| Setting | Description | Default |
|---|---|---|
| Sample Rate | Audio sample rate (44100 or 48000) | 44100 |
| Channels | 1 = mono, 2 = stereo | 2 (stereo) |
| Interior Mode | Apply cabin acoustics processing | Off |
| Car Type | Cabin preset (Sedan, Coupe, SUV, Hatchback, Supercar, Truck) | Sedan |
| Exterior Preset | Exhaust processing preset (Raw, Sport, Race, Supercar, Muffler, Custom) | Raw |
| Record Limiter | Also record a limiter WAV for FMOD accessories | Off |

You can also create/load **RPM Profiles** to save your target RPM lists for reuse.

#### Setting Target RPMs

On the Recorder page, enter the RPM points you want to record. Typical ranges:

| Engine Type | RPM Range | Step |
|---|---|---|
| Economy 4-cyl | 800 – 6500 | 500 |
| Sports 6-cyl | 1000 – 8000 | 500 |
| High-rev V8 | 1000 – 9000 | 500 |
| Race car | 2000 – 12000 | 1000 |

Configure:
- **RPM Tolerance** — How close to the target RPM before recording starts (default: ±5 RPM)
- **Hold Seconds** — How long to stabilize the engine at the target RPM before recording (default: 5s)
- **Record Seconds** — Duration of each audio capture (default: 5s)

### Step 6: Record

1. In Engine Simulator, start the engine:
   - The app can auto-start it (Ignition → Starter sequence)
   - Or manually start it yourself
2. Click **Start Recording** in the Recorder page
3. The app will automatically:
   - Engage **Dyno mode** (sends D key)
   - Enable **Hold RPM** (sends H key)
   - For each target RPM:
     - Adjust throttle to approach the target
     - Wait for RPM to stabilize within tolerance
     - Hold for the configured stabilization time
     - Record audio for the configured duration
     - Save as `{prefix}_on_{rpm}.wav` (on-load) and `{prefix}_off_{rpm}.wav` (off-load, throttle released)
4. Monitor progress in the **Log** page

**During recording:**
- Do not interact with the game window
- Do not alt-tab or minimize the game
- Ensure your audio capture device is active and receiving game audio
- If interior mode is on, the processing is applied in real-time to the captured audio

### Step 7: Generate FMOD Scripts

Go to the **Scripts** page:

1. **Set Car Name** — The name matching your FMOD event folder (e.g., "Tatuus", "Abarth")
2. **Set FMOD Project Path** — Point to your **copy** of the AC Audio SDK `.fspro` folder
3. **Optionally set Vendor Name** — The manufacturer subfolder in Assets (auto-detected if left empty)
4. **Select directories:**
   - Exterior recordings directory
   - Interior recordings directory (optional)
   - Limiter recordings directory (optional)
5. **Choose generation mode:**
   - **Use Existing Template** — Clones an existing car's event structure and replaces audio
   - **From Scratch** — Creates new events from a base template (e.g., "tatuusfa1")
6. Click **Generate Script**

The tool will:
- Scan WAV files and parse RPM/load info from filenames
- Copy recordings into the FMOD project's `Assets/` structure
- Generate ES3 JavaScript that creates/updates FMOD events with:
  - Separate on-load and off-load audio tracks
  - RPM game parameter with smooth seek speed
  - Crossfade curves between adjacent RPM samples
  - Limiter accessory event (if enabled)
- Validate the script for FMOD 1.08 compatibility (checks for ES6 patterns)

### Step 8: Import into FMOD Studio

1. Open **FMOD Studio 1.08.12**
2. Open your **copy** of the AC Audio SDK project
3. Go to **Scripts** menu → run the generated script, or:
   - Go to the **FmodImport** page in the recorder
   - Click **Connect** to establish TCP connection to FMOD Studio (127.0.0.1:3663)
   - Click **Execute** to send the generated script
4. FMOD will:
   - Import all WAV files
   - Create/update event folders and events
   - Set up parameters, tracks, and automation
   - Save and build the project
5. Verify in FMOD Studio that events look correct — check the RPM parameter ranges and crossfade curves

### Step 9: Build and Deploy to Assetto Corsa

After FMOD Studio builds the project:

1. In the recorder's **FmodImport** page (or manually), the post-build handler will:
   - Filter `Build/GUIDs.txt` to lines relevant to your car name + bus entries
   - Copy the filtered `GUIDs.txt` and compiled `.bank` file to:
     ```
     {AC Content Root}/cars/{CarName}/sfx/
     ```
2. Launch Assetto Corsa and select your car
3. The new engine sounds should be active

---

## Audio Processing

### Interior (Cabin) Processing

Simulates what you hear sitting inside the car. Applied in real-time during recording when Interior Mode is enabled.

**DSP Chain (7 stages):**

1. **Rumble Boost** — Peaking EQ at 60-90 Hz (+6 dB, Q=1.2). Simulates structure-borne vibration through the chassis.

2. **Cabin Resonance #1** — Peaking EQ at 180 Hz (+5 dB, Q=1.5). First cabin standing wave.

3. **Cabin Resonance #2** — Peaking EQ at 350 Hz (+4 dB, Q=1.8). Second harmonic resonance.

4. **Low-Pass Filter** — Butterworth LPF at 1.5-3 kHz (car-dependent). Car cabins absorb high frequencies aggressively. Cutoff varies by car type:

   | Car Type  | LPF Cutoff | Stereo Width |
   |-----------|-----------|-------------|
   | Supercar  | 1800 Hz   | 20%         |
   | Coupe     | 2000 Hz   | 25%         |
   | Sedan     | 2200 Hz   | 30%         |
   | SUV       | 2500 Hz   | 35%         |
   | Truck     | 2800 Hz   | 40%         |

5. **Compressor** — 3:1 ratio, -12 dB threshold, 15 ms attack, 80 ms release. Simulates natural cabin compression.

6. **Stereo Narrowing** — Mid-side processing. Reduces stereo width to 20-40%. Small cabin = narrow stereo image.

7. **Comb Reverb** — 30 ms delay, 25% feedback, 5-10% wet. Simulates short reflections inside the cabin.

### Exterior (Exhaust) Processing

Simulates what you hear standing outside the car. Apply to recordings after capture (select preset before FMOD import).

**DSP Chain (7 stages):**

1. **Low-Pass Filter** — 5.5-9 kHz cutoff (preset-dependent). Rolls off extreme HF.

2. **High-Shelf Cut** — Gentle downward slope above 4-5.5 kHz (-2 to -6 dB). Natural exhaust HF rolloff.

3. **Low-Mid Boost** — Peaking EQ at 120-180 Hz (+2 to +4 dB). Exhaust pipe fundamental body.

4. **Soft-Clip Saturation** — `tanh` waveshaper with variable drive (1.0-2.8). Adds harmonic distortion/grit.

5. **Mechanical Noise** — Optional white noise injection at low level (0.05-0.10 gain). Simulates valve train, belt noise.

6. **Comb Reverb** — 20-30 ms delay. Simulates exhaust pipe reflections and resonance.

7. **Compressor** — 2.5-4:1 ratio, -10 to -14 dB threshold. Varies by preset.

**Exhaust Presets:**

| Preset     | LPF     | Sat Drive | Noise | Character            |
|------------|---------|-----------|-------|----------------------|
| Raw        | 20 kHz  | 1.0       | Off   | No processing        |
| Sport      | 8 kHz   | 2.0       | On    | Aggressive street    |
| Race       | 7 kHz   | 2.5       | On    | Open headers, raw    |
| Supercar   | 9 kHz   | 2.8       | Off   | Clean, high-revving  |
| Muffler    | 5.5 kHz | 1.8       | On    | Muffled OEM exhaust  |
| Custom     | User-defined parameters                            |

---

## RPM Profiles

Save and load target RPM configurations as JSON files in the `profiles/` directory.

A profile stores:
- Profile name
- Car name
- File prefix for WAV naming
- Output directory
- Target RPM list
- Sample rate and channel count

Profiles are useful when recording multiple configurations of the same car or when you want to reuse a proven RPM set.

---

## Configuration Reference

Settings are persisted in `settings.json` (next to the executable):

| Field | Type | Default | Description |
|---|---|---|---|
| SampleRate | int | 44100 | Recording sample rate |
| Channels | int | 2 | 1=mono, 2=stereo |
| LastProfile | string | "" | Last used RPM profile name |
| InteriorMode | bool | false | Apply cabin processing during recording |
| CarType | string | "Sedan" | Cabin acoustics preset |
| RecordLimiter | bool | false | Record a limiter WAV for accessories |
| GeneratePowerLut | bool | false | Generate power LUT (future feature) |
| Theme | string | "Dark" | UI theme ("Dark" or "Light") |
| ExteriorPreset | enum | Raw | Exhaust DSP preset |
| Exterior | object | defaults | Custom exterior DSP parameters |
| Interior | object | defaults | Custom interior DSP parameters |

---

## Troubleshooting

### "Cannot connect to FMOD Studio on 127.0.0.1:3663"
- FMOD Studio 1.08 must be running with a project open
- Verify the TCP console is enabled in FMOD Studio preferences
- Check that no firewall is blocking localhost connections

### "DLL injection failed"
- Run the recorder as Administrator
- Ensure Engine Simulator is running before clicking Connect
- Check that `es_hook.dll` exists and was built for the correct architecture (x64)
- Antivirus may block injection — whitelist the build directory

### "No RPM data received"
- The hook DLL may not have found the correct memory patterns
- Engine Simulator version may be incompatible with the current signatures
- Ensure the engine is actually running in the game

### Generated script contains ES6 errors
- The validator catches this automatically — check the validation output
- If manually editing scripts, remember: FMOD 1.08 only supports ES3
- Use `var` (not `const`/`let`), `function()` (not `=>`), string concatenation (not template literals)

### FMOD import creates events but no audio plays
- Check that WAV files were actually imported (Assets folder in FMOD project)
- Verify the RPM parameter range covers your engine's actual RPM span
- Check crossfade curves — if RPM gaps exist between samples, you'll get silence at those RPMs
- Ensure the FMOD event is referenced correctly in GUIDs.txt

### Bank file not found during post-build
- FMOD Studio must build the project before the bank file exists
- Check FMOD Studio's Build output directory
- The car name must match the FMOD bank naming convention

---

## License

This project is licensed under the **GNU General Public License v3.0** — see [LICENSE](LICENSE) for details.

---

## Credits

- [Engine Simulator](https://github.com/ange-yaghi/engine-sim) by Ange Yaghi
- [MinHook](https://github.com/TsudaKageworthy/minhook) — Windows API hooking library
- [NAudio](https://github.com/naudio/NAudio) — .NET audio library
- [WPF-UI](https://github.com/lepoco/wpfui) — Modern WPF UI framework
- [CommunityToolkit.Mvvm](https://github.com/CommunityToolkit/dotnet) — MVVM framework
