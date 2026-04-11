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

        /// <summary>RPM-aware processing scaling (optional, off by default).</summary>
        public RpmProcessingSettings RpmProcessing { get; set; } = new();
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

        // ── Psychoacoustic enhancement ────────────────────────────────────
        /// <summary>Mud cut: narrow EQ dip at this frequency in Hz (0 = disabled).</summary>
        public float MudCutHz { get; set; } = 350f;

        /// <summary>Mud cut gain in dB (negative = cut). Default -3dB.</summary>
        public float MudCutDb { get; set; } = -3f;

        /// <summary>Mud cut Q (higher = narrower). Default 2.0.</summary>
        public float MudCutQ { get; set; } = 2.0f;

        /// <summary>Character band boost center frequency in Hz (0 = disabled). Engine "voice".</summary>
        public float CharacterHz { get; set; } = 1500f;

        /// <summary>Character band boost gain in dB. Default +2dB.</summary>
        public float CharacterDb { get; set; } = 2f;

        /// <summary>Character band boost Q. Default 1.2.</summary>
        public float CharacterQ { get; set; } = 1.2f;

        /// <summary>Transient shaper attack boost in dB (0 = disabled). Default +3dB.</summary>
        public float TransientBoostDb { get; set; } = 3f;

        /// <summary>Harmonic exciter center frequency in Hz (0 = disabled).</summary>
        public float ExciterHz { get; set; } = 3000f;

        /// <summary>Harmonic exciter bandwidth in Hz.</summary>
        public float ExciterBandwidthHz { get; set; } = 3000f;

        /// <summary>Harmonic exciter drive (1.5-4.0).</summary>
        public float ExciterDrive { get; set; } = 2.5f;

        /// <summary>Harmonic exciter wet mix (0.05-0.25).</summary>
        public float ExciterMix { get; set; } = 0.15f;
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

        // ── Psychoacoustic enhancement ────────────────────────────────────
        /// <summary>Character band boost gain in dB (0 = disabled). Adds engine "voice" through cabin.</summary>
        public float CharacterBoostDb { get; set; } = 2f;

        /// <summary>Character band center frequency in Hz.</summary>
        public float CharacterHz { get; set; } = 1800f;

        /// <summary>Subtle saturation drive (1.0 = none, 1.1-1.3 = warm cabin saturation).</summary>
        public float SatDrive { get; set; } = 1.15f;
    }

    /// <summary>
    /// RPM-aware processing overrides. When enabled, the processor scales its
    /// parameters based on the recording's target RPM instead of using static values.
    /// All ranges are [lowRpmValue, highRpmValue] — the processor linearly interpolates.
    /// </summary>
    public sealed class RpmProcessingSettings
    {
        public bool Enabled { get; set; } = false;

        /// <summary>Minimum RPM for scaling (typically 800-1000).</summary>
        public int MinRpm { get; set; } = 800;

        /// <summary>Maximum RPM for scaling (typically 7000-9000).</summary>
        public int MaxRpm { get; set; } = 8000;

        // ── Exterior RPM scaling ──────────────────────────────────────────
        /// <summary>Character band Hz at [minRpm, maxRpm]. Shifts up with RPM.</summary>
        public float[] CharacterHzRange { get; set; } = { 1200f, 2500f };

        /// <summary>Saturation drive at [minRpm, maxRpm]. More grit at high RPM.</summary>
        public float[] SatDriveRange { get; set; } = { 1.2f, 1.8f };

        /// <summary>Transient shaper boost dB at [minRpm, maxRpm]. More punch at low RPM.</summary>
        public float[] TransientBoostRange { get; set; } = { 4f, 2f };

        /// <summary>Mud cut Hz at [minRpm, maxRpm]. Shifts up with RPM.</summary>
        public float[] MudCutHzRange { get; set; } = { 300f, 420f };

        /// <summary>Exciter wet mix at [minRpm, maxRpm]. More bite at high RPM.</summary>
        public float[] ExciterMixRange { get; set; } = { 0.10f, 0.20f };

        // ── Interior RPM scaling ──────────────────────────────────────────
        /// <summary>LPF cutoff Hz at [minRpm, maxRpm]. Opens up at high RPM.</summary>
        public float[] LpfCutoffRange { get; set; } = { 1800f, 2800f };

        /// <summary>Rumble boost dB at [minRpm, maxRpm]. Stronger at idle.</summary>
        public float[] RumbleBoostRange { get; set; } = { 7f, 4f };

        /// <summary>Compressor threshold dB at [minRpm, maxRpm]. Less compression at high RPM.</summary>
        public float[] CompThreshRange { get; set; } = { -14f, -8f };

        /// <summary>Interpolate a 2-element range by RPM position. 0 = minRpm, 1 = maxRpm.</summary>
        public float Lerp(float[] range, int rpm)
        {
            if (range == null || range.Length < 2) return 0f;
            float t = Math.Clamp((float)(rpm - MinRpm) / Math.Max(1, MaxRpm - MinRpm), 0f, 1f);
            return range[0] + (range[1] - range[0]) * t;
        }
    }
}
