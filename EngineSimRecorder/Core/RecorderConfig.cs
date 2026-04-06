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
    }
}
