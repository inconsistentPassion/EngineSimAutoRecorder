using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Animation;
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
        private DispatcherTimer? _progressTimer;
        private Stopwatch? _progressSw;
        private double _progressDurationSec;
        private IntPtr _engineSimHwnd = IntPtr.Zero;
        private bool _focusWarned = false;
        private AppSettings _settings = new();
        private KeyboardBackend? _backend;

        public MainWindow()
        {
            InitializeComponent();

            string iconPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "icon.ico");
            if (File.Exists(iconPath))
                this.Icon = new System.Windows.Media.Imaging.BitmapImage(new Uri(iconPath));

            // Default RPM targets
            foreach (int rpm in new[] { 1500, 2000, 3000, 4000, 5000, 6000 })
                lstTargetRpms.Items.Add(rpm);

            btnStart.IsEnabled = false;
        }

        // ── Process ──

        private void RefreshProcessList()
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

        private void btnRefresh_Click(object sender, RoutedEventArgs e)
        {
            if (_backend != null)
            {
                if (_cts != null)
                {
                    MessageBox.Show("Stop recording first.", "Busy",
                        MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }
                Disconnect();
                return;
            }

            RefreshProcessList();
            if (cmbProcess.Items.Count == 0)
            {
                MessageBox.Show("No Engine Simulator process found.", "Not Found",
                    MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            if (cmbProcess.SelectedItem is not ProcessItem sel)
            {
                MessageBox.Show("Select a process.", "No Selection",
                    MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            btnRefresh.IsEnabled = false;
            btnRefresh.Content = "Connecting...";
            txtLog.Text = "";

            var cfg = new RecorderConfig { ProcessId = sel.ProcessId };

            Task.Run(() =>
            {
                var backend = new KeyboardBackend();
                bool ok = backend.Initialize(cfg, msg => Log(msg), CancellationToken.None);

                Dispatcher.BeginInvoke(() =>
                {
                    if (ok)
                    {
                        _backend = backend;
                        btnRefresh.Content = "Disconnect";
                        btnRefresh.IsEnabled = true;
                        btnStart.IsEnabled = true;

                        double? maxRpm = backend.ReadMaxRpm();
                        if (maxRpm.HasValue)
                        {
                            Log($"Connected. Engine redline: {maxRpm.Value:F0} RPM");
                            lblRedline.Text = $"(redline: {maxRpm.Value:F0})";
                        }
                        else
                        {
                            Log("Connected. Waiting for redline data...");
                        }
                    }
                    else
                    {
                        backend.Dispose();
                        btnRefresh.Content = "Connect";
                        btnRefresh.IsEnabled = true;
                        btnStart.IsEnabled = true;
                        Log("Connection failed.");
                    }
                });
            });
        }

        private void Disconnect()
        {
            _backend?.Dispose();
            _backend = null;
            btnRefresh.Content = "Connect";
            btnStart.IsEnabled = true;
            lblCurrentRpm.Text = "RPM: ---";
            lblRedline.Text = "";
            Log("Disconnected.");
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

        private void btnSmartRpm_Click(object sender, RoutedEventArgs e)
        {
            double? maxRpm = _backend?.ReadMaxRpm();
            if (!maxRpm.HasValue || maxRpm.Value <= 0)
            {
                MessageBox.Show(
                    "Redline not detected. Connect to Engine Simulator first,\nthen wait for the redline reading.",
                    "No Redline Data",
                    MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            int redlineMinus250 = (int)maxRpm.Value - 250;
            if (!lstTargetRpms.Items.Contains(redlineMinus250))
                lstTargetRpms.Items.Add(redlineMinus250);

            Log($"Auto RPM: redline={maxRpm.Value:F0}, added {redlineMinus250}");
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

            if (_backend == null)
            {
                MessageBox.Show("Connect to Engine Simulator first.", "Not connected",
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
                ProcessId = 0, // backend already connected
                TargetRpms = targets,
                CustomName = txtCarName.Text.Trim(),
                CustomPrefix = GetPrefix(),
                SampleRate = cmbSampleRate.SelectedIndex == 1 ? 48000 : 44100,
                Channels = cmbChannels.SelectedIndex == 1 ? 1 : 2,
                InteriorMode = rbInterior.IsChecked == true,
                CarType = GetCarType(),
                RecordLimiter = chkRecordLimiter.IsChecked == true,
                GeneratePowerLut = chkGeneratePowerLut.IsChecked == true,
                Interior = new InteriorSettings
                {
                    CutoffHz = (float)slCutoff.Value,
                    RumbleHz = (float)slRumbleHz.Value,
                    RumbleDb = (float)slRumbleDb.Value,
                    Res1Hz = (float)slRes1Hz.Value,
                    Res1Db = (float)slRes1Db.Value,
                    Res2Hz = (float)slRes2Hz.Value,
                    Res2Db = (float)slRes2Db.Value,
                    StereoWidth = (float)(slWidth.Value / 100.0),
                    ReverbMix = (float)(slReverbMix.Value / 100.0),
                    ReverbMs = (float)slReverbMs.Value,
                    CompRatio = (float)slCompRatio.Value,
                    CompThreshDb = (float)slCompThresh.Value,
                },
            };

            btnStart.IsEnabled = false;
            btnStop.IsEnabled = true;
            pbarProgress.BeginAnimation(ProgressBar.ValueProperty, null);
            pbarProgress.Value = 0;
            pbarProgress.Foreground = (Brush)FindResource("AccentBrush");

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
        private void SetEta(string t) => Dispatcher.BeginInvoke(() => lblEta.Text = t);

        private void StartRecProgress(double durationSec)
        {
            Dispatcher.BeginInvoke(() =>
            {
                _progressDurationSec = durationSec;
                pbarProgress.BeginAnimation(ProgressBar.ValueProperty, null);
                pbarProgress.Value = 0;
                _progressSw = Stopwatch.StartNew();
                _progressTimer?.Stop();
                _progressTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(60) };
                _progressTimer.Tick += ProgressTimer_Tick;
                _progressTimer.Start();
            });
        }

        private void StopRecProgress()
        {
            Dispatcher.BeginInvoke(() =>
            {
                _progressTimer?.Stop();
                _progressTimer = null;
                pbarProgress.BeginAnimation(ProgressBar.ValueProperty, null);
                pbarProgress.Value = 0;
            });
        }

        private void FinishRecProgress()
        {
            Dispatcher.BeginInvoke(() =>
            {
                _progressTimer?.Stop();
                _progressTimer = null;
                pbarProgress.BeginAnimation(ProgressBar.ValueProperty, null);
                pbarProgress.Value = 1;
                pbarProgress.Foreground = (Brush)FindResource("DoneBrush");
            });
        }

        private void ProgressTimer_Tick(object? sender, EventArgs e)
        {
            if (_progressSw == null || _progressDurationSec <= 0) return;
            double fraction = Math.Min(_progressSw.Elapsed.TotalSeconds / _progressDurationSec, 1.0);
            var anim = new DoubleAnimation(fraction, new Duration(TimeSpan.FromMilliseconds(60)));
            pbarProgress.BeginAnimation(ProgressBar.ValueProperty, anim);
        }

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
            bool ownsBackend = false;
            try
            {
                if (_backend != null)
                {
                    backend = _backend;
                    Log($"Mode: {backend.Name}");
                }
                else
                {
                    backend = new KeyboardBackend();
                    ownsBackend = true;
                    Log($"Mode: {backend.Name}");

                    if (!backend.Initialize(cfg, Log, ct))
                    {
                        Log("Backend initialization failed.");
                        return;
                    }
                }

                double? maxRpm = backend.ReadMaxRpm();
                if (maxRpm.HasValue)
                {
                    Log($"Engine redline: {maxRpm.Value:F0} RPM (max target: {maxRpm.Value - 250:F0})");
                    Dispatcher.BeginInvoke(() => lblRedline.Text = $"(redline: {maxRpm.Value:F0})");
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

                // ETA tracking
                int completedTargets = 0;
                var overallSw = Stopwatch.StartNew();

                // Torque data collection for power.lut
                var torqueData = new List<(int rpm, double torqueNm)>();

                // Step 4: Record each target RPM
                string prefix = cfg.CustomPrefix ?? "";
                string carName = cfg.CustomName ?? "";
                for (int i = 0; i < cfg.TargetRpms.Count; i++)
                {
                    if (ct.IsCancellationRequested) break;

                    int target = cfg.TargetRpms[i];
                    Log($"=== TARGET {i + 1}/{cfg.TargetRpms.Count}: {target} RPM ===");

                    SetStatus($"Revving to {target} RPM...");
                    SetEta("");
                    Log($"Holding R to rev to {target} RPM...");
                    backend.SetThrottle(1.0);
                    WaitForRpm(backend, cfg, target, ct);
                    if (ct.IsCancellationRequested) break;

                    double? rpmAtHold = backend.ReadRpm();

                    Log("Pressing H (Hold RPM) - throttle still held");
                    KeyboardSim.KeyPress(hwnd, KeyboardSim.VK_H, 120);
                    ct.WaitHandle.WaitOne(500);

                    double? rpmAfterHold = backend.ReadRpm();
                    if (rpmAtHold.HasValue && rpmAfterHold.HasValue)
                    {
                        Log($"Hold verified: before={rpmAtHold.Value:F0}, after={rpmAfterHold.Value:F0}, " +
                            $"delta={Math.Abs(rpmAfterHold.Value - rpmAtHold.Value):F0} RPM");
                    }

                    // Read torque for power.lut
                    if (cfg.GeneratePowerLut)
                    {
                        ct.WaitHandle.WaitOne(500);
                        double? torqueLbft = backend.ReadTorque();
                        if (torqueLbft.HasValue && torqueLbft.Value > 0)
                        {
                            double torqueNm = torqueLbft.Value * 1.35582;
                            torqueData.Add((target, torqueNm));
                            Log($"Torque at {target} RPM: {torqueNm:F1} N·m ({torqueLbft.Value:F1} lb·ft)");
                        }
                        else
                        {
                            Log($"Warning: no torque reading at {target} RPM");
                        }
                    }

                    string baseName = string.IsNullOrEmpty(carName) ? "" : $"{prefix}{carName}_";
                    string modePrefix = cfg.InteriorMode ? "int_" : "";

                    // Record LOAD
                    string loadFile = $"{baseName}{modePrefix}on_{target}.wav";
                    string loadPath = Path.Combine(cfg.OutputDir, loadFile);
                    SetStatus($"Recording {target} RPM (load)...");
                    Log($"Recording LOAD for {cfg.RecordSeconds}s -> {loadPath}");
                    RecordAudio(backend, loadPath, cfg, ct);
                    if (ct.IsCancellationRequested) break;
                    Log($"Saved: {loadPath}");

                    // Record NO-LOAD
                    Log("Releasing R (throttle off) - H still holding");
                    backend.SetThrottle(0);
                    Log("Waiting 2s for engine to settle...");
                    ct.WaitHandle.WaitOne(2000);

                    string noloadFile = $"{baseName}{modePrefix}off_{target}.wav";
                    string noloadPath = Path.Combine(cfg.OutputDir, noloadFile);
                    SetStatus($"Recording {target} RPM (no load)...");
                    Log($"Recording NO-LOAD for {cfg.RecordSeconds}s -> {noloadPath}");
                    RecordAudio(backend, noloadPath, cfg, ct);
                    if (ct.IsCancellationRequested) break;
                    Log($"Saved: {noloadPath}");

                    Log($"Target {target} RPM complete!");
                    completedTargets++;

                    // ETA update
                    double elapsed = overallSw.Elapsed.TotalSeconds;
                    double avgPerTarget = elapsed / completedTargets;
                    double remaining = avgPerTarget * (cfg.TargetRpms.Count - completedTargets);
                    double totalEst = elapsed + remaining;
                    if (completedTargets < cfg.TargetRpms.Count)
                        SetEta($"ETA: {remaining:F0}s / {totalEst:F0}s");

                    // Prepare for next RPM
                    if (i < cfg.TargetRpms.Count - 1)
                    {
                        Log("Pressing R + releasing H -> revving to next target...");
                        backend.SetThrottle(1.0);
                        ct.WaitHandle.WaitOne(16);
                        KeyboardSim.KeyPress(hwnd, KeyboardSim.VK_H, 120);
                        ct.WaitHandle.WaitOne(300);
                    }
                    else
                    {
                        KeyboardSim.KeyPress(hwnd, KeyboardSim.VK_H, 120);
                    }
                }

                // Step 5 (optional): Record engine limiter at redline
                if (cfg.RecordLimiter && !ct.IsCancellationRequested)
                {
                    double? detectedMaxRpm = backend.ReadMaxRpm();
                    int limiterRpm = detectedMaxRpm.HasValue
                        ? (int)detectedMaxRpm.Value
                        : (cfg.TargetRpms.Count > 0 ? cfg.TargetRpms[^1] + 250 : 7000);

                    Log($"=== RECORDING ENGINE LIMITER at {limiterRpm} RPM ===");
                    SetStatus($"Revving to redline ({limiterRpm} RPM)...");
                    SetEta("");

                    backend.SetThrottle(1.0);
                    WaitForRpm(backend, cfg, limiterRpm, ct);

                    Log("Waiting 0.2s for rev limiter to engage...");
                    ct.WaitHandle.WaitOne(200);

                    if (!ct.IsCancellationRequested)
                    {
                        string limiterBaseName = string.IsNullOrEmpty(carName) ? "" : $"{prefix}{carName}_";
                        string limiterModePrefix = cfg.InteriorMode ? "int_" : "";
                        string limiterFile = $"{limiterBaseName}{limiterModePrefix}engine_limiter.wav";
                        string limiterPath = Path.Combine(cfg.OutputDir, limiterFile);
                        SetStatus($"Recording engine limiter ({limiterRpm} RPM)...");
                        Log($"Recording LIMITER for {cfg.RecordSeconds}s -> {limiterPath}");
                        RecordAudio(backend, limiterPath, cfg, ct);
                        Log($"Saved: {limiterPath}");
                    }

                    backend.SetThrottle(0);
                }

                // Step 6 (optional): Generate power.lut
                if (cfg.GeneratePowerLut && torqueData.Count > 0 && !ct.IsCancellationRequested)
                {
                    string lutPath = Path.Combine(cfg.OutputDir, "power.lut");
                    Log($"=== GENERATING power.lut ({torqueData.Count} points) ===");
                    try
                    {
                        using var writer = new StreamWriter(lutPath);
                        foreach (var (rpm, torqueNm) in torqueData)
                            writer.WriteLine($"{rpm}|{torqueNm:F2}");
                        Log($"Saved: {lutPath}");
                        foreach (var (rpm, torqueNm) in torqueData)
                            Log($"  {rpm} RPM = {torqueNm:F2} N·m");
                    }
                    catch (Exception ex)
                    {
                        Log($"Error writing power.lut: {ex.Message}");
                    }
                }

                // Shutdown
                Log("=== SHUTTING DOWN ===");
                SetEta("");
                backend.SetThrottle(0);
                ct.WaitHandle.WaitOne(200);
                KeyboardSim.KeyPress(hwnd, KeyboardSim.VK_D, 120);
                ct.WaitHandle.WaitOne(200);
                KeyboardSim.KeyPress(hwnd, KeyboardSim.VK_A, 120);
                Log("Engine stopped. All recordings complete!");
                FinishRecProgress();
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
                if (ownsBackend)
                {
                    try { backend?.SetThrottle(0); } catch { }
                    backend?.Dispose();
                }
                else
                {
                    try { backend?.SetThrottle(0); } catch { }
                }
                _backend = ownsBackend ? null : backend;
                Dispatcher.BeginInvoke(() =>
                {
                    _focusMonitor?.Stop();
                    _focusMonitor = null;
                });
                _engineSimHwnd = IntPtr.Zero;
                StopRecProgress();
                SetEta("");
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
                    // Use one-sided check: only break when RPM reaches or exceeds target
                    // (overshoot tolerance to avoid triggering H too early during rapid rise)
                    if (rpm.Value >= targetRpm - cfg.RpmTolerance)
                    {
                        Log($"RPM {rpm.Value:F0} reached target {targetRpm} (±{cfg.RpmTolerance})");
                        return;
                    }
                }
                if (sw.Elapsed.TotalSeconds > 30)
                {
                    Log($"Warning: timeout waiting for {targetRpm} RPM");
                    return;
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

            // Interior cabin processor
            InteriorProcessor? interior = null;
            if (cfg.InteriorMode)
            {
                if (cfg.CarType == "Custom")
                {
                    var i = cfg.Interior;
                    interior = new InteriorProcessor(cfg.SampleRate, cfg.Channels,
                        i.CutoffHz, i.StereoWidth,
                        i.RumbleHz, i.RumbleDb,
                        i.Res1Hz, i.Res1Db,
                        i.Res2Hz, i.Res2Db,
                        i.ReverbMs, i.ReverbMix,
                        i.CompRatio, i.CompThreshDb);
                }
                else
                {
                    var (cutoff, width) = InteriorProcessor.GetPreset(cfg.CarType);
                    interior = new InteriorProcessor(cfg.SampleRate, cfg.Channels, cutoff, width);
                }
            }

            StartRecProgress(cfg.RecordSeconds);

            capture.DataAvailable += (s, e) =>
            {
                if (e.BytesRecorded == 0) return;

                if (interior != null)
                {
                    int samples = e.BytesRecorded / 4;
                    var floats = new float[samples];
                    Buffer.BlockCopy(e.Buffer, 0, floats, 0, e.BytesRecorded);
                    interior.Process(floats, samples);
                    Buffer.BlockCopy(floats, 0, e.Buffer, 0, e.BytesRecorded);
                }

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
                ct.WaitHandle.WaitOne(16);
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

        private string GetCarType()
        {
            return (cmbCarType.SelectedItem as ComboBoxItem)?.Content?.ToString() ?? "Sedan";
        }

        private sealed class ProcessItem
        {
            public int ProcessId { get; }
            public string DisplayName { get; }

            public ProcessItem(Process p)
            {
                ProcessId = p.Id;
                DisplayName = $"{p.ProcessName} (PID {p.Id})";
            }

            public override string ToString() => DisplayName;
        }

        private void Window_Loaded(object sender, RoutedEventArgs e)
        {
            _settings = AppSettings.Load();
            cmbSampleRate.SelectedIndex = _settings.SampleRate == 48000 ? 1 : 0;
            cmbChannels.SelectedIndex = _settings.Channels == 1 ? 1 : 0;
            rbInterior.IsChecked = _settings.InteriorMode;
            rbExterior.IsChecked = !_settings.InteriorMode;
            pnlCutoff.Visibility = _settings.InteriorMode ? Visibility.Visible : Visibility.Collapsed;
            chkRecordLimiter.IsChecked = _settings.RecordLimiter;
            chkGeneratePowerLut.IsChecked = _settings.GeneratePowerLut;

            for (int i = 0; i < cmbCarType.Items.Count; i++)
            {
                if ((cmbCarType.Items[i] as ComboBoxItem)?.Content?.ToString() == _settings.CarType)
                {
                    cmbCarType.SelectedIndex = i;
                    break;
                }
            }

            RefreshProfiles();
            if (!string.IsNullOrEmpty(_settings.LastProfile))
            {
                for (int i = 0; i < cmbProfiles.Items.Count; i++)
                {
                    if (cmbProfiles.Items[i]?.ToString() == _settings.LastProfile)
                    {
                        cmbProfiles.SelectedIndex = i;
                        break;
                    }
                }
            }
        }

        private void Window_Closing(object sender, System.ComponentModel.CancelEventArgs e)
        {
            _cts?.Cancel();
            _backend?.Dispose();
            _backend = null;
            _settings.SampleRate = cmbSampleRate.SelectedIndex == 1 ? 48000 : 44100;
            _settings.Channels = cmbChannels.SelectedIndex == 1 ? 1 : 2;
            _settings.InteriorMode = rbInterior.IsChecked == true;
            _settings.CarType = GetCarType();
            _settings.RecordLimiter = chkRecordLimiter.IsChecked == true;
            _settings.GeneratePowerLut = chkGeneratePowerLut.IsChecked == true;
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

        // ── Profiles ──

        private void RefreshProfiles()
        {
            cmbProfiles.Items.Clear();
            foreach (string name in RpmProfile.GetProfileNames())
                cmbProfiles.Items.Add(name);
        }

        private void btnSaveProfile_Click(object sender, RoutedEventArgs e)
        {
            string name = txtProfileName.Text.Trim();
            if (string.IsNullOrEmpty(name))
            {
                lblProfileStatus.Text = "Enter a profile name first.";
                return;
            }

            var targets = new List<int>();
            foreach (var item in lstTargetRpms.Items)
                targets.Add(Convert.ToInt32(item));

            var profile = new RpmProfile
            {
                Name = name,
                CarName = txtCarName.Text.Trim(),
                Prefix = txtPrefix.Text.Trim(),
                OutputDir = txtOutputDir.Text.Trim(),
                TargetRpms = targets,
                SampleRate = cmbSampleRate.SelectedIndex == 1 ? 48000 : 44100,
                Channels = cmbChannels.SelectedIndex == 1 ? 1 : 2,
            };
            profile.Save();

            _settings.LastProfile = name;
            RefreshProfiles();
            for (int i = 0; i < cmbProfiles.Items.Count; i++)
            {
                if (cmbProfiles.Items[i]?.ToString() == name)
                {
                    cmbProfiles.SelectedIndex = i;
                    break;
                }
            }
            lblProfileStatus.Text = $"Saved '{name}' ({targets.Count} RPM targets)";
        }

        private void btnLoadProfile_Click(object sender, RoutedEventArgs e)
        {
            if (cmbProfiles.SelectedItem is not string name)
            {
                lblProfileStatus.Text = "Select a profile to load.";
                return;
            }

            var profile = RpmProfile.Load(name);
            if (profile == null)
            {
                lblProfileStatus.Text = $"Failed to load '{name}'.";
                RefreshProfiles();
                return;
            }

            txtCarName.Text = profile.CarName;
            txtPrefix.Text = profile.Prefix;
            txtOutputDir.Text = profile.OutputDir ?? "recordings";

            lstTargetRpms.Items.Clear();
            foreach (int rpm in profile.TargetRpms)
                lstTargetRpms.Items.Add(rpm);

            cmbSampleRate.SelectedIndex = profile.SampleRate == 48000 ? 1 : 0;
            cmbChannels.SelectedIndex = profile.Channels == 1 ? 1 : 0;

            _settings.LastProfile = name;
            lblProfileStatus.Text = $"Loaded '{name}' ({profile.TargetRpms.Count} RPM targets)";
        }

        private void btnDeleteProfile_Click(object sender, RoutedEventArgs e)
        {
            if (cmbProfiles.SelectedItem is not string name)
            {
                lblProfileStatus.Text = "Select a profile to delete.";
                return;
            }

            var result = MessageBox.Show($"Delete profile '{name}'?", "Confirm",
                MessageBoxButton.YesNo, MessageBoxImage.Question);
            if (result == MessageBoxResult.Yes)
            {
                RpmProfile.Delete(name);
                RefreshProfiles();
                lblProfileStatus.Text = $"Deleted '{name}'.";
            }
        }

        private void btnNudgeUp_Click(object sender, RoutedEventArgs e)
        {
            if (lstTargetRpms.SelectedItem is int selected)
            {
                int idx = lstTargetRpms.SelectedIndex;
                lstTargetRpms.Items[idx] = selected + 250;
            }
        }

        private void btnNudgeDown_Click(object sender, RoutedEventArgs e)
        {
            if (lstTargetRpms.SelectedItem is int selected)
            {
                int idx = lstTargetRpms.SelectedIndex;
                int newVal = selected - 250;
                if (newVal > 0) lstTargetRpms.Items[idx] = newVal;
            }
        }

        private void cmbChannels_SelectionChanged(object sender, SelectionChangedEventArgs e) { }

        private void rbMode_Checked(object sender, RoutedEventArgs e)
        {
            if (pnlCutoff != null)
                pnlCutoff.Visibility = rbInterior.IsChecked == true ? Visibility.Visible : Visibility.Collapsed;
        }

        private void cmbCarType_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (pnlCustom == null) return;
            string? type = (cmbCarType.SelectedItem as ComboBoxItem)?.Content?.ToString();
            pnlCustom.Visibility = type == "Custom" ? Visibility.Visible : Visibility.Collapsed;
        }

        private void slCustom_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            if (!this.IsLoaded) return;
            if (lblCutoff == null) return;
            lblCutoff.Text = $"{(int)slCutoff.Value} Hz";
            lblRumbleHz.Text = $"{(int)slRumbleHz.Value} Hz";
            lblRumbleDb.Text = $"+{(int)slRumbleDb.Value} dB";
            lblRes1Hz.Text = $"{(int)slRes1Hz.Value} Hz";
            lblRes1Db.Text = $"+{(int)slRes1Db.Value} dB";
            lblRes2Hz.Text = $"{(int)slRes2Hz.Value} Hz";
            lblRes2Db.Text = $"+{(int)slRes2Db.Value} dB";
            lblWidth.Text = $"{(int)slWidth.Value}%";
            lblReverbMix.Text = $"{(int)slReverbMix.Value}%";
            lblReverbMs.Text = $"{(int)slReverbMs.Value} ms";
            lblCompRatio.Text = $"{(int)slCompRatio.Value}:1";
            lblCompThresh.Text = $"{(int)slCompThresh.Value} dB";
        }
    }
}
