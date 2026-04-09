using System;
using System.Threading;

namespace EngineSimRecorder.Core
{
    /// <summary>
    /// Abstraction for reading RPM and controlling the engine.
    /// </summary>
    public interface IEngineBackend : IDisposable
    {
        string Name { get; }
        bool Initialize(RecorderConfig config, Action<string> log, CancellationToken ct);
        double? ReadRpm();
        double? ReadMaxRpm();
        double? ReadTorque();
        void SetThrottle(double throttle);
        void StopEngine();
    }
}
