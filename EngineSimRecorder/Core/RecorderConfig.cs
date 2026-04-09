using System.Collections.Generic;

namespace EngineSimRecorder.Core
{
    public sealed class RecorderConfig
    {
        public string OutputDir { get; set; } = "recordings";
        public int ProcessId { get; set; }
        public string ProcessName { get; set; } = "";
        public string CustomName { get; set; } = "";
        public string CustomPrefix { get; set; } = "";
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
    }

    public sealed class ExteriorSettings
    {
        public float LpHz       { get; set; } = 8000f;
        public float LpQ        { get; set; } = 0.7f;
        public float HsHz       { get; set; } = 5000f;
        public float HsGainDb   { get; set; } = -3f;
        public float MidHz      { get; set; } = 150f;
        public float MidGainDb  { get; set; } = 3f;
        public float SatDrive   { get; set; } = 2.5f;
        public bool  EnableNoise { get; set; } = true;
        public float ReverbMs   { get; set; } = 25f;
        public float ReverbMix  { get; set; } = 0.10f;
        public float CompRatio  { get; set; } = 3f;
        public float CompThreshDb { get; set; } = -12f;
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
