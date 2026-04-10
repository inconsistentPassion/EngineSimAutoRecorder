using System.Collections.Generic;
using EngineSimRecorder.Services;

namespace EngineSimRecorder.Core
{
    public sealed class RecorderConfig
    {
        public string OutputDir { get; set; } = "recordings";
        public int ProcessId { get; set; }
        public string CarName { get; set; } = "";
        public string Prefix { get; set; } = "";
        public List<int> TargetRpms { get; set; } = new();
        public int RpmTolerance { get; set; } = 5;
        public int HoldSeconds { get; set; } = 5;
        public int RecordSeconds { get; set; } = 5;
        public int SampleRate { get; set; } = 44100;
        public int Channels { get; set; } = 2; // 1=mono, 2=stereo
        public bool InteriorMode { get; set; } = false;
        public string CarType { get; set; } = "Sedan";
        public bool RecordLimiter { get; set; } = false;
        public bool GeneratePowerLut { get; set; } = false;

        /// <summary>Custom interior acoustics parameters (only used when CarType == "Custom").</summary>
        public InteriorSettings Interior { get; set; } = new();

        /// <summary>Exterior DSP preset. Raw = no processing (default).</summary>
        public ExteriorPreset ExteriorPreset { get; set; } = ExteriorPreset.Raw;

        /// <summary>Custom exterior DSP parameters (only used when ExteriorPreset == Custom).</summary>
        public ExteriorSettings Exterior { get; set; } = new();

        // ── FMOD / AC automation (persisted via AppSettings.Automation) ─────────

        public string FmodProjectPath { get; set; } = "";
        public string AcContentPath { get; set; } = "";
        public string RecordingsDirExt { get; set; } = "";
        public string RecordingsDirInt { get; set; } = "";
        public string RecordingsDirLimiter { get; set; } = "";
        public FmodGenerationMode FmodGenerationMode { get; set; } = FmodGenerationMode.UseExistingTemplate;
    }

    public sealed class ExteriorSettings
    {
        // Optimized for realistic, natural-sounding exterior recordings
        public float LpHz       { get; set; } = 14000f;  // Gentle air absorption
        public float LpQ        { get; set; } = 0.65f;
        public float HsHz       { get; set; } = 8000f;   // Natural HF roll-off
        public float HsGainDb   { get; set; } = -2f;
        public float MidHz      { get; set; } = 180f;    // Warmth/body frequency
        public float MidGainDb  { get; set; } = 2.5f;
        public float SatDrive   { get; set; } = 1.4f;    // Gentle tape saturation
        public bool  EnableNoise { get; set; } = false;  // Disabled - adds unwanted static
        public float ReverbMs   { get; set; } = 25f;     // Natural space simulation
        public float ReverbMix  { get; set; } = 0.06f;   // Subtle reverb
        public float CompRatio  { get; set; } = 1.8f;    // Transparent compression
        public float CompThreshDb { get; set; } = -16f;
    }

    public sealed class InteriorSettings
    {
        public float CutoffHz { get; set; } = 2000f;
        public float RumbleHz { get; set; } = 80f;
        public float RumbleDb { get; set; } = 6f;
        public float Res1Hz { get; set; } = 180f;
        public float Res1Db { get; set; } = 5f;
        public float Res2Hz { get; set; } = 350f;
        public float Res2Db { get; set; } = 4f;
        public float StereoWidth { get; set; } = 0.3f;
        public float ReverbMix { get; set; } = 0.07f;
        public float ReverbMs { get; set; } = 30f;
        public float CompRatio { get; set; } = 3f;
        public float CompThreshDb { get; set; } = -12f;
    }
}
