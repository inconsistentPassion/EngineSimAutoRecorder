using System.Collections.Generic;

namespace EngineSimRecorder.Core
{
    public sealed class RecorderConfig
    {
        public string OutputDir { get; set; } = "recordings";
        public int ProcessId { get; set; }
        public string ProcessName { get; set; } = "";
        public List<int> TargetRpms { get; set; } = new();
        public int RpmTolerance { get; set; } = 50;
        public int HoldSeconds { get; set; } = 5;
        public int RecordSeconds { get; set; } = 5;
        public double Kp { get; set; } = 0.005;
        public double Ki { get; set; } = 0.0001;
        public double Kd { get; set; } = 0.005;
    }
}
