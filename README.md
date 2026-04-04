# Engine Simulator Auto-Recorder

A set of tools that automate engine-audio recording from
[Engine Simulator](https://github.com/ange-yaghi/engine-sim) by using
AI-vision (OCR) to read the on-screen RPM counter and a PID controller
to hold the engine at each target RPM while the system audio is captured.
The resulting WAV files are ready to be imported into FMOD Studio and
blended for use in Assetto Corsa (or any other FMOD-based game).

Two implementations are provided:

| Tool | Language | OCR engine | Audio capture |
|------|----------|-----------|---------------|
| `EngineSimRecorder/` | C# / WinForms (.NET 6) | Tesseract 5 | NAudio WASAPI loopback |
| `python_version/engine_auto_recorder.py` | Python 3.9+ | PaddleOCR | sounddevice loopback |

---

### How it works

```
┌──────────────────────────────────────────────────────────────────┐
│  For each target RPM                                             │
│                                                                  │
│   1. OCR polls the RPM display every ~200 ms                     │
│   2. PID controller sends W / S keypresses to adjust throttle   │
│   3. Once RPM is stable for HOLD_SECONDS → begin recording       │
│   4. WASAPI / loopback capture saves rpm_<value>.wav             │
│   5. If RPM drifts during recording → re-stabilise, re-record   │
└──────────────────────────────────────────────────────────────────┘
```

---

## C# WinForms version (`EngineSimRecorder/`)

### Prerequisites

| Requirement | Notes |
|---|---|
| Windows 10/11 | WASAPI loopback requires Windows |
| [.NET 6 SDK](https://dotnet.microsoft.com/download/dotnet/6.0) | `dotnet` CLI must be on PATH |
| [Tesseract tessdata](https://github.com/tesseract-ocr/tessdata) | Download `eng.traineddata` |
| Engine Simulator (running) | The window must be visible |

NuGet packages are restored automatically on first build:

- **NAudio** 2.2.1 — WASAPI loopback capture
- **Tesseract** 5.2.0 — OCR bindings

### Install & build

```bash
# Clone the repo (if you haven't already)
git clone https://github.com/inconsistentPassion/MusicTheory.git
cd MusicTheory/EngineSimRecorder

# Restore packages and build
dotnet restore
dotnet build -c Release

# Run
dotnet run -c Release
```

### Tuning the OCR coordinates (C# version)

1. Launch Engine Simulator and load your engine.
2. Use a screen-ruler tool (e.g. [ShareX](https://getsharex.com/) → *Capture region*) to find the pixel
   bounding box of the RPM readout on your display.
3. In the application, set **X / Y / W / H** in the *OCR Region* group to those values.
4. Click **▶ Start** — the live *RPM:* label in the Status bar confirms the OCR is working.

### tessdata setup

```
EngineSimRecorder/
└── tessdata/
    └── eng.traineddata   ← download from https://github.com/tesseract-ocr/tessdata
```

Point the **tessdata folder** field in the UI at this directory (or anywhere
`eng.traineddata` lives).

### Running the C# tool

1. Set **Output Dir** to where you want WAV files saved.
2. Set **tessdata folder** (see above).
3. Set **OCR Region** (X / Y / W / H) to match the RPM counter on screen.
4. Add your target RPMs to the list (defaults: 800, 1500, 3000, 4500, 6000, 7500).
5. Adjust **Hold (seconds)** and **RPM Tolerance** as needed.
6. Click **▶ Start** — the tool will run through every RPM target and produce
   `rpm_<value>.wav` in your output folder.

---


### Configuration constants

All settings live at the top of `engine_auto_recorder.py`:

| Constant | Default | Description |
|---|---|---|
| `OCR_REGION` | `{left:860, top:45, width:160, height:40}` | Screen region containing RPM digits |
| `TARGET_RPMS` | `[800,1500,3000,4500,6000,7500]` | RPM values to record |
| `RPM_TOLERANCE` | `50` | ±RPM band considered stable |
| `HOLD_SECONDS` | `3.0` | Seconds stable before recording |
| `RECORD_SECONDS` | `6.0` | Duration of each WAV recording |
| `AUDIO_DEVICE` | `None` | sounddevice device name/index (None = default) |
| `OUTPUT_DIR` | `recordings/` | Output folder for WAV files |
| `KP / KI / KD` | `0.0005 / 0.00001 / 0.0005` | PID gains |

---

## PID gain tuning guide

The PID controller converts the error between target RPM and current RPM into
throttle key-press durations.

| Symptom | Fix |
|---|---|
| Engine overshoots and hunts | Reduce `Kp` |
| Engine is too slow to reach target | Increase `Kp` |
| Steady-state offset remains | Increase `Ki` |
| Oscillation / jitter | Reduce `Ki` or increase `Kd` |
| Noisy OCR readings cause spikes | Increase `RPM_TOLERANCE` |

---

## Output files

Each run produces one WAV file per target RPM:

```
recordings/
├── rpm_800.wav
├── rpm_1500.wav
├── rpm_3000.wav
├── rpm_4500.wav
├── rpm_6000.wav
└── rpm_7500.wav
```

All files are **44 100 Hz, 2-channel, 16-bit PCM** — ready to be imported
directly into FMOD Studio.

---

## Using the recordings in FMOD Studio / Assetto Corsa

1. Import WAVs into FMOD Studio.
2. Create a **parameter** named `RPM` (range 0 → redline).
3. Assign each sample to its RPM range and enable cross-fading.
4. Add pitch-shifting automation to the parameter curve for realism.
5. Build the `.bank` file and drop it into:
   ```
   assettocorsa/content/cars/<car_name>/sfx/
   ```

---

## Troubleshooting

| Problem | Solution |
|---|---|
| OCR always reads `???` | Check `OCR_REGION` coordinates; try the `--find-region` helper |
| No audio recorded | Enable *Stereo Mix* (Windows) or set the correct `AUDIO_DEVICE` |
| PID never stabilises | Check that Engine Simulator is the focused window; reduce `Kp` |
| `eng.traineddata` not found | Set the tessdata path in the UI / ensure the file is downloaded |
| PaddleOCR import error | Re-install: `pip install paddlepaddle paddleocr` |
