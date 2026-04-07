using System;
using System.Threading;

namespace EngineSimRecorder.Core
{
    /// <summary>
    /// Abstraction for reading RPM and controlling the engine.
    ///
    /// InjectionBackend: DLL hook → memory read RPM, memory write throttle.
    /// </summary>
    public interface IEngineBackend : IDisposable
    {
        string Name { get; }
        bool Initialize(RecorderConfig config, Action<string> log, CancellationToken ct);
        double? ReadRpm();
        double? ReadMaxRpm();
        void SetThrottle(double throttle);
        void StartEngine(Action<string> log, CancellationToken ct);
        void StopEngine();
    }
}
