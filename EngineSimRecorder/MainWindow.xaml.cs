using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Threading;
using EngineSimRecorder.Backends.Keyboard;
using EngineSimRecorder.Core;
using NAudio.Wave;

namespace EngineSimRecorder
{
    public partial class MainWindow : Window
    {
        private CancellationTokenSource? _cts;
        private Task? _workerTask;
        private DispatcherTimer? _focusMonitor;
        private IntPtr _engineSimHwnd = IntPtr.Zero;
        private bool _focusWarned = false;
        private AppSettings _settings = new();

        public MainWindow()
        {
            InitializeComponent();

            string iconPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "icon.ico");
            if (File.Exists(iconPath))
                this.Icon = new System.Windows.Media.Imaging.BitmapImage(new Uri(iconPath));

            // Default RPM targets
            foreach (int rpm in new[] { 1500, 2000, 3000, 4000, 5000, 6000 })
                lstTargetRpms.Items.Add(rpm);
        }

        // ── Process ──

        private void btnRefresh_Click(object sender, RoutedEventArgs e)
        {
            cmbProcess.Items.Clear();
            foreach (var name in new[] { "engine-sim-app", "engine-sim", "engine_sim", "EngineSimulator" })
            {
                foreach (var proc in Process.GetProcessesByName(name))
                    cmbProcess.Items.Add(new ProcessItem(proc));
            }
            if (cmbProcess.Items.Count > 0)
                cmbProcess.SelectedIndex = 0;
        }

        // ── Output ──

        private void btnBrowseOutput_Click(object sender, RoutedEventArgs e)
        {
            var dlg = new System.Windows.Forms.FolderBrowserDialog { Description = "Select output folder" };
            if (dlg.ShowDialog() == System.Windows.Forms.DialogResult.OK)
                txtOutputDir.Text = dlg.SelectedPath;
        }

        private void btnOpenOutput_Click(object sender, RoutedEventArgs e)
        {
            string dir = txtOutputDir.Text.Trim();
            if (!Directory.Exists(dir))
                Directory.CreateDirectory(dir);
            Process.Start("explorer.exe", dir);
        }

        // ── RPM ──

        private void btnAddRpm_Click(object sender, RoutedEventArgs e)
        {
            if (int.TryParse(txtRpmInput.Text, out int rpm) && !lstTargetRpms.Items.Contains(rpm))
                lstTargetRpms.Items.Add(rpm);
        }

        private void btnRemoveRpm_Click(object sender, RoutedEventArgs e)
        {
            if (lstTargetRpms.SelectedItem != null)
                lstTargetRpms.Items.Remove(lstTargetRpms.SelectedItem);
        }

        private void btnPreset_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button btn && int.TryParse(btn.Content?.ToString()?.Replace("K", "000"), out int rpm))
            {
                if (!lstTargetRpms.Items.Contains(rpm))
                    lstTargetRpms.Items.Add(rpm);
            }
        }

        private void btnSortRpm_Click(object sender, RoutedEventArgs e)
        {
            var items = new List<int>();
            foreach (var item in lstTargetRpms.Items)
                items.Add(Convert.ToInt32(item));
            items.Sort();
            lstTargetRpms.Items.Clear();
            foreach (var rpm in items)
                lstTargetRpms.Items.Add(rpm);
        }

        private void btnClearRpm_Click(object sender, RoutedEventArgs e)
        {
            lstTargetRpms.Items.Clear();
        }

        private void btnEditRpm_Click(object sender, RoutedEventArgs e)
        {
            if (lstTargetRpms.SelectedItem is not int selected) return;
            if (int.TryParse(txtRpmInput.Text, out int newVal))
            {
                int idx = lstTargetRpms.SelectedIndex;
                lstTargetRpms.Items[idx] = newVal;
            }
        }

        private void lstTargetRpms_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (lstTargetRpms.SelectedItem is int selected)
                txtRpmInput.Text = selected.ToString();
        }

        // ── Start / Stop ──

        private void btnStart_Click(object sender, RoutedEventArgs e)
        {
            if (lstTargetRpms.Items.Count == 0)
            {
                MessageBox.Show("Add at least one target RPM.", "No targets",
                    MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            if (cmbProcess.SelectedItem is not ProcessItem sel)
            {
                MessageBox.Show("Select an Engine Simulator process.", "No process",
                    MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            string outputDir = txtOutputDir.Text.Trim();
            Directory.CreateDirectory(outputDir);

            var targets = new List<int>();
            foreach (var item in lstTargetRpms.Items)
                targets.Add(Convert.ToInt32(item));
            targets.Sort();

            var cfg = new RecorderConfig
            {
                OutputDir = outputDir,
                ProcessId = sel.ProcessId,
                TargetRpms = targets,
                CustomName = txtCarName.Text.Trim(),
                CustomPrefix = GetPrefix(),
                SampleRate = cmbSampleRate.SelectedIndex == 1 ? 48000 : 44100,
                Channels = cmbChannels.SelectedIndex == 1 ? 1 : 2,
            };

            btnStart.IsEnabled = false;
            btnStop.IsEnabled = true;
            pbarProgress.Value = 0;
            pbarProgress.Maximum = targets.Count;
            txtLog.Text = "";

            _cts = new CancellationTokenSource();
            _workerTask = Task.Run(() => RunAsync(cfg, _cts.Token));
        }

        private void btnStop_Click(object sender, RoutedEventArgs e) => _cts?.Cancel();

        private void chkStayOnTop_Changed(object sender, RoutedEventArgs e)
        {
            Topmost = chkStayOnTop.IsChecked == true;
        }

        // ── UI Helpers ──

        private void Log(string msg)
        {
            string line = $"[{DateTime.Now:HH:mm:ss}] {msg}";
            Dispatcher.BeginInvoke(() =>
            {
                txtLog.AppendText(line + "\n");
                txtLog.ScrollToEnd();
            });
        }

        private void SetStatus(string t) => Dispatcher.BeginInvoke(() => lblStatus.Text = t);
        private void SetRpm(string t) => Dispatcher.BeginInvoke(() => lblCurrentRpm.Text = t);
        private void IncProgress() => Dispatcher.BeginInvoke(() =>
            pbarProgress.Value = Math.Min(pbarProgress.Value + 1, pbarProgress.Maximum));
        private void ResetControls() => Dispatcher.BeginInvoke(() =>
        {
            btnStart.IsEnabled = true;
            btnStop.IsEnabled = false;
            lblStatus.Text = "Done.";
        });

        // ════════════════════════════════════════════════════════════════
        //  WORKFLOW (all keyboard, DLL only for RPM)
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

                Log("Focusing Engine Sim window...");
                KeyboardSim.FocusWindow(hwnd);

                _focusWarned = false;
                Dispatcher.BeginInvoke(() =>
                {
                    _focusMonitor = new DispatcherTimer { Interval = TimeSpan.FromSeconds(1) };
                    _focusMonitor.Tick += FocusMonitor_Tick;
                    _focusMonitor.Start();
                });

                // Step 1: Ignition
                Log("=== STEP 1: Ignition ===");
                KeyboardSim.KeyPress(hwnd, KeyboardSim.VK_A, 120);
                Log("Pressed A (Ignition ON)");
                ct.WaitHandle.WaitOne(500);

                // Step 2: Starter
                Log("=== STEP 2: Starting engine ===");
                Log("Holding S (Starter) for 1500ms + single tap W...");
                KeyboardSim.KeyDown(hwnd, KeyboardSim.VK_S);
                ct.WaitHandle.WaitOne(300);
                KeyboardSim.KeyPress(hwnd, KeyboardSim.VK_W, 10);
                Log("Tapped W (throttle)");

                var starterSw = Stopwatch.StartNew();
                while (starterSw.ElapsedMilliseconds < 1200)
                {
                    if (ct.IsCancellationRequested) break;
                    KeyboardSim.KeyDown(hwnd, KeyboardSim.VK_S);
                    double? rpm = backend.ReadRpm();
                    if (rpm.HasValue) SetRpm($"RPM: {rpm.Value:F0}");
                    ct.WaitHandle.WaitOne(16);
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

                // Step 3: Dyno mode
                Log("=== STEP 3: Dyno mode ===");
                KeyboardSim.KeyPress(hwnd, KeyboardSim.VK_D, 120);
                Log("Pressed D (Dyno ON) - holding S for 500ms...");
                KeyboardSim.KeyDown(hwnd, KeyboardSim.VK_S);
                ct.WaitHandle.WaitOne(500);
                KeyboardSim.KeyUp(hwnd, KeyboardSim.VK_S);
                Log("Released S");
                ct.WaitHandle.WaitOne(300);

                // Step 4: Record each target RPM
                string prefix = cfg.CustomPrefix ?? "";
                string carName = cfg.CustomName ?? "";
                for (int i = 0; i < cfg.TargetRpms.Count; i++)
                {
                    if (ct.IsCancellationRequested) break;

                    int target = cfg.TargetRpms[i];
                    Log($"=== TARGET {i + 1}/{cfg.TargetRpms.Count}: {target} RPM ===");

                    SetStatus($"Revving to {target} RPM...");
                    Log($"Holding R to rev to {target} RPM...");
                    backend.SetThrottle(1.0);
                    WaitForRpm(backend, cfg, target, ct);
                    if (ct.IsCancellationRequested) break;

                    Log("Pressing H (Hold RPM) - throttle still held");
                    KeyboardSim.KeyPress(hwnd, KeyboardSim.VK_H, 120);
                    ct.WaitHandle.WaitOne(300);

                    string baseName = string.IsNullOrEmpty(carName) ? "" : $"{prefix}{carName}_";
                    string loadFile = $"{baseName}on_{target}.wav";
                    string loadPath = Path.Combine(cfg.OutputDir, loadFile);
                    SetStatus($"Recording {target} RPM (load)...");
                    Log($"Recording LOAD for {cfg.RecordSeconds}s -> {loadPath}");
                    RecordAudio(backend, loadPath, cfg, ct);
                    if (ct.IsCancellationRequested) break;
                    Log($"Saved: {loadPath}");

                    Log("Releasing R (throttle off) - H still holding");
                    backend.SetThrottle(0);
                    Log("Waiting 2s for engine to settle...");
                    ct.WaitHandle.WaitOne(2000);

                    string noloadFile = $"{baseName}off_{target}.wav";
                    string noloadPath = Path.Combine(cfg.OutputDir, noloadFile);
                    SetStatus($"Recording {target} RPM (no load)...");
                    Log($"Recording NO-LOAD for {cfg.RecordSeconds}s -> {noloadPath}");
                    RecordAudio(backend, noloadPath, cfg, ct);
                    if (ct.IsCancellationRequested) break;
                    Log($"Saved: {noloadPath}");

                    IncProgress();
                    Log($"Target {target} RPM complete!");

                    if (i < cfg.TargetRpms.Count - 1)
                    {
                        Log("Pressing R + releasing H -> revving to next target...");
                        backend.SetThrottle(1.0);
                        ct.WaitHandle.WaitOne(30);
                        KeyboardSim.KeyPress(hwnd, KeyboardSim.VK_H, 120);
                        ct.WaitHandle.WaitOne(300);
                    }
                    else
                    {
                        KeyboardSim.KeyPress(hwnd, KeyboardSim.VK_H, 120);
                    }
                }

                // Shutdown
                Log("=== SHUTTING DOWN ===");
                backend.SetThrottle(0);
                Thread.Sleep(200);
                KeyboardSim.KeyPress(hwnd, KeyboardSim.VK_D, 120);
                Thread.Sleep(200);
                KeyboardSim.KeyPress(hwnd, KeyboardSim.VK_A, 120);
                Log("Engine stopped. All recordings complete!");
                try { Process.Start("explorer.exe", cfg.OutputDir); }
                catch { Log("Could not open output folder."); }
            }
            catch (OperationCanceledException) { Log("Stopped by user."); }
            catch (Exception ex)
            {
                Log($"ERROR: {ex.Message}");
                Dispatcher.BeginInvoke(() =>
                    MessageBox.Show(ex.Message, "Error", MessageBoxButton.OK, MessageBoxImage.Error));
            }
            finally
            {
                try { backend?.SetThrottle(0); } catch { }
                backend?.Dispose();
                Dispatcher.BeginInvoke(() =>
                {
                    _focusMonitor?.Stop();
                    _focusMonitor = null;
                });
                _engineSimHwnd = IntPtr.Zero;
                ResetControls();
            }
        }

        private void WaitForRpm(KeyboardBackend backend, RecorderConfig cfg, int targetRpm, CancellationToken ct)
        {
            var sw = Stopwatch.StartNew();
            while (!ct.IsCancellationRequested)
            {
                double? rpm = backend.ReadRpm();
                if (rpm.HasValue)
                {
                    SetRpm($"RPM: {rpm.Value:F0}");
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

        private void RecordAudio(KeyboardBackend backend, string outputPath, RecorderConfig cfg, CancellationToken ct)
        {
            using var capture = new WasapiLoopbackCapture();
            capture.WaveFormat = WaveFormat.CreateIeeeFloatWaveFormat(cfg.SampleRate, cfg.Channels);
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
                if (elapsed >= cfg.RecordSeconds) break;

                double? rpm = backend.ReadRpm();
                if (rpm.HasValue)
                {
                    SetRpm($"RPM: {rpm.Value:F0}");
                    SetStatus($"Recording - {rpm.Value:F0} RPM - {elapsed:F1}s / {cfg.RecordSeconds}s");
                }
                ct.WaitHandle.WaitOne(30);
            }

            capture.StopRecording();
            done.Wait(TimeSpan.FromSeconds(5));
        }

        // ── Helpers ──

        private string GetPrefix()
        {
            string p = txtPrefix.Text.Trim();
            if (string.IsNullOrEmpty(p)) return "";
            return p.EndsWith("_") ? p : p + "_";
        }

        private sealed class ProcessItem
        {
            public int ProcessId { get; }
            public string DisplayName { get; }
            public ProcessItem(Process p) { ProcessId = p.Id; DisplayName = $"{p.ProcessName} (PID {p.Id})"; }
            public override string ToString() => DisplayName;
        }

        private void Window_Loaded(object sender, RoutedEventArgs e)
        {
            _settings = AppSettings.Load();
            cmbSampleRate.SelectedIndex = _settings.SampleRate == 48000 ? 1 : 0;
            cmbChannels.SelectedIndex = _settings.Channels == 1 ? 1 : 0;
        }

        private void Window_Closing(object sender, System.ComponentModel.CancelEventArgs e)
        {
            _cts?.Cancel();
            _settings.SampleRate = cmbSampleRate.SelectedIndex == 1 ? 48000 : 44100;
            _settings.Channels = cmbChannels.SelectedIndex == 1 ? 1 : 2;
            _settings.Save();
        }

        // ── Focus monitor ──

        [System.Runtime.InteropServices.DllImport("user32.dll")]
        private static extern IntPtr GetForegroundWindow();

        private void FocusMonitor_Tick(object? sender, EventArgs e)
        {
            if (_engineSimHwnd == IntPtr.Zero) return;

            IntPtr focused = GetForegroundWindow();
            bool recorderFocused = (focused == new System.Windows.Interop.WindowInteropHelper(this).Handle);
            bool simFocused = (focused == _engineSimHwnd);

            if (!recorderFocused && !simFocused)
            {
                if (!_focusWarned)
                {
                    _focusWarned = true;
                    lblStatus.Foreground = new SolidColorBrush(Colors.OrangeRed);
                    Log("⚠ Neither window focused — click this or Engine Sim");
                }
            }
            else
            {
                if (_focusWarned)
                {
                    _focusWarned = false;
                    lblStatus.Foreground = (Brush)FindResource("TextSecondaryBrush");
                }
            }
        }
    }
}
