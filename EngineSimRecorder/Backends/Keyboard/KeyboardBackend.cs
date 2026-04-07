using System;
using System.Diagnostics;
using System.IO;
using System.IO.Pipes;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;
using EngineSimRecorder.Core;

namespace EngineSimRecorder.Backends.Keyboard
{
    /// <summary>
    /// Engine Simulator backend:
    ///   - DLL injection ONLY for reading exact RPM via named pipe
    ///   - ALL engine control via keyboard (PostMessage WM_KEYDOWN/WM_KEYUP)
    ///
    /// Engine Sim uses Delta Studio (DX11 + Win32 WinProc).
    /// PostMessage works because it goes through the standard Win32 message loop.
    ///
    /// Key bindings:
    ///   I = Ignition (toggle)
    ///   S = Starter (hold to crank)
    ///   D = Dyno (toggle)
    ///   H = Hold RPM in dyno mode (toggle)
    ///   R = Throttle (hold to rev)
    /// </summary>
    public sealed class KeyboardBackend : IEngineBackend
    {
   public string Name => "Keyboard + DLL RPM";

        // ?? Win32: DLL Injection ??????????????????????????????????????

        [DllImport("kernel32.dll")] private static extern IntPtr OpenProcess(uint a, bool b, int pid);
 [DllImport("kernel32.dll", CharSet = CharSet.Ansi)] private static extern IntPtr GetProcAddress(IntPtr h, string p);
        [DllImport("kernel32.dll", CharSet = CharSet.Auto)] private static extern IntPtr GetModuleHandle(string n);
        [DllImport("kernel32.dll", SetLastError = true)] private static extern IntPtr VirtualAllocEx(IntPtr h, IntPtr a, uint s, uint t, uint p);
     [DllImport("kernel32.dll", SetLastError = true)] private static extern bool WriteProcessMemory(IntPtr h, IntPtr a, byte[] b, uint s, out int w);
        [DllImport("kernel32.dll")] private static extern IntPtr CreateRemoteThread(IntPtr h, IntPtr a, uint s, IntPtr fp, IntPtr p, uint c, out IntPtr tid);
        [DllImport("kernel32.dll")] private static extern uint WaitForSingleObject(IntPtr h, uint ms);
        [DllImport("kernel32.dll")] private static extern bool GetExitCodeThread(IntPtr h, out uint exitCode);
        [DllImport("kernel32.dll", SetLastError = true)] private static extern bool VirtualFreeEx(IntPtr h, IntPtr a, uint s, uint t);
   [DllImport("kernel32.dll")] private static extern bool CloseHandle(IntPtr h);

        private const uint PROCESS_ALL_ACCESS = 0x001F0FFF;
        private const uint MEM_COMMIT_RESERVE = 0x00003000;
        private const uint PAGE_READWRITE = 4;

        // ?? Pipe protocol (RPM reading only) ?????????????????????????

  private const byte MSG_RPM_UPDATE = 0x01;
        private const byte MSG_MAX_RPM = 0x02;
        private const string PIPE_NAME = "es-recorder-pipe";

        // ?? State ?????????????????????????????????????????????????????

        /// <summary>The Engine Sim main window handle (for PostMessage).</summary>
    public IntPtr Hwnd { get; private set; }
        private Action<string>? _log;

        private NamedPipeClientStream? _pipe;
   private readonly object _pipeLock = new object();

        // Background RPM reader
  private Thread? _rpmReaderThread;
      private volatile bool _rpmReaderRunning;
  private double _latestRpm;
  private double _maxRpm;
        private readonly object _rpmLock = new object();

        // Throttle hold thread
        private Thread? _throttleThread;
      private volatile bool _throttleRunning;
   private volatile bool _throttleKeyHeld;
        private readonly object _throttleLock = new object();

        // ?? IEngineBackend ????????????????????????????????????????????

        public bool Initialize(RecorderConfig cfg, Action<string> log, CancellationToken ct)
        {
   _log = log;

   // 1. Find the Engine Sim window
Hwnd = KeyboardSim.FindMainWindow(cfg.ProcessId);
    if (Hwnd == IntPtr.Zero)
  {
  log("ERROR: Could not find Engine Sim window. Is it running?");
   return false;
      }
    log($"Found Engine Sim window: 0x{Hwnd:X}");

        // 2. Inject hook DLL for RPM reading
         string? dllPath = FindDll("es_hook.dll");
  if (dllPath == null)
    {
           log("ERROR: es_hook.dll not found. Build EngineSimHook first.");
             return false;
            }
         log($"Injecting {dllPath}...");
      if (!InjectDll(cfg.ProcessId, dllPath))
    return false;

   // 3. Connect pipe with retry — the DLL creates the pipe server,
   //    so we need to wait for it to be ready
   log("Connecting to hook pipe...");
  if (!ConnectPipeWithRetry(ct, maxAttempts: 10, delayMs: 500))
    return false;

// 4. Start background RPM reader
          _rpmReaderRunning = true;
 _rpmReaderThread = new Thread(RpmReaderLoop) { IsBackground = true, Name = "RPM-Reader" };
  _rpmReaderThread.Start();

            // 5. Start throttle hold thread
  _throttleRunning = true;
            _throttleKeyHeld = false;
     _throttleThread = new Thread(ThrottleHoldLoop) { IsBackground = true, Name = "Throttle-Hold" };
      _throttleThread.Start();

         // 6. Wait for first RPM readings
          ct.WaitHandle.WaitOne(1000);
    double? testRpm = ReadRpm();
     log($"Initial RPM: {(testRpm.HasValue ? testRpm.Value.ToString("F0") : "null")}");
    log("Backend ready - keyboard control + pipe RPM.");
     return true;
        }

    public double? ReadRpm()
        {
    lock (_rpmLock) { return _latestRpm > 0 ? _latestRpm : null; }
  }

    public double? ReadMaxRpm()
        {
    lock (_rpmLock) { return _maxRpm > 0 ? _maxRpm : null; }
  }

        /// <summary>
   /// Set throttle: 0 = release R key, >0 = hold R key down.
        /// The actual "analog" throttle doesn't exist in ES - it's binary (R held or not).
        /// Any value > 0.1 holds R down.
    /// </summary>
        public void SetThrottle(double throttle)
        {
    lock (_throttleLock)
            {
        _throttleKeyHeld = throttle > 0.1;
            }
    }

        public void StartEngine(Action<string> log, CancellationToken ct)
        {
            // Not used - Form1 drives the startup sequence directly
        }

     public void StopEngine()
        {
            SetThrottle(0);
          Thread.Sleep(100);
            // Toggle dyno off
         KeyboardSim.KeyPress(Hwnd, KeyboardSim.VK_D, 120);
       Thread.Sleep(200);
          // Toggle ignition off
     KeyboardSim.KeyPress(Hwnd, KeyboardSim.VK_A, 120);
        }

        public void Dispose()
        {
            // Stop throttle thread
            _throttleRunning = false;
            _throttleThread?.Join(2000);
       // Make sure R key is released
        KeyboardSim.KeyUp(Hwnd, KeyboardSim.VK_R);

      // Stop RPM reader
            _rpmReaderRunning = false;
            _rpmReaderThread?.Join(2000);
   try { _pipe?.Dispose(); } catch { }
        _pipe = null;
      }

  // Throttle hold thread
        // Engine Sim throttle (R key) is binary: held = full throttle, released = idle.
        // Send KeyDown once when throttle is requested, repeat every 200ms as a
        // safety net in case the game drops the key state. KeyUp on release.

        private void ThrottleHoldLoop()
        {
            bool wasHeld = false;

            while (_throttleRunning)
            {
                bool shouldHold;
                lock (_throttleLock) { shouldHold = _throttleKeyHeld; }

                if (shouldHold)
                {
                    if (!wasHeld)
                    {
                        KeyboardSim.KeyDown(Hwnd, KeyboardSim.VK_R);
                        wasHeld = true;
                    }
                    // Safety repeat every 200ms — not 20ms, that floods the input buffer
                    Thread.Sleep(200);
                }
                else
                {
                    if (wasHeld)
                    {
                        KeyboardSim.KeyUp(Hwnd, KeyboardSim.VK_R);
                        wasHeld = false;
                    }
                    Thread.Sleep(50);
                }
            }

            // Cleanup
            if (wasHeld)
                KeyboardSim.KeyUp(Hwnd, KeyboardSim.VK_R);
        }

  // RPM reader thread

        private void RpmReaderLoop()
        {
            byte[] buf = new byte[64];
            while (_rpmReaderRunning)
            {
                if (_pipe == null || !_pipe.IsConnected)
                {
                    Thread.Sleep(100);
                    continue;
                }
                try
                {
                    int n;
                    lock (_pipeLock)
                    {
                        if (_pipe == null || !_pipe.IsConnected) continue;
                        n = _pipe.Read(buf, 0, buf.Length);
                    }
                    if (n >= 9 && buf[0] == MSG_RPM_UPDATE)
                    {
                        double rpm = BitConverter.ToDouble(buf, 1);
                        lock (_rpmLock) { _latestRpm = rpm; }
                    }
                    else if (n >= 9 && buf[0] == MSG_MAX_RPM)
                    {
                        double maxRpm = BitConverter.ToDouble(buf, 1);
                        lock (_rpmLock) { _maxRpm = maxRpm; }
                        _log?.Invoke($"Engine redline detected: {maxRpm:F0} RPM");
                    }
                    else if (n == 0)
                    {
                        // Pipe disconnected
                        _log?.Invoke("RPM pipe disconnected.");
                        Thread.Sleep(100);
                    }
                }
                catch (IOException)
                {
                    // Pipe broken — will reconnect on next iteration
                    Thread.Sleep(100);
                }
                catch (Exception ex)
                {
                    _log?.Invoke($"RPM reader error: {ex.Message}");
                    Thread.Sleep(100);
                }
            }
        }

// Pipe connection (RPM only)

        private bool ConnectPipeWithRetry(CancellationToken ct, int maxAttempts = 10, int delayMs = 500)
        {
            for (int attempt = 1; attempt <= maxAttempts; attempt++)
            {
                if (ct.IsCancellationRequested) return false;
                _log?.Invoke($"Pipe connection attempt {attempt}/{maxAttempts}...");
                if (ConnectPipe(2000)) return true;
                if (attempt < maxAttempts) ct.WaitHandle.WaitOne(delayMs);
            }
            _log?.Invoke("ERROR: Could not connect to hook pipe after all attempts.");
            return false;
        }

        private bool ConnectPipe(int timeoutMs = 5000)
        {
            try
            {
                _pipe?.Dispose();
                _pipe = new NamedPipeClientStream(".", PIPE_NAME, PipeDirection.InOut, PipeOptions.None);
                _pipe.Connect(timeoutMs);
                _pipe.ReadMode = PipeTransmissionMode.Message;
                _log?.Invoke("Connected to pipe (message mode) for RPM");
                return true;
            }
            catch (Exception ex)
            {
                _log?.Invoke($"Pipe connection failed: {ex.Message}");
                return false;
            }
        }

  // ?? DLL Injection ?????????????????????????????????????????????

        private static string? FindDll(string name)
    {
            string[] candidates = {
         Path.Combine(AppDomain.CurrentDomain.BaseDirectory, name),
    Path.Combine(Environment.CurrentDirectory, name),
 Path.Combine(Environment.CurrentDirectory, "bin", name),
            };
            foreach (var p in candidates)
       if (File.Exists(p)) return p;
            return null;
        }

        private bool InjectDll(int pid, string dllPath)
   {
     string full = Path.GetFullPath(dllPath);
            if (!File.Exists(full)) { _log?.Invoke($"DLL not found: {full}"); return false; }

            IntPtr hProc = OpenProcess(PROCESS_ALL_ACCESS, false, pid);
            if (hProc == IntPtr.Zero) { _log?.Invoke("Failed to open process (run as admin?)"); return false; }

        IntPtr loadLib = GetProcAddress(GetModuleHandle("kernel32.dll"), "LoadLibraryA");
       byte[] pathBytes = Encoding.ASCII.GetBytes(full + '\0');
        IntPtr mem = VirtualAllocEx(hProc, IntPtr.Zero, (uint)pathBytes.Length, MEM_COMMIT_RESERVE, PAGE_READWRITE);
     WriteProcessMemory(hProc, mem, pathBytes, (uint)pathBytes.Length, out _);
            IntPtr thread = CreateRemoteThread(hProc, IntPtr.Zero, 0, loadLib, mem, 0, out _);
 if (thread == IntPtr.Zero)
    {
      _log?.Invoke("Failed to create remote thread");
    CloseHandle(hProc);
  return false;
     }

            // Wait for LoadLibraryA to complete and check result
            uint waitResult = WaitForSingleObject(thread, 10000);
            if (waitResult != 0x00000000) // WAIT_OBJECT_0
            {
                _log?.Invoke($"Remote thread wait failed: 0x{waitResult:X} (timeout or error)");
                CloseHandle(thread);
                VirtualFreeEx(hProc, mem, 0, 0x8000); // MEM_RELEASE
                CloseHandle(hProc);
                return false;
            }

            // LoadLibraryA returns HMODULE (non-zero on success)
            if (!GetExitCodeThread(thread, out uint exitCode) || exitCode == 0)
            {
                _log?.Invoke($"DLL init failed — LoadLibraryA returned 0x{exitCode:X} (bad path or missing deps?)");
                CloseHandle(thread);
                VirtualFreeEx(hProc, mem, 0, 0x8000);
                CloseHandle(hProc);
                return false;
            }

    VirtualFreeEx(hProc, mem, 0, 0x8000);
CloseHandle(thread);
            CloseHandle(hProc);
    _log?.Invoke($"DLL injected into PID {pid} (module base: 0x{exitCode:X})");
    return true;
}
    }
}
