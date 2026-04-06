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
        [DllImport("kernel32.dll", SetLastError = true)] private static extern bool VirtualFreeEx(IntPtr h, IntPtr a, uint s, uint t);
   [DllImport("kernel32.dll")] private static extern bool CloseHandle(IntPtr h);

        private const uint PROCESS_ALL_ACCESS = 0x001F0FFF;
        private const uint MEM_COMMIT_RESERVE = 0x00003000;
        private const uint PAGE_READWRITE = 4;

        // ?? Pipe protocol (RPM reading only) ?????????????????????????

  private const byte MSG_RPM_UPDATE = 0x01;
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

   log("Waiting for hook initialization (5s)...");
  ct.WaitHandle.WaitOne(5000);

         // 3. Connect pipe (for RPM data only)
    if (!ConnectPipe())
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

  // ?? Throttle hold thread ??????????????????????????????????????
        // Engine Sim throttle (R key) is binary: held = full throttle, released = idle.
 // This thread sends repeated WM_KEYDOWN when throttle is "on" so the
        // game sees the key as continuously held.

        private void ThrottleHoldLoop()
        {
      uint scanCode = 0;
   bool wasHeld = false;

            while (_throttleRunning)
     {
      bool shouldHold;
      lock (_throttleLock) { shouldHold = _throttleKeyHeld; }

  if (shouldHold)
       {
     if (!wasHeld)
             {
             // Initial key down
   KeyboardSim.KeyDown(Hwnd, KeyboardSim.VK_R);
     wasHeld = true;
  }
           else
    {
            // Repeat key down (simulates held key)
      KeyboardSim.KeyDown(Hwnd, KeyboardSim.VK_R);
        }
      Thread.Sleep(20); // ~50 repeats/sec
                }
       else
   {
       if (wasHeld)
   {
            KeyboardSim.KeyUp(Hwnd, KeyboardSim.VK_R);
            wasHeld = false;
              }
          Thread.Sleep(20);
    }
       }

            // Cleanup
 if (wasHeld)
  KeyboardSim.KeyUp(Hwnd, KeyboardSim.VK_R);
        }

  // ?? RPM reader thread ?????????????????????????????????????????

        private void RpmReaderLoop()
        {
            byte[] buf = new byte[64];
            while (_rpmReaderRunning)
            {
         if (_pipe == null || !_pipe.IsConnected) { Thread.Sleep(100); continue; }
        try
         {
   int n;
          lock (_pipeLock) { n = _pipe.Read(buf, 0, buf.Length); }
                    if (n >= 9 && buf[0] == MSG_RPM_UPDATE)
  {
            double rpm = BitConverter.ToDouble(buf, 1);
   lock (_rpmLock) { _latestRpm = rpm; }
   }
          }
          catch (Exception ex)
             {
 _log?.Invoke($"RPM reader error: {ex.Message}");
          Thread.Sleep(100);
      }
            }
        }

// ?? Pipe connection (RPM only) ????????????????????????????????

   private bool ConnectPipe(int timeoutMs = 15000)
        {
            try
   {
        _pipe = new NamedPipeClientStream(".", PIPE_NAME, PipeDirection.InOut, PipeOptions.None);
          _log?.Invoke("Connecting to hook pipe...");
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

            WaitForSingleObject(thread, 5000);
    VirtualFreeEx(hProc, mem, 0, 0x8000);
CloseHandle(thread);
            CloseHandle(hProc);
    _log?.Invoke($"DLL injected into PID {pid}");
    return true;
}
    }
}
