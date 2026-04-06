# Engine Sim Auto-Recorder v1.0.0

## What's New

### Custom Filename Control
- **Car Name** field — prefix WAVs with your car name (e.g. `supra_3000_on.wav`)
- **Prefix** field — prepend any string to the filename (e.g. `ext_supra_3000_on.wav`)
- Default naming changed from `_load`/`_noload` to `_on`/`_off`
- Filename format: `{prefix}{carname}_{rpm}_on.wav` / `{prefix}{carname}_{rpm}_off.wav`

### Improved RPM Management
- **Quick preset buttons** — 1K through 8K, one-click to add
- **Sort** — order RPMs ascending
- **Clear All** — reset the list instantly
- **Right-click** any RPM to remove via context menu
- **Larger list box** — see more targets at a glance

### Auto-Open Output Folder
- Output folder automatically opens in Explorer when recording finishes
- Only triggers on successful completion (not on cancel or error)

### Single-File Publish
- `dotnet publish -c Release` produces just `EngineSimRecorder.exe` + `es_hook.dll` + `icon.ico`
- No debug symbols (PDB) in output
- All .NET dependencies bundled into the exe

### Custom App Icon
- New dark-themed icon with record button design
- Shows in title bar and taskbar

## Output

```
recordings/
├── ext_supra_1500_on.wav
├── ext_supra_1500_off.wav
├── ext_supra_3000_on.wav
├── ext_supra_3000_off.wav
├── ext_supra_6000_on.wav
└── ext_supra_6000_off.wav
```

All files: 44 100 Hz, stereo, float PCM — ready for FMOD Studio.

## Build

**Visual Studio:** Open `EngineSimAutoRecorder.sln` → Build

**Publish (single exe):**
```bash
dotnet publish -c Release
```

## Requirements
- Windows 10/11
- .NET 8 Runtime
- Visual Studio 2022 with C++ workload (for building)
- Engine Simulator running as admin
