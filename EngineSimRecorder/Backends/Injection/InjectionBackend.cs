using System;
using System.Diagnostics;
using System.IO;
using System.IO.Pipes;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;
using EngineSimRecorder.Core;

namespace EngineSimRecorder.Backends.Injection
{
    public sealed class InjectionBackend : IEngineBackend
    {
        public string Name => "Injection (DLL + Memory)";

        // ── Win32 ─────────────────────────────────────────────────────

        [DllImport("kernel32.dll")] private static extern IntPtr OpenProcess(uint a, bool b, int pid);
        [DllImport("kernel32.dll", CharSet = CharSet.Ansi)] private static extern IntPtr GetProcAddress(IntPtr h, string p);
        [DllImport("kernel32.dll", CharSet = CharSet.Auto)] private static extern IntPtr GetModuleHandle(string n);
        [DllImport("kernel32.dll", SetLastError = true)] private static extern IntPtr VirtualAllocEx(IntPtr h, IntPtr a, uint s, uint t, uint p);
        [DllImport("kernel32.dll", SetLastError = true)] private static extern bool WriteProcessMemory(IntPtr h, IntPtr a, byte[] b, uint s, out int w);
        [DllImport("kernel32.dll")] private static extern IntPtr CreateRemoteThread(IntPtr h, IntPtr a, uint s, IntPtr fp, IntPtr p, uint c, out IntPtr tid);
        [DllImport("kernel32.dll")] private static extern uint WaitForSingleObject(IntPtr h, uint ms);
        [DllImport("kernel32.dll", SetLastError = true)] private static extern bool VirtualFreeEx(IntPtr h, IntPtr a, uint s, uint t);
        [DllImport("kernel32.dll")] private static extern bool CloseHandle(IntPtr h);

        private const uint PROCESS_ALL_ACCESS = 0x001F0FFF;
        private const uint MEM_COMMIT_RESERVE = 0x00003000;
        private const uint PAGE_READWRITE = 4;

        // ── Pipe protocol ─────────────────────────────────────────────

        private const byte MSG_RPM_UPDATE   = 0x01;
        private const byte MSG_CMD_THROTTLE = 0x10;
        private const byte MSG_CMD_STARTER  = 0x11;
        private const byte MSG_CMD_IGNITION = 0x12;
        private const byte MSG_CMD_DYNO     = 0x13;
        private const string PIPE_NAME = "es-recorder-pipe";
        private const int RPM_SIZE = 9;

        // ── State ─────────────────────────────────────────────────────

        private NamedPipeClientStream? _pipe;
        private Action<string>? _log;

        public bool Initialize(RecorderConfig cfg, Action<string> log, CancellationToken ct)
        {
            _log = log;
            string dllPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "es_hook.dll");
            log($"Injecting {dllPath}...");
            if (!InjectDll(cfg.ProcessId, dllPath)) return false;

            log("Waiting for hook initialization...");
            ct.WaitHandle.WaitOne(3000);
            return ConnectPipe();
        }

        public double? ReadRpm()
        {
            if (_pipe == null || !_pipe.IsConnected) return null;
            try
            {
                byte[] buf = new byte[RPM_SIZE];
                int n = _pipe.Read(buf, 0, buf.Length);
                if (n >= RPM_SIZE && buf[0] == MSG_RPM_UPDATE)
                    return BitConverter.ToDouble(buf, 1);
            }
            catch { }
            return null;
        }

        public void SetThrottle(double throttle)
        {
            SendCmd(MSG_CMD_THROTTLE, throttle);
        }

        public void StartEngine(Action<string> log, CancellationToken ct)
        {
            log("Starting engine (injection mode)...");
            SendBoolCmd(MSG_CMD_IGNITION, true); log("Ignition ON");
            ct.WaitHandle.WaitOne(500);
            SendBoolCmd(MSG_CMD_DYNO, true); log("Dyno ON");
            ct.WaitHandle.WaitOne(500);
            SendBoolCmd(MSG_CMD_STARTER, true); log("Starter engaged");

            var sw = Stopwatch.StartNew();
            while (!ct.IsCancellationRequested)
            {
                double? rpm = ReadRpm();
                if (rpm.HasValue && rpm.Value > 200)
                {
                    log($"Engine started — RPM: {rpm.Value:F0}");
                    break;
                }
                if (sw.Elapsed.TotalSeconds > 15)
                {
                    log("Warning: engine didn't start in 15s");
                    break;
                }
                ct.WaitHandle.WaitOne(100);
            }

            SendBoolCmd(MSG_CMD_STARTER, false); log("Starter disengaged");
            ct.WaitHandle.WaitOne(500);
        }

        public void StopEngine()
        {
            SetThrottle(0);
            SendBoolCmd(MSG_CMD_DYNO, false);
            SendBoolCmd(MSG_CMD_IGNITION, false);
        }

        public void Dispose()
        {
            try { _pipe?.Dispose(); } catch { }
            _pipe = null;
        }

        // ── Helpers ───────────────────────────────────────────────────

        private bool InjectDll(int pid, string dllPath)
        {
            string full = Path.GetFullPath(dllPath);
            if (!File.Exists(full)) { _log?.Invoke($"DLL not found: {full}"); return false; }

            IntPtr hProc = OpenProcess(PROCESS_ALL_ACCESS, false, pid);
            if (hProc == IntPtr.Zero) { _log?.Invoke("Failed to open process (run as admin?)"); return false; }

            IntPtr loadLib = GetProcAddress(GetModuleHandle("kernel32.dll"), "LoadLibraryA");
            byte[] pathBytes = Encoding.ASCII.GetBytes(full);
            IntPtr mem = VirtualAllocEx(hProc, IntPtr.Zero, (uint)pathBytes.Length, MEM_COMMIT_RESERVE, PAGE_READWRITE);
            WriteProcessMemory(hProc, mem, pathBytes, (uint)pathBytes.Length, out _);
            IntPtr thread = CreateRemoteThread(hProc, IntPtr.Zero, 0, loadLib, mem, 0, out _);
            if (thread == IntPtr.Zero) { _log?.Invoke("Failed to create remote thread"); CloseHandle(hProc); return false; }

            WaitForSingleObject(thread, 5000);
            VirtualFreeEx(hProc, mem, (uint)pathBytes.Length, 0x8000);
            CloseHandle(hProc);
            _log?.Invoke($"DLL injected into PID {pid}");
            return true;
        }

        private bool ConnectPipe(int timeoutMs = 10000)
        {
            try
            {
                _pipe = new NamedPipeClientStream(".", PIPE_NAME, PipeDirection.InOut);
                _pipe.Connect(timeoutMs);
                _log?.Invoke("Connected to hook pipe");
                return true;
            }
            catch (Exception ex) { _log?.Invoke($"Pipe failed: {ex.Message}"); return false; }
        }

        private void SendCmd(byte type, double value)
        {
            if (_pipe == null || !_pipe.IsConnected) return;
            try
            {
                byte[] msg = new byte[9];
                msg[0] = type;
                BitConverter.GetBytes(value).CopyTo(msg, 1);
                _pipe.Write(msg, 0, msg.Length);
                _pipe.Flush();
            }
            catch { }
        }

        private void SendBoolCmd(byte type, bool enabled)
        {
            if (_pipe == null || !_pipe.IsConnected) return;
            try
            {
                byte[] msg = { type, enabled ? (byte)1 : (byte)0 };
                _pipe.Write(msg, 0, msg.Length);
                _pipe.Flush();
            }
            catch { }
        }
    }
}
