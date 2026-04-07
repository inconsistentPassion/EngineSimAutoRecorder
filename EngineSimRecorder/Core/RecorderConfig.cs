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

        // Custom interior params
        public float CustomCutoffHz { get; set; } = 2000f;
        public float CustomRumbleHz { get; set; } = 80f;
        public float CustomRumbleDb { get; set; } = 6f;
        public float CustomRes1Hz { get; set; } = 180f;
        public float CustomRes1Db { get; set; } = 5f;
        public float CustomRes2Hz { get; set; } = 350f;
        public float CustomRes2Db { get; set; } = 4f;
        public float CustomStereoWidth { get; set; } = 0.3f;
        public float CustomReverbMix { get; set; } = 0.07f;
        public float CustomReverbMs { get; set; } = 30f;
        public float CustomCompRatio { get; set; } = 3f;
        public float CustomCompThreshDb { get; set; } = -12f;
    }
}
