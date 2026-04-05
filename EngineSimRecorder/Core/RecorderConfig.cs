using System.Collections.Generic;
using System.Drawing;

namespace EngineSimRecorder.Core
{
    public sealed class RecorderConfig
    {
        public string OutputDir { get; set; } = "recordings";
        public int ProcessId { get; set; }
        public string ProcessName { get; set; } = "";
        public Rectangle OcrRegion { get; set; } = new(860, 45, 160, 40);
        public List<int> TargetRpms { get; set; } = new();
        public int RpmTolerance { get; set; } = 50;
        public int HoldSeconds { get; set; } = 3;
        public int RecordSeconds { get; set; } = 6;
        public double Kp { get; set; } = 0.005;
        public double Ki { get; set; } = 0.0001;
        public double Kd { get; set; } = 0.005;
        public BackendMode Mode { get; set; } = BackendMode.Injection;
    }

    public enum BackendMode
    {
        /// <summary>DLL injection + memory read/write. Precise, requires admin.</summary>
        Injection,

        /// <summary>OCR + SendInput keystrokes. Non-invasive, no admin needed.</summary>
        Ocr
    }
}
