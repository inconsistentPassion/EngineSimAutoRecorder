using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;
using EngineSimRecorder.Backends.Keyboard;
using EngineSimRecorder.Core;
using NAudio.Wave;

namespace EngineSimRecorder
{
    public partial class Form1 : Form
    {
        private CancellationTokenSource? _cts;
        private Task? _workerTask;
        private System.Windows.Forms.Timer? _focusMonitor;
        private IntPtr _engineSimHwnd = IntPtr.Zero;
        private bool _focusWarned = false;

        public Form1()
        {
            InitializeComponent();
            // Set application icon for title bar and taskbar
            string iconPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "icon.ico");
            if (File.Exists(iconPath))
            {
                var ico = new Icon(iconPath);
                this.Icon = ico;
            }
            if (System.ComponentModel.LicenseManager.UsageMode != System.ComponentModel.LicenseUsageMode.Designtime)
            {
                foreach (int rpm in new[] { 1500, 2000, 3000, 4000, 5000, 6000 })
                    lstTargetRpms.Items.Add(rpm);
            }

            // change window title here
            this.Text = "Engine Sim Recorder 1.0.0";
        }

        // ── UI events ──────────────────────────────────────────────────

        private void btnRefresh_Click(object sender, EventArgs e) => RefreshProcessList();

        private void RefreshProcessList()
        {
            cmbProcess.Items.Clear();
            foreach (var name in new[] { "engine-sim-app", "engine-sim", "engine_sim", "EngineSimulator" })
                foreach (var proc in Process.GetProcessesByName(name))
                    cmbProcess.Items.Add(new ProcessItem(proc));
            if (cmbProcess.Items.Count > 0) cmbProcess.SelectedIndex = 0;
        }

        private void btnBrowseOutput_Click(object sender, EventArgs e)
        {
            using var dlg = new FolderBrowserDialog { Description = "Select output folder" };
            if (dlg.ShowDialog() == DialogResult.OK) txtOutputDir.Text = dlg.SelectedPath;
        }

        private void btnOpenOutput_Click(object sender, EventArgs e)
        {
            string dir = txtOutputDir.Text.Trim();
            if (!Directory.Exists(dir))
            {
                Directory.CreateDirectory(dir);
            }
            Process.Start("explorer.exe", dir);
        }

        private void btnAddRpm_Click(object sender, EventArgs e)
        {
            int rpm = (int)numRpmList.Value;
            if (!lstTargetRpms.Items.Contains(rpm)) lstTargetRpms.Items.Add(rpm);
        }

        private void btnRemoveRpm_Click(object sender, EventArgs e)
        {
            if (lstTargetRpms.SelectedItem != null) lstTargetRpms.Items.Remove(lstTargetRpms.SelectedItem);
        }

        private void btnPreset_Click(object? sender, EventArgs e)
        {
            if (sender is Button btn && int.TryParse(btn.Text.Replace("K", "000"), out int rpm))
            {
                if (!lstTargetRpms.Items.Contains(rpm)) lstTargetRpms.Items.Add(rpm);
            }
        }

        private void btnSortRpm_Click(object sender, EventArgs e)
        {
            var items = new List<int>();
            foreach (var item in lstTargetRpms.Items) items.Add(Convert.ToInt32(item));
            items.Sort();
            lstTargetRpms.Items.Clear();
            foreach (var rpm in items) lstTargetRpms.Items.Add(rpm);
        }

        private void btnClearRpm_Click(object sender, EventArgs e)
        {
            lstTargetRpms.Items.Clear();
        }

        private void btnStart_Click(object sender, EventArgs e)
        {
            if (lstTargetRpms.Items.Count == 0)
            {
                MessageBox.Show("Add at least one target RPM.", "No targets",
                           MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            if (cmbProcess.SelectedItem is not ProcessItem sel)
            {
                MessageBox.Show("Select an Engine Simulator process.", "No process",
                          MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            string outputDir = txtOutputDir.Text.Trim();
            Directory.CreateDirectory(outputDir);

            var targets = new List<int>();
            foreach (var item in lstTargetRpms.Items) targets.Add(Convert.ToInt32(item));
            targets.Sort();

            var cfg = new RecorderConfig
            {
                OutputDir = outputDir,
                ProcessId = sel.ProcessId,
                TargetRpms = targets,
                CustomName = txtCarName.Text.Trim(),
                CustomPrefix = txtPrefix.Text.Trim(),
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

        private void btnStop_Click(object sender, EventArgs e) => _cts?.Cancel();
        private void Form1_FormClosing(object sender, FormClosingEventArgs e) => _cts?.Cancel();

        private void chkStayOnTop_CheckedChanged(object sender, EventArgs e)
        {
            this.TopMost = chkStayOnTop.Checked;
        }

        // ── UI helpers ─────────────────────────────────────────────────

        private void Log(string msg)
        {
            string line = $"[{DateTime.Now:HH:mm:ss}] {msg}";
            if (InvokeRequired) BeginInvoke(new Action(() => AppendLog(line)));
            else AppendLog(line);
        }
        private void AppendLog(string line)
        {
            txtLog.AppendText(line + Environment.NewLine);
            txtLog.ScrollToCaret();
        }
        private void SetStatus(string t) => BeginInvoke(new Action(() => lblStatus.Text = t));
        private void SetRpm(string t) => BeginInvoke(new Action(() => lblCurrentRpm.Text = t));
        private void IncProgress() => BeginInvoke(new Action(() =>
       pbarProgress.Value = Math.Min(pbarProgress.Value + 1, pbarProgress.Maximum)));
        private void ResetControls() => BeginInvoke(new Action(() =>
        {
            btnStart.Enabled = true; btnStop.Enabled = false;
            btnRefresh.Enabled = true;
            lblStatus.Text = "Done.";
        }));

        // ════════════════════════════════════════════════════════════════
        //  WORKFLOW (all keyboard, DLL only for RPM)
        //
        //  1. Inject DLL, connect pipe for RPM
        //  2. Press I to turn on ignition
        //  3. Hold S for 2s (starter) + tap R to help start
        //  4. Press D to enter dyno mode
        //  5. For each target RPM:
        //     a. Hold R to rev up until RPM reaches target
        //     b. Press H to hold RPM (R is STILL held)
        //     c. Record 5s with R held -> {rpm}_load.wav
        //     d. Release R (depress), record 5s -> {rpm}_noload.wav
        //     e. Press R again + release H -> engine slowly revs to next RPM
        //  6. Shut down
        // ════════════════════════════════════════════════════════════════

        private void RunAsync(RecorderConfig cfg, CancellationToken ct)
        {
            KeyboardBackend? backend = null;
            try
            {
                backend = new KeyboardBackend();
                Log($"Mode: {backend.Name}");

                if (!backend.Initialize(cfg, Log, ct))
                {
                    Log("Backend initialization failed.");
                    return;
                }

                IntPtr hwnd = backend.Hwnd;
                _engineSimHwnd = hwnd;

                // ── Force focus on Engine Sim ──
                Log("Focusing Engine Sim window...");
                KeyboardSim.FocusWindow(hwnd);

                // ── Start focus monitor ──
                _focusWarned = false;
                BeginInvoke(new Action(() =>
                {
                    _focusMonitor = new System.Windows.Forms.Timer { Interval = 1000 };
                    _focusMonitor.Tick += FocusMonitor_Tick;
                    _focusMonitor.Start();
                }));

                // ── Step 1: Turn on ignition (press A) ──
                Log("=== STEP 1: Ignition ===");
                KeyboardSim.KeyPress(hwnd, KeyboardSim.VK_A, 120);
                Log("Pressed A (Ignition ON)");
                ct.WaitHandle.WaitOne(500);

                // ── Step 2: Hold starter S for 1500ms + single tap W ──
                Log("=== STEP 2: Starting engine ===");
                Log("Holding S (Starter) for 1500ms + single tap W...");

                KeyboardSim.KeyDown(hwnd, KeyboardSim.VK_S);
                ct.WaitHandle.WaitOne(300); // let starter crank briefly before throttle tap

                // Single short tap of W to help engine catch (10ms, precise via busy-wait)
                KeyboardSim.KeyPress(hwnd, KeyboardSim.VK_W, 10);
                Log("Tapped W (throttle)");

                // Keep S held for the remainder of 1500ms total
                var starterSw = Stopwatch.StartNew();
                while (starterSw.ElapsedMilliseconds < 1200) // 300 already elapsed above
                {
                    if (ct.IsCancellationRequested) break;
                    KeyboardSim.KeyDown(hwnd, KeyboardSim.VK_S); // keep held
                    double? rpm = backend.ReadRpm();
                    if (rpm.HasValue) SetRpm($"RPM: {rpm.Value:F0}");
                    ct.WaitHandle.WaitOne(50);
                }

                KeyboardSim.KeyUp(hwnd, KeyboardSim.VK_S);
                Log("Released S (Starter)");
                ct.WaitHandle.WaitOne(500);

                double? idleRpm = backend.ReadRpm();
                if (idleRpm.HasValue && idleRpm.Value > 100)
                    Log($"Engine running - idle RPM: {idleRpm.Value:F0}");
                else
                    Log($"Warning: RPM = {idleRpm?.ToString("F0") ?? "null"} - engine may not have started");

                if (ct.IsCancellationRequested) return;

                // ── Step 3: Enter dyno mode (press D), keep S held 500ms to prevent stall ──
                Log("=== STEP 3: Dyno mode ===");
                KeyboardSim.KeyPress(hwnd, KeyboardSim.VK_D, 120);
                Log("Pressed D (Dyno ON) - holding S for 500ms to prevent stall...");
                KeyboardSim.KeyDown(hwnd, KeyboardSim.VK_S);
                ct.WaitHandle.WaitOne(500);
                KeyboardSim.KeyUp(hwnd, KeyboardSim.VK_S);
                Log("Released S");
                ct.WaitHandle.WaitOne(300);

                // ── Step 4: For each target RPM ──
                string prefix = cfg.CustomPrefix ?? "";
                string carName = cfg.CustomName ?? "";
                for (int i = 0; i < cfg.TargetRpms.Count; i++)
                {
                    if (ct.IsCancellationRequested) break;

                    int target = cfg.TargetRpms[i];
                    Log($"=== TARGET {i + 1}/{cfg.TargetRpms.Count}: {target} RPM ===");

                    // ── 4a: Hold R to rev up to target RPM ──
                    SetStatus($"Revving to {target} RPM...");
                    Log($"Holding R to rev to {target} RPM...");
                    backend.SetThrottle(1.0); // starts holding R key
                    WaitForRpm(backend, cfg, target, ct);
                    if (ct.IsCancellationRequested) break;

                    // ── 4b: Press H to hold RPM (R is STILL held) ──
                    Log("Pressing H (Hold RPM) - throttle still held");
                    KeyboardSim.KeyPress(hwnd, KeyboardSim.VK_H, 120);
                    ct.WaitHandle.WaitOne(300);

                    // ── 4c: Record LOAD (R still held, H holding RPM) ──
                    string baseName = string.IsNullOrEmpty(carName) ? "" : $"{prefix}{carName}_";
                    string loadFile = $"{baseName}on_{target}.wav";
                    string loadPath = Path.Combine(cfg.OutputDir, loadFile);
                    SetStatus($"Recording {target} RPM (load)...");
                    Log($"Recording LOAD for {cfg.RecordSeconds}s -> {loadPath}");
                    RecordAudio(backend, loadPath, cfg.RecordSeconds, ct);
                    if (ct.IsCancellationRequested) break;
                    Log($"Saved: {loadPath}");

                    // ── 4d: Release R (depress throttle), 2s gap, then record NO-LOAD ──
                    Log("Releasing R (throttle off) - H still holding");
                    backend.SetThrottle(0); // releases R key
                    Log("Waiting 2s for engine to settle...");
                    ct.WaitHandle.WaitOne(2000); // 2 second gap

                    string noloadFile = $"{baseName}off_{target}.wav";
                    string noloadPath = Path.Combine(cfg.OutputDir, noloadFile);
                    SetStatus($"Recording {target} RPM (no load)...");
                    Log($"Recording NO-LOAD for {cfg.RecordSeconds}s -> {noloadPath}");
                    RecordAudio(backend, noloadPath, cfg.RecordSeconds, ct);
                    if (ct.IsCancellationRequested) break;
                    Log($"Saved: {noloadPath}");

                    IncProgress();
                    Log($"Target {target} RPM complete!");

                    // ── 4e: Prepare for next RPM ──
                    // Press R (hold throttle again) + release H
                    // so the engine slowly revs toward the next target
                    if (i < cfg.TargetRpms.Count - 1)
                    {
                        Log("Pressing R + releasing H -> revving to next target...");
                        backend.SetThrottle(1.0); // hold R again
                        ct.WaitHandle.WaitOne(100);
                        KeyboardSim.KeyPress(hwnd, KeyboardSim.VK_H, 120); // release hold
                        ct.WaitHandle.WaitOne(300);
                    }
                    else
                    {
                        // Last target - release hold
                        KeyboardSim.KeyPress(hwnd, KeyboardSim.VK_H, 120);
                    }
                }

                // ── Step 5: Shut down ──
                Log("=== SHUTTING DOWN ===");
                backend.SetThrottle(0);
                Thread.Sleep(200);
                KeyboardSim.KeyPress(hwnd, KeyboardSim.VK_D, 120); // Dyno off
                Thread.Sleep(200);
                KeyboardSim.KeyPress(hwnd, KeyboardSim.VK_A, 120); // Ignition off
                Log("Engine stopped. All recordings complete!");
                // Open output folder when recording finishes
                try { Process.Start("explorer.exe", cfg.OutputDir); }
                catch { Log("Could not open output folder."); }
            }
            catch (OperationCanceledException) { Log("Stopped by user."); }
            catch (Exception ex)
            {
                Log($"ERROR: {ex.Message}");
                BeginInvoke(new Action(() =>
        MessageBox.Show(ex.Message, "Error", MessageBoxButtons.OK, MessageBoxIcon.Error)));
            }
            finally
            {
                try { backend?.SetThrottle(0); } catch { }
                backend?.Dispose();
                BeginInvoke(new Action(() =>
                {
                    _focusMonitor?.Stop();
                    _focusMonitor?.Dispose();
                    _focusMonitor = null;
                    lblStatus.ForeColor = SystemColors.ControlText;
                }));
                _engineSimHwnd = IntPtr.Zero;
                ResetControls();
            }
        }

        // ── Wait for RPM to reach target ──────────────────────────────
        // Throttle is already being held by the caller.
        // Just monitors RPM until it's close enough.

        private void WaitForRpm(KeyboardBackend backend, RecorderConfig cfg,
          int targetRpm, CancellationToken ct)
        {
            var sw = Stopwatch.StartNew();
            while (!ct.IsCancellationRequested)
            {
                double? rpm = backend.ReadRpm();
                if (rpm.HasValue)
                {
                    SetRpm($"RPM: {rpm.Value:F0}");
                    // Fire slightly before target to account for H key reaction time (~120ms)
                    if (rpm.Value >= targetRpm - cfg.RpmTolerance - 25)
                    {
                        Log($"Reached {rpm.Value:F0} RPM (target: {targetRpm})");
                        break;
                    }
                }

                if (sw.Elapsed.TotalSeconds > 30)
                {
                    double current = backend.ReadRpm() ?? 0;
                    Log($"Warning: timeout revving to {targetRpm} (current: {current:F0})");
                    break;
                }

                ct.WaitHandle.WaitOne(20);
            }
        }

        // ── Simple audio recording ────────────────────────────────────
        // Records system audio for the given number of seconds.
        // Shows RPM in the UI while recording.

        private void RecordAudio(KeyboardBackend backend, string outputPath,
               int seconds, CancellationToken ct)
        {
            using var capture = new WasapiLoopbackCapture();
            capture.WaveFormat = WaveFormat.CreateIeeeFloatWaveFormat(44100, 2);
            using var writer = new WaveFileWriter(outputPath, capture.WaveFormat);
            var done = new ManualResetEventSlim(false);

            capture.DataAvailable += (s, e) =>
                {
                    if (e.BytesRecorded > 0)
                        writer.Write(e.Buffer, 0, e.BytesRecorded);
                };
            capture.RecordingStopped += (s, e) => done.Set();
            capture.StartRecording();

            DateTime start = DateTime.UtcNow;
            while (!ct.IsCancellationRequested)
            {
                double elapsed = (DateTime.UtcNow - start).TotalSeconds;
                if (elapsed >= seconds) break;

                double? rpm = backend.ReadRpm();
                if (rpm.HasValue)
                {
                    SetRpm($"RPM: {rpm.Value:F0}");
                    SetStatus($"Recording - {rpm.Value:F0} RPM - {elapsed:F1}s / {seconds}s");
                }

                ct.WaitHandle.WaitOne(100);
            }

            capture.StopRecording();
            done.Wait(TimeSpan.FromSeconds(5));
        }

        // ── Helpers ────────────────────────────────────────────────────

        private sealed class ProcessItem
        {
            public int ProcessId { get; }
            public string DisplayName { get; }
            public ProcessItem(Process p) { ProcessId = p.Id; DisplayName = $"{p.ProcessName} (PID {p.Id})"; }
            public override string ToString() => DisplayName;
        }

        private void Form1_Load(object sender, EventArgs e)
        {

        }

        // ── Focus monitoring ──────────────────────────────────────────

        [System.Runtime.InteropServices.DllImport("user32.dll")]
        private static extern IntPtr GetForegroundWindow();

        private void FocusMonitor_Tick(object? sender, EventArgs e)
        {
            if (_engineSimHwnd == IntPtr.Zero) return;

            IntPtr focused = GetForegroundWindow();
            bool recorderFocused = (focused == this.Handle);
            bool simFocused = (focused == _engineSimHwnd);

            if (!recorderFocused && !simFocused)
            {
                if (!_focusWarned)
                {
                    _focusWarned = true;
                    lblStatus.ForeColor = Color.OrangeRed;
                    Log("⚠ WARNING: Neither Engine Sim nor this window is focused. Click this window or Engine Sim to keep input working.");
                }
            }
            else
            {
                if (_focusWarned)
                {
                    _focusWarned = false;
                    lblStatus.ForeColor = SystemColors.ControlText;
                }
            }
        }
    }
}
