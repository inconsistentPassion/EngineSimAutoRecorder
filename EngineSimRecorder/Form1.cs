using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.IO.Pipes;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;
using NAudio.Wave;

namespace EngineSimRecorder
{
    /// <summary>
    /// Engine Simulator Auto-Recorder
    ///
    /// Workflow:
    ///   1. Inject es_hook.dll into the Engine Simulator process.
    ///   2. The DLL hooks the ignition module to read RPM from memory
    ///      and starts a named pipe server.
    ///   3. This GUI connects to the pipe, receives RPM updates, and
    ///      runs a PID controller to hold each target RPM.
    ///   4. WASAPI loopback captures audio while RPM is stable.
    /// </summary>
    public partial class Form1 : Form
    {
        // ── Win32 P/Invoke for DLL injection ───────────────────────────

        [DllImport("kernel32.dll")]
        private static extern IntPtr OpenProcess(uint dwDesiredAccess, bool bInheritHandle, int dwProcessId);

        [DllImport("kernel32.dll", CharSet = CharSet.Ansi, SetLastError = true)]
        private static extern IntPtr GetProcAddress(IntPtr hModule, string procName);

        [DllImport("kernel32.dll", CharSet = CharSet.Auto)]
        private static extern IntPtr GetModuleHandle(string lpModuleName);

        [DllImport("kernel32.dll", SetLastError = true, ExactSpelling = true)]
        private static extern IntPtr VirtualAllocEx(IntPtr hProcess, IntPtr lpAddress, uint dwSize, uint flAllocationType, uint flProtect);

        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern bool WriteProcessMemory(IntPtr hProcess, IntPtr lpBaseAddress, byte[] lpBuffer, uint nSize, out int lpNumberOfBytesWritten);

        [DllImport("kernel32.dll")]
        private static extern IntPtr CreateRemoteThread(IntPtr hProcess, IntPtr lpThreadAttributes, uint dwStackSize,
            IntPtr lpStartAddress, IntPtr lpParameter, uint dwCreationFlags, out IntPtr lpThreadId);

        [DllImport("kernel32.dll")]
        private static extern uint WaitForSingleObject(IntPtr hHandle, uint dwMilliseconds);

        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern bool VirtualFreeEx(IntPtr hProcess, IntPtr lpAddress, uint dwSize, uint dwFreeType);

        [DllImport("kernel32.dll")]
        private static extern bool CloseHandle(IntPtr handle);

        private const uint PROCESS_ALL_ACCESS = 0x001F0FFF;
        private const uint MEM_COMMIT_RESERVE = 0x00003000;
        private const uint PAGE_READWRITE = 4;

        // ── Pipe protocol (must match C++ DLL) ────────────────────────

        private const byte MSG_RPM_UPDATE = 0x01;
        private const byte MSG_CMD_THROTTLE = 0x10;
        private const byte MSG_CMD_KILL = 0x11;

        private const string PIPE_NAME = "es-recorder-pipe";
        private const int RPM_STRUCT_SIZE = 9;  // 1 byte type + 8 byte double

        // ── State ─────────────────────────────────────────────────────

        private CancellationTokenSource? _cts;
        private Task? _workerTask;

        // ── Constructor ───────────────────────────────────────────────

        public Form1()
        {
            InitializeComponent();
            foreach (int rpm in new[] { 800, 1500, 3000, 4500, 6000, 7500 })
                lstTargetRpms.Items.Add(rpm);
        }

        // ── UI event handlers ─────────────────────────────────────────

        private void btnRefresh_Click(object sender, EventArgs e)
        {
            RefreshProcessList();
        }

        private void RefreshProcessList()
        {
            cmbProcess.Items.Clear();
            foreach (var proc in Process.GetProcessesByName("engine-sim"))
                cmbProcess.Items.Add(new ProcessItem(proc));
            foreach (var proc in Process.GetProcessesByName("engine_sim"))
                cmbProcess.Items.Add(new ProcessItem(proc));

            if (cmbProcess.Items.Count > 0)
                cmbProcess.SelectedIndex = 0;
        }

        private void btnBrowseOutput_Click(object sender, EventArgs e)
        {
            using var dlg = new FolderBrowserDialog { Description = "Select output folder for WAV recordings" };
            if (dlg.ShowDialog() == DialogResult.OK)
                txtOutputDir.Text = dlg.SelectedPath;
        }

        private void btnAddRpm_Click(object sender, EventArgs e)
        {
            int rpm = (int)numRpmList.Value;
            if (!lstTargetRpms.Items.Contains(rpm))
                lstTargetRpms.Items.Add(rpm);
        }

        private void btnRemoveRpm_Click(object sender, EventArgs e)
        {
            if (lstTargetRpms.SelectedItem != null)
                lstTargetRpms.Items.Remove(lstTargetRpms.SelectedItem);
        }

        private void btnStart_Click(object sender, EventArgs e)
        {
            if (lstTargetRpms.Items.Count == 0)
            {
                MessageBox.Show("Add at least one target RPM before starting.", "No targets",
                    MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            if (cmbProcess.SelectedItem is not ProcessItem selected)
            {
                MessageBox.Show("Select an Engine Simulator process first.", "No process",
                    MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            string outputDir = txtOutputDir.Text.Trim();
            Directory.CreateDirectory(outputDir);

            var targets = new List<int>();
            foreach (var item in lstTargetRpms.Items)
                targets.Add(Convert.ToInt32(item));

            var cfg = new RecorderConfig
            {
                OutputDir = outputDir,
                ProcessId = selected.ProcessId,
                TargetRpms = targets,
                RpmTolerance = (int)numRpmTol.Value,
                HoldSeconds = (int)numHoldSec.Value,
                RecordSeconds = (int)numRecordSec.Value,
                Kp = (double)numKp.Value,
                Ki = (double)numKi.Value,
                Kd = (double)numKd.Value,
            };

            btnStart.Enabled = false;
            btnStop.Enabled = true;
            btnRefresh.Enabled = false;
            pbarProgress.Value = 0;
            pbarProgress.Maximum = targets.Count;
            txtLog.Clear();

            _cts = new CancellationTokenSource();
            _workerTask = Task.Run(() => RunAsync(cfg, _cts.Token));
        }

        private void btnStop_Click(object sender, EventArgs e)
        {
            _cts?.Cancel();
        }

        private void Form1_FormClosing(object sender, FormClosingEventArgs e)
        {
            _cts?.Cancel();
        }

        // ── Thread-safe UI helpers ─────────────────────────────────────

        private void Log(string message)
        {
            string line = $"[{DateTime.Now:HH:mm:ss}] {message}";
            if (InvokeRequired)
                BeginInvoke(new Action(() => AppendLog(line)));
            else
                AppendLog(line);
        }

        private void AppendLog(string line)
        {
            txtLog.AppendText(line + Environment.NewLine);
            txtLog.ScrollToCaret();
        }

        private void SetStatus(string text) =>
            BeginInvoke(new Action(() => lblStatus.Text = text));

        private void SetRpm(string text) =>
            BeginInvoke(new Action(() => lblCurrentRpm.Text = text));

        private void IncProgress() =>
            BeginInvoke(new Action(() => pbarProgress.Value = Math.Min(pbarProgress.Value + 1, pbarProgress.Maximum)));

        private void ResetControls() =>
            BeginInvoke(new Action(() =>
            {
                btnStart.Enabled = true;
                btnStop.Enabled = false;
                btnRefresh.Enabled = true;
                lblStatus.Text = "Done.";
            }));

        // ── DLL injection ──────────────────────────────────────────────

        private bool InjectDll(int pid, string dllPath)
        {
            string fullDllPath = Path.GetFullPath(dllPath);
            if (!File.Exists(fullDllPath))
            {
                Log($"DLL not found: {fullDllPath}");
                return false;
            }

            IntPtr hProc = OpenProcess(PROCESS_ALL_ACCESS, false, pid);
            if (hProc == IntPtr.Zero)
            {
                Log("Failed to open process (run as admin?)");
                return false;
            }

            IntPtr loadLibAddr = GetProcAddress(GetModuleHandle("kernel32.dll"), "LoadLibraryA");

            byte[] pathBytes = Encoding.ASCII.GetBytes(fullDllPath);
            IntPtr allocatedMem = VirtualAllocEx(hProc, IntPtr.Zero, (uint)pathBytes.Length, MEM_COMMIT_RESERVE, PAGE_READWRITE);

            WriteProcessMemory(hProc, allocatedMem, pathBytes, (uint)pathBytes.Length, out _);

            IntPtr threadId;
            IntPtr threadHandle = CreateRemoteThread(hProc, IntPtr.Zero, 0, loadLibAddr, allocatedMem, 0, out threadId);
            if (threadHandle == IntPtr.Zero)
            {
                Log("Failed to create remote thread");
                CloseHandle(hProc);
                return false;
            }

            WaitForSingleObject(threadHandle, 5000);
            VirtualFreeEx(hProc, allocatedMem, (uint)pathBytes.Length, 0x8000); // MEM_RELEASE
            CloseHandle(hProc);

            Log($"DLL injected into PID {pid}");
            return true;
        }

        // ── Pipe communication ─────────────────────────────────────────

        private NamedPipeClientStream? _pipe;

        private bool ConnectPipe(int timeoutMs = 10000)
        {
            try
            {
                _pipe = new NamedPipeClientStream(".", PIPE_NAME, PipeDirection.InOut);
                _pipe.Connect(timeoutMs);
                _pipe.ReadMode = PipeTransmissionMode.Byte;
                Log("Connected to hook pipe");
                return true;
            }
            catch (Exception ex)
            {
                Log($"Pipe connection failed: {ex.Message}");
                return false;
            }
        }

        private double? ReadRpmFromPipe()
        {
            if (_pipe == null || !_pipe.IsConnected) return null;

            try
            {
                byte[] buf = new byte[RPM_STRUCT_SIZE];
                int bytesRead = _pipe.Read(buf, 0, buf.Length);

                if (bytesRead >= RPM_STRUCT_SIZE && buf[0] == MSG_RPM_UPDATE)
                {
                    double rpm = BitConverter.ToDouble(buf, 1);
                    return rpm;
                }
            }
            catch (TimeoutException) { }
            catch (IOException) { }

            return null;
        }

        private void SendThrottleCommand(double throttle)
        {
            if (_pipe == null || !_pipe.IsConnected) return;

            try
            {
                byte[] msg = new byte[9];
                msg[0] = MSG_CMD_THROTTLE;
                BitConverter.GetBytes(throttle).CopyTo(msg, 1);
                _pipe.Write(msg, 0, msg.Length);
                _pipe.Flush();
            }
            catch (IOException) { }
        }

        private void DisconnectPipe()
        {
            try
            {
                _pipe?.Dispose();
                _pipe = null;
            }
            catch { }
        }

        // ── Main worker ───────────────────────────────────────────────

        private void RunAsync(RecorderConfig cfg, CancellationToken ct)
        {
            try
            {
                // 1. Inject the hook DLL
                string dllPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "es_hook.dll");
                Log($"Injecting {dllPath}...");
                if (!InjectDll(cfg.ProcessId, dllPath))
                {
                    Log("Injection failed — aborting.");
                    return;
                }

                // 2. Wait for DLL to initialize, then connect to pipe
                Log("Waiting for hook to initialize...");
                Thread.Sleep(3000);

                if (!ConnectPipe())
                {
                    Log("Could not connect to hook pipe — aborting.");
                    return;
                }

                // 3. Process each RPM target
                for (int i = 0; i < cfg.TargetRpms.Count; i++)
                {
                    if (ct.IsCancellationRequested) break;

                    int targetRpm = cfg.TargetRpms[i];
                    Log($"── Target {i + 1}/{cfg.TargetRpms.Count}: {targetRpm} RPM ──");
                    SetStatus($"Approaching {targetRpm} RPM…");

                    HoldRpm(cfg, targetRpm, ct);
                    if (ct.IsCancellationRequested) break;

                    string wavPath = Path.Combine(cfg.OutputDir, $"rpm_{targetRpm}.wav");
                    Log($"Recording → {wavPath}");
                    SetStatus($"Recording at {targetRpm} RPM…");
                    RecordWasapi(wavPath, cfg, targetRpm, ct);
                    IncProgress();
                    Log($"Saved: {wavPath}");
                }

                // 4. Release throttle override
                SendThrottleCommand(0.0);
            }
            catch (OperationCanceledException)
            {
                Log("Stopped by user.");
            }
            catch (Exception ex)
            {
                Log($"ERROR: {ex.Message}");
                BeginInvoke(new Action(() =>
                    MessageBox.Show(ex.Message, "Error", MessageBoxButtons.OK, MessageBoxIcon.Error)));
            }
            finally
            {
                DisconnectPipe();
                ResetControls();
            }
        }

        // ── PID-based RPM holding ─────────────────────────────────────

        private void HoldRpm(RecorderConfig cfg, int targetRpm, CancellationToken ct)
        {
            double integral = 0;
            double prevError = 0;
            const int loopMs = 50; // 20Hz PID loop
            DateTime stableStart = DateTime.MinValue;
            bool wasStable = false;

            while (!ct.IsCancellationRequested)
            {
                double? rpm = ReadRpmFromPipe();
                if (!rpm.HasValue)
                {
                    Thread.Sleep(loopMs);
                    continue;
                }

                int currentRpm = (int)rpm.Value;
                SetRpm($"RPM: {currentRpm}");

                double error = targetRpm - currentRpm;
                integral += error * (loopMs / 1000.0);
                double derivative = (error - prevError) / (loopMs / 1000.0);
                prevError = error;
                double output = cfg.Kp * error + cfg.Ki * integral + cfg.Kd * derivative;

                // Clamp output to 0-1 throttle range
                double throttle = Math.Clamp(output, 0.0, 1.0);
                SendThrottleCommand(throttle);

                bool stable = Math.Abs(error) <= cfg.RpmTolerance;
                if (stable)
                {
                    if (!wasStable) stableStart = DateTime.UtcNow;
                    wasStable = true;
                    double heldFor = (DateTime.UtcNow - stableStart).TotalSeconds;
                    SetStatus($"Stable at {currentRpm} RPM – hold {heldFor:F1}s / {cfg.HoldSeconds}s");
                    if (heldFor >= cfg.HoldSeconds) break;
                }
                else
                {
                    wasStable = false;
                    stableStart = DateTime.MinValue;
                }

                Thread.Sleep(loopMs);
            }
        }

        // ── WASAPI loopback recording ─────────────────────────────────

        private void RecordWasapi(string outputPath, RecorderConfig cfg,
                                   int targetRpm, CancellationToken ct)
        {
            using var capture = new WasapiLoopbackCapture();
            capture.WaveFormat = WaveFormat.CreateIeeeFloatWaveFormat(44100, 2);

            using var writer = new WaveFileWriter(outputPath, capture.WaveFormat);
            var recordingEnd = new ManualResetEventSlim(false);
            DateTime startTime = DateTime.UtcNow;

            capture.DataAvailable += (s, e) =>
            {
                if (e.BytesRecorded > 0)
                    writer.Write(e.Buffer, 0, e.BytesRecorded);
            };

            capture.RecordingStopped += (s, e) => recordingEnd.Set();

            capture.StartRecording();
            Log($"WASAPI capture started ({cfg.RecordSeconds}s)");

            while (!ct.IsCancellationRequested)
            {
                double elapsed = (DateTime.UtcNow - startTime).TotalSeconds;
                if (elapsed >= cfg.RecordSeconds) break;

                double? rpm = ReadRpmFromPipe();
                if (rpm.HasValue)
                {
                    int currentRpm = (int)rpm.Value;
                    SetRpm($"RPM: {currentRpm}");
                    if (Math.Abs(currentRpm - targetRpm) > cfg.RpmTolerance * 2)
                    {
                        Log($"RPM drifted to {currentRpm} – rebalancing…");
                        HoldRpm(cfg, targetRpm, ct);
                        startTime = DateTime.UtcNow;
                    }
                }

                Thread.Sleep(50);
            }

            capture.StopRecording();
            recordingEnd.Wait(TimeSpan.FromSeconds(5));
        }

        // ── Config DTO ────────────────────────────────────────────────

        private sealed class RecorderConfig
        {
            public string OutputDir { get; set; } = "recordings";
            public int ProcessId { get; set; }
            public List<int> TargetRpms { get; set; } = new();
            public int RpmTolerance { get; set; } = 50;
            public int HoldSeconds { get; set; } = 5;
            public int RecordSeconds { get; set; } = 6;
            public double Kp { get; set; } = 0.005;
            public double Ki { get; set; } = 0.0001;
            public double Kd { get; set; } = 0.005;
        }

        // ── Process list item ─────────────────────────────────────────

        private sealed class ProcessItem
        {
            public int ProcessId { get; }
            public string DisplayName { get; }

            public ProcessItem(Process proc)
            {
                ProcessId = proc.Id;
                DisplayName = $"{proc.ProcessName} (PID {proc.Id})";
            }

            public override string ToString() => DisplayName;
        }
    }
}
