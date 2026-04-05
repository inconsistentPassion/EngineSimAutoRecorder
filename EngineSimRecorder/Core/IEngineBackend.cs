using System;
using System.Threading;

namespace EngineSimRecorder.Core
{
    /// <summary>
    /// Abstraction for reading RPM and controlling the engine.
    ///
    /// InjectionBackend: DLL hook → memory read RPM, memory write throttle.
    /// OcrBackend:       PaddleOCR → screen capture RPM, SendInput → W/S keys.
    /// </summary>
    public interface IEngineBackend : IDisposable
    {
        string Name { get; }
        bool Initialize(RecorderConfig config, Action<string> log, CancellationToken ct);
        double? ReadRpm();
        void SetThrottle(double throttle);
        void StartEngine(Action<string> log, CancellationToken ct);
        void StopEngine();
    }
}
