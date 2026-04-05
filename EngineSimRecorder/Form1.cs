using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;
using EngineSimRecorder.Backends.Injection;
using EngineSimRecorder.Backends.Ocr;
using EngineSimRecorder.Core;
using NAudio.Wave;

namespace EngineSimRecorder
{
    public partial class Form1 : Form
    {
        private CancellationTokenSource? _cts;
        private Task? _workerTask;

        public Form1()
        {
            InitializeComponent();
            foreach (int rpm in new[] { 800, 1500, 3000, 4500, 6000, 7500 })
                lstTargetRpms.Items.Add(rpm);

            // Wire up mode toggle
            rbInjection.CheckedChanged += ModeChanged;
            rbOcr.CheckedChanged += ModeChanged;
            UpdateModeVisibility();
        }

        // ── Mode toggle ───────────────────────────────────────────────

        private void ModeChanged(object? sender, EventArgs e) => UpdateModeVisibility();

        private void UpdateModeVisibility()
        {
            bool inj = rbInjection.Checked;
            grpProcess.Visible = inj;
            grpOcrRegion.Visible = !inj;
        }

        private BackendMode SelectedMode =>
            rbInjection.Checked ? BackendMode.Injection : BackendMode.Ocr;

        // ── UI events ──────────────────────────────────────────────────

        private void btnRefresh_Click(object sender, EventArgs e) => RefreshProcessList();

        private void RefreshProcessList()
        {
            cmbProcess.Items.Clear();
            foreach (var name in new[] { "engine-sim", "engine_sim", "EngineSimulator" })
                foreach (var proc in Process.GetProcessesByName(name))
                    cmbProcess.Items.Add(new ProcessItem(proc));
            if (cmbProcess.Items.Count > 0) cmbProcess.SelectedIndex = 0;
        }

        private void btnBrowseOutput_Click(object sender, EventArgs e)
        {
            using var dlg = new FolderBrowserDialog { Description = "Select output folder" };
            if (dlg.ShowDialog() == DialogResult.OK) txtOutputDir.Text = dlg.SelectedPath;
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

        private void btnStart_Click(object sender, EventArgs e)
        {
            if (lstTargetRpms.Items.Count == 0)
            {
                MessageBox.Show("Add at least one target RPM.", "No targets",
                    MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            int pid = 0;
            if (rbInjection.Checked)
            {
                if (cmbProcess.SelectedItem is not ProcessItem sel)
                {
                    MessageBox.Show("Select an Engine Simulator process.", "No process",
                        MessageBoxButtons.OK, MessageBoxIcon.Warning);
                    return;
                }
                pid = sel.ProcessId;
            }

            string outputDir = txtOutputDir.Text.Trim();
            Directory.CreateDirectory(outputDir);

            var targets = new List<int>();
            foreach (var item in lstTargetRpms.Items) targets.Add(Convert.ToInt32(item));

            var cfg = new RecorderConfig
            {
                OutputDir = outputDir,
                ProcessId = pid,
                OcrRegion = new Rectangle((int)numOcrX.Value, (int)numOcrY.Value,
                                          (int)numOcrW.Value, (int)numOcrH.Value),
                TargetRpms = targets,
                RpmTolerance = (int)numRpmTol.Value,
                HoldSeconds = (int)numHoldSec.Value,
                RecordSeconds = (int)numRecordSec.Value,
                Kp = (double)numKp.Value,
                Ki = (double)numKi.Value,
                Kd = (double)numKd.Value,
                Mode = SelectedMode,
            };

            btnStart.Enabled = false;
            btnStop.Enabled = true;
            btnRefresh.Enabled = false;
            rbInjection.Enabled = false;
            rbOcr.Enabled = false;
            pbarProgress.Value = 0;
            pbarProgress.Maximum = targets.Count;
            txtLog.Clear();

            _cts = new CancellationTokenSource();
            _workerTask = Task.Run(() => RunAsync(cfg, _cts.Token));
        }

        private void btnStop_Click(object sender, EventArgs e) => _cts?.Cancel();
        private void Form1_FormClosing(object sender, FormClosingEventArgs e) => _cts?.Cancel();

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
            btnRefresh.Enabled = true; rbInjection.Enabled = true; rbOcr.Enabled = true;
            lblStatus.Text = "Done.";
        }));

        // ── Main worker ───────────────────────────────────────────────

        private void RunAsync(RecorderConfig cfg, CancellationToken ct)
        {
            IEngineBackend? backend = null;
            try
            {
                // Create backend based on mode
                backend = cfg.Mode == BackendMode.Injection
                    ? new InjectionBackend()
                    : new OcrBackend();

                Log($"Mode: {backend.Name}");

                // Initialize
                if (!backend.Initialize(cfg, Log, ct))
                {
                    Log("Backend initialization failed.");
                    return;
                }

                // Start engine
                backend.StartEngine(Log, ct);
                if (ct.IsCancellationRequested) return;

                // Record each RPM target
                for (int i = 0; i < cfg.TargetRpms.Count; i++)
                {
                    if (ct.IsCancellationRequested) break;

                    int target = cfg.TargetRpms[i];
                    Log($"── Target {i + 1}/{cfg.TargetRpms.Count}: {target} RPM ──");
                    SetStatus($"Approaching {target} RPM…");

                    // PID hold
                    var pid = new PidController(cfg.Kp, cfg.Ki, cfg.Kd);
                    HoldRpm(backend, pid, cfg, target, ct);
                    if (ct.IsCancellationRequested) break;

                    // Record
                    string wavPath = Path.Combine(cfg.OutputDir, $"rpm_{target}.wav");
                    Log($"Recording → {wavPath}");
                    SetStatus($"Recording at {target} RPM…");
                    RecordWasapi(backend, pid, wavPath, cfg, target, ct);
                    IncProgress();
                    Log($"Saved: {wavPath}");
                }

                backend.StopEngine();
                Log("Done.");
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
                backend?.Dispose();
                ResetControls();
            }
        }

        // ── PID hold ───────────────────────────────────────────────────

        private void HoldRpm(IEngineBackend backend, PidController pid, RecorderConfig cfg,
                              int targetRpm, CancellationToken ct)
        {
            const int loopMs = 50;
            DateTime stableStart = DateTime.MinValue;
            bool wasStable = false;
            pid.Reset();

            while (!ct.IsCancellationRequested)
            {
                double? rpm = backend.ReadRpm();
                if (!rpm.HasValue) { Thread.Sleep(loopMs, ct); continue; }

                SetRpm($"RPM: {rpm.Value:F0}");

                double error = targetRpm - rpm.Value;
                double output = pid.Update(error, loopMs / 1000.0);
                backend.SetThrottle(Math.Clamp(output, 0, 1));

                bool stable = Math.Abs(error) <= cfg.RpmTolerance;
                if (stable)
                {
                    if (!wasStable) stableStart = DateTime.UtcNow;
                    wasStable = true;
                    double held = (DateTime.UtcNow - stableStart).TotalSeconds;
                    SetStatus($"Stable at {rpm.Value:F0} RPM – {held:F1}s / {cfg.HoldSeconds}s");
                    if (held >= cfg.HoldSeconds) break;
                }
                else { wasStable = false; stableStart = DateTime.MinValue; }

                Thread.Sleep(loopMs, ct);
            }
        }

        // ── WASAPI recording ───────────────────────────────────────────

        private void RecordWasapi(IEngineBackend backend, PidController pid,
                                   string outputPath, RecorderConfig cfg,
                                   int targetRpm, CancellationToken ct)
        {
            using var capture = new WasapiLoopbackCapture();
            capture.WaveFormat = WaveFormat.CreateIeeeFloatWaveFormat(44100, 2);
            using var writer = new WaveFileWriter(outputPath, capture.WaveFormat);
            var end = new ManualResetEventSlim(false);
            DateTime start = DateTime.UtcNow;

            capture.DataAvailable += (s, e) => { if (e.BytesRecorded > 0) writer.Write(e.Buffer, 0, e.BytesRecorded); };
            capture.RecordingStopped += (s, e) => end.Set();
            capture.StartRecording();
            Log($"Recording {cfg.RecordSeconds}s...");

            while (!ct.IsCancellationRequested)
            {
                if ((DateTime.UtcNow - start).TotalSeconds >= cfg.RecordSeconds) break;

                double? rpm = backend.ReadRpm();
                if (rpm.HasValue)
                {
                    SetRpm($"RPM: {rpm.Value:F0}");
                    if (Math.Abs(rpm.Value - targetRpm) > cfg.RpmTolerance * 2)
                    {
                        Log($"RPM drifted to {rpm.Value:F0} — rebalancing...");
                        // Re-stabilize
                        pid.Reset();
                        const int loopMs = 50;
                        while (!ct.IsCancellationRequested)
                        {
                            double? r = backend.ReadRpm();
                            if (r.HasValue)
                            {
                                double err = targetRpm - r.Value;
                                double out_ = pid.Update(err, loopMs / 1000.0);
                                backend.SetThrottle(Math.Clamp(out_, 0, 1));
                                if (Math.Abs(err) <= cfg.RpmTolerance) break;
                            }
                            Thread.Sleep(loopMs, ct);
                        }
                        start = DateTime.UtcNow;
                    }
                }
                Thread.Sleep(50, ct);
            }

            capture.StopRecording();
            end.Wait(TimeSpan.FromSeconds(5));
        }

        // ── Helpers ────────────────────────────────────────────────────

        private sealed class ProcessItem
        {
            public int ProcessId { get; }
            public string DisplayName { get; }
            public ProcessItem(Process p) { ProcessId = p.Id; DisplayName = $"{p.ProcessName} (PID {p.Id})"; }
            public override string ToString() => DisplayName;
        }
    }
}
