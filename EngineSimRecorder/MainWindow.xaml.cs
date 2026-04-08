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
using EngineSimRecorder.Pages;
using NAudio.Wave;
using Wpf.Ui.Controls;

namespace EngineSimRecorder
{
    public partial class MainWindow : FluentWindow
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

        // Page references
        private RecorderPage? _recorderPage;
        private LogPage? _logPage;
        private OptionsPage? _optionsPage;

        public MainWindow()
        {
            InitializeComponent();

            string iconPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "icon.ico");
            if (File.Exists(iconPath))
                this.Icon = new System.Windows.Media.Imaging.BitmapImage(new Uri(iconPath));

            Loaded += Window_Loaded;
            Closing += Window_Closing;

            // Pre-create pages
            _recorderPage = new RecorderPage();
            _logPage = new LogPage();
            _optionsPage = new OptionsPage();

            // Set initial page
            RootNavigation.Content = _recorderPage;

            // Wire up events after page is loaded
            _recorderPage.Loaded += RecorderPage_Loaded;
            _optionsPage.Loaded += OptionsPage_Loaded;
        }

        private void RootNavigation_SelectedChanged(NavigationView sender, RoutedEventArgs args)
        {
            if (sender.SelectedItem is NavigationViewItem item)
            {
                var tag = item.Tag?.ToString();
                switch (tag)
                {
                    case "recorder":
                        RootNavigation.Content = _recorderPage;
                        break;
                    case "log":
                        RootNavigation.Content = _logPage;
                        break;
                    case "options":
                        RootNavigation.Content = _optionsPage;
                        break;
                }
            }
        }

        // ── Page loaded hooks (wire up events after XAML parsed) ──

        private void RecorderPage_Loaded(object sender, RoutedEventArgs e)
        {
            var p = _recorderPage!;
            p.btnRefresh.Click -= btnRefresh_Click;
            p.btnRefresh.Click += btnRefresh_Click;
            p.btnBrowseOutput.Click -= btnBrowseOutput_Click;
            p.btnBrowseOutput.Click += btnBrowseOutput_Click;
            p.btnOpenOutput.Click -= btnOpenOutput_Click;
            p.btnOpenOutput.Click += btnOpenOutput_Click;
            p.btnAddRpm.Click -= btnAddRpm_Click;
            p.btnAddRpm.Click += btnAddRpm_Click;
            p.btnEditRpm.Click -= btnEditRpm_Click;
            p.btnEditRpm.Click += btnEditRpm_Click;
            p.btnRemoveRpm.Click -= btnRemoveRpm_Click;
            p.btnRemoveRpm.Click += btnRemoveRpm_Click;
            p.btnSortRpm.Click -= btnSortRpm_Click;
            p.btnSortRpm.Click += btnSortRpm_Click;
            p.btnClearRpm.Click -= btnClearRpm_Click;
            p.btnClearRpm.Click += btnClearRpm_Click;
            p.btnPreset.Click -= btnPreset_Click;
            p.btnPreset.Click += btnPreset_Click;
            p.btnSmartRpm.Click -= btnSmartRpm_Click;
            p.btnSmartRpm.Click += btnSmartRpm_Click;
            p.btnNudgeUp.Click -= btnNudgeUp_Click;
            p.btnNudgeUp.Click += btnNudgeUp_Click;
            p.btnNudgeDown.Click -= btnNudgeDown_Click;
            p.btnNudgeDown.Click += btnNudgeDown_Click;
            p.lstTargetRpms.SelectionChanged -= lstTargetRpms_SelectionChanged;
            p.lstTargetRpms.SelectionChanged += lstTargetRpms_SelectionChanged;
            p.lstTargetRpms.MouseDoubleClick -= btnEditRpm_Click;
            p.lstTargetRpms.MouseDoubleClick += btnEditRpm_Click;
            p.btnStart.Click -= btnStart_Click;
            p.btnStart.Click += btnStart_Click;
            p.btnStop.Click -= btnStop_Click;
            p.btnStop.Click += btnStop_Click;
            p.chkStayOnTop.Checked -= chkStayOnTop_Changed;
            p.chkStayOnTop.Unchecked -= chkStayOnTop_Changed;
            p.chkStayOnTop.Checked += chkStayOnTop_Changed;
            p.chkStayOnTop.Unchecked += chkStayOnTop_Changed;

            // Default RPM targets
            if (p.lstTargetRpms.Items.Count == 0)
            {
                foreach (int rpm in new[] { 1500, 2000, 3000, 4000, 5000, 6000 })
                    p.lstTargetRpms.Items.Add(rpm);
            }
            p.btnStart.IsEnabled = false;
        }

        private void OptionsPage_Loaded(object sender, RoutedEventArgs e)
        {
            var o = _optionsPage!;
            o.btnLoadProfile.Click -= btnLoadProfile_Click;
            o.btnLoadProfile.Click += btnLoadProfile_Click;
            o.btnDeleteProfile.Click -= btnDeleteProfile_Click;
            o.btnDeleteProfile.Click += btnDeleteProfile_Click;
            o.btnSaveProfile.Click -= btnSaveProfile_Click;
            o.btnSaveProfile.Click += btnSaveProfile_Click;
            o.rbExterior.Checked -= rbMode_Checked;
            o.rbExterior.Checked += rbMode_Checked;
            o.rbInterior.Checked -= rbMode_Checked;
            o.rbInterior.Checked += rbMode_Checked;
            o.cmbCarType.SelectionChanged -= cmbCarType_SelectionChanged;
            o.cmbCarType.SelectionChanged += cmbCarType_SelectionChanged;
            o.cmbChannels.SelectionChanged -= cmbChannels_SelectionChanged;
            o.cmbChannels.SelectionChanged += cmbChannels_SelectionChanged;
            o.slCutoff.ValueChanged -= slCustom_ValueChanged;
            o.slCutoff.ValueChanged += slCustom_ValueChanged;
            o.slRumbleHz.ValueChanged -= slCustom_ValueChanged;
            o.slRumbleHz.ValueChanged += slCustom_ValueChanged;
            o.slRumbleDb.ValueChanged -= slCustom_ValueChanged;
            o.slRumbleDb.ValueChanged += slCustom_ValueChanged;
            o.slRes1Hz.ValueChanged -= slCustom_ValueChanged;
            o.slRes1Hz.ValueChanged += slCustom_ValueChanged;
            o.slRes1Db.ValueChanged -= slCustom_ValueChanged;
            o.slRes1Db.ValueChanged += slCustom_ValueChanged;
            o.slRes2Hz.ValueChanged -= slCustom_ValueChanged;
            o.slRes2Hz.ValueChanged += slCustom_ValueChanged;
            o.slRes2Db.ValueChanged -= slCustom_ValueChanged;
            o.slRes2Db.ValueChanged += slCustom_ValueChanged;
            o.slWidth.ValueChanged -= slCustom_ValueChanged;
            o.slWidth.ValueChanged += slCustom_ValueChanged;
            o.slReverbMix.ValueChanged -= slCustom_ValueChanged;
            o.slReverbMix.ValueChanged += slCustom_ValueChanged;
            o.slReverbMs.ValueChanged -= slCustom_ValueChanged;
            o.slReverbMs.ValueChanged += slCustom_ValueChanged;
            o.slCompRatio.ValueChanged -= slCustom_ValueChanged;
            o.slCompRatio.ValueChanged += slCustom_ValueChanged;
            o.slCompThresh.ValueChanged -= slCustom_ValueChanged;
            o.slCompThresh.ValueChanged += slCustom_ValueChanged;
        }

        // ── Process ──

        private void RefreshProcessList()
        {
            var p = _recorderPage!;
            p.cmbProcess.Items.Clear();
            foreach (var name in new[] { "engine-sim-app", "engine-sim", "engine_sim", "EngineSimulator" })
            {
                foreach (var proc in Process.GetProcessesByName(name))
                    p.cmbProcess.Items.Add(new ProcessItem(proc));
            }
            if (p.cmbProcess.Items.Count > 0)
                p.cmbProcess.SelectedIndex = 0;
        }

        private void btnRefresh_Click(object sender, RoutedEventArgs e)
        {
            var p = _recorderPage!;
            if (_backend != null)
            {
                if (_cts != null)
                {
                    MessageBox.Show("Stop recording first.", "Busy", MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }
                Disconnect();
                return;
            }

            RefreshProcessList();
            if (p.cmbProcess.Items.Count == 0)
            {
                MessageBox.Show("No Engine Simulator process found.", "Not Found", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            if (p.cmbProcess.SelectedItem is not ProcessItem sel)
            {
                MessageBox.Show("Select a process.", "No Selection", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            p.btnRefresh.IsEnabled = false;
            p.btnRefresh.Content = "Connecting...";
            _logPage?.Clear();

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
                        p.btnRefresh.Content = "Disconnect";
                        p.btnRefresh.IsEnabled = true;
                        p.btnStart.IsEnabled = true;

                        double? maxRpm = backend.ReadMaxRpm();
                        if (maxRpm.HasValue)
                        {
                            Log($"Connected. Engine redline: {maxRpm.Value:F0} RPM");
                            p.lblRedline.Text = $"(redline: {maxRpm.Value:F0})";
                        }
                        else
                        {
                            Log("Connected. Waiting for redline data...");
                        }
                    }
                    else
                    {
                        backend.Dispose();
                        p.btnRefresh.Content = "Connect";
                        p.btnRefresh.IsEnabled = true;
                        p.btnStart.IsEnabled = true;
                        Log("Connection failed.");
                    }
                });
            });
        }

        private void Disconnect()
        {
            var p = _recorderPage!;
            _backend?.Dispose();
            _backend = null;
            p.btnRefresh.Content = "Connect";
            p.btnStart.IsEnabled = true;
            p.lblCurrentRpm.Text = "RPM: ---";
            p.lblRedline.Text = "";
            Log("Disconnected.");
        }

        // ── Output ──

        private void btnBrowseOutput_Click(object sender, RoutedEventArgs e)
        {
            var dlg = new System.Windows.Forms.FolderBrowserDialog { Description = "Select output folder" };
            if (dlg.ShowDialog() == System.Windows.Forms.DialogResult.OK)
                _recorderPage!.txtOutputDir.Text = dlg.SelectedPath;
        }

        private void btnOpenOutput_Click(object sender, RoutedEventArgs e)
        {
            string dir = _recorderPage!.txtOutputDir.Text.Trim();
            if (!Directory.Exists(dir))
                Directory.CreateDirectory(dir);
            Process.Start("explorer.exe", dir);
        }

        // ── RPM ──

        private void btnAddRpm_Click(object sender, RoutedEventArgs e)
        {
            var p = _recorderPage!;
            if (int.TryParse(p.txtRpmInput.Text, out int rpm) && !p.lstTargetRpms.Items.Contains(rpm))
                p.lstTargetRpms.Items.Add(rpm);
        }

        private void btnRemoveRpm_Click(object sender, RoutedEventArgs e)
        {
            var p = _recorderPage!;
            if (p.lstTargetRpms.SelectedItem != null)
                p.lstTargetRpms.Items.Remove(p.lstTargetRpms.SelectedItem);
        }

        private void btnPreset_Click(object sender, RoutedEventArgs e)
        {
            var p = _recorderPage!;
            if (sender is Button btn && int.TryParse(btn.Content?.ToString()?.Replace("K", "000"), out int rpm))
            {
                if (!p.lstTargetRpms.Items.Contains(rpm))
                    p.lstTargetRpms.Items.Add(rpm);
            }
        }

        private void btnSortRpm_Click(object sender, RoutedEventArgs e)
        {
            var p = _recorderPage!;
            var items = new List<int>();
            foreach (var item in p.lstTargetRpms.Items)
                items.Add(Convert.ToInt32(item));
            items.Sort();
            p.lstTargetRpms.Items.Clear();
            foreach (var rpm in items)
                p.lstTargetRpms.Items.Add(rpm);
        }

        private void btnClearRpm_Click(object sender, RoutedEventArgs e)
        {
            _recorderPage!.lstTargetRpms.Items.Clear();
        }

        private void btnSmartRpm_Click(object sender, RoutedEventArgs e)
        {
            double? maxRpm = _backend?.ReadMaxRpm();
            if (!maxRpm.HasValue || maxRpm.Value <= 0)
            {
                MessageBox.Show("Redline not detected. Connect to Engine Simulator first,\nthen wait for the redline reading.",
                    "No Redline Data", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            int redlineMinus250 = (int)maxRpm.Value - 250;
            var p = _recorderPage!;
            if (!p.lstTargetRpms.Items.Contains(redlineMinus250))
                p.lstTargetRpms.Items.Add(redlineMinus250);

            Log($"Auto RPM: redline={maxRpm.Value:F0}, added {redlineMinus250}");
        }

        private void btnEditRpm_Click(object sender, RoutedEventArgs e)
        {
            var p = _recorderPage!;
            if (p.lstTargetRpms.SelectedItem is not int selected) return;
            if (int.TryParse(p.txtRpmInput.Text, out int newVal))
            {
                int idx = p.lstTargetRpms.SelectedIndex;
                p.lstTargetRpms.Items[idx] = newVal;
            }
        }

        private void lstTargetRpms_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            var p = _recorderPage!;
            if (p.lstTargetRpms.SelectedItem is int selected)
                p.txtRpmInput.Text = selected.ToString();
        }

        // ── Start / Stop ──

        private void btnStart_Click(object sender, RoutedEventArgs e)
        {
            var p = _recorderPage!;
            var o = _optionsPage!;

            if (p.lstTargetRpms.Items.Count == 0)
            {
                MessageBox.Show("Add at least one target RPM.", "No targets", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            if (_backend == null)
            {
                MessageBox.Show("Connect to Engine Simulator first.", "Not connected", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            string outputDir = p.txtOutputDir.Text.Trim();
            Directory.CreateDirectory(outputDir);

            var targets = new List<int>();
            foreach (var item in p.lstTargetRpms.Items)
                targets.Add(Convert.ToInt32(item));
            targets.Sort();

            var cfg = new RecorderConfig
            {
                OutputDir = outputDir,
                ProcessId = 0,
                TargetRpms = targets,
                CustomName = p.txtCarName.Text.Trim(),
                CustomPrefix = GetPrefix(),
                SampleRate = o.cmbSampleRate.SelectedIndex == 1 ? 48000 : 44100,
                Channels = o.cmbChannels.SelectedIndex == 1 ? 1 : 2,
                InteriorMode = o.rbInterior.IsChecked == true,
                CarType = GetCarType(),
                RecordLimiter = o.chkRecordLimiter.IsChecked == true,
                GeneratePowerLut = o.chkGeneratePowerLut.IsChecked == true,
                Interior = new InteriorSettings
                {
                    CutoffHz = (float)o.slCutoff.Value,
                    RumbleHz = (float)o.slRumbleHz.Value,
                    RumbleDb = (float)o.slRumbleDb.Value,
                    Res1Hz = (float)o.slRes1Hz.Value,
                    Res1Db = (float)o.slRes1Db.Value,
                    Res2Hz = (float)o.slRes2Hz.Value,
                    Res2Db = (float)o.slRes2Db.Value,
                    StereoWidth = (float)(o.slWidth.Value / 100.0),
                    ReverbMix = (float)(o.slReverbMix.Value / 100.0),
                    ReverbMs = (float)o.slReverbMs.Value,
                    CompRatio = (float)o.slCompRatio.Value,
                    CompThreshDb = (float)o.slCompThresh.Value,
                },
            };

            p.btnStart.IsEnabled = false;
            p.btnStop.IsEnabled = true;
            p.pbarProgress.BeginAnimation(ProgressBar.ValueProperty, null);
            p.pbarProgress.Value = 0;

            _cts = new CancellationTokenSource();
            _workerTask = Task.Run(() => RunAsync(cfg, _cts.Token));
        }

        private void btnStop_Click(object sender, RoutedEventArgs e) => _cts?.Cancel();

        private void chkStayOnTop_Changed(object sender, RoutedEventArgs e)
        {
            Topmost = _recorderPage!.chkStayOnTop.IsChecked == true;
        }

        // ── UI Helpers ──

        private void Log(string msg)
        {
            string line = $"[{DateTime.Now:HH:mm:ss}] {msg}";
            Dispatcher.BeginInvoke(() => _logPage?.AppendLog(line));
        }

        private void SetStatus(string t) => Dispatcher.BeginInvoke(() => _recorderPage!.lblStatus.Text = t);
        private void SetRpm(string t) => Dispatcher.BeginInvoke(() => _recorderPage!.lblCurrentRpm.Text = t);
        private void SetEta(string t) => Dispatcher.BeginInvoke(() => _recorderPage!.lblEta.Text = t);

        private void StartRecProgress(double durationSec)
        {
            Dispatcher.BeginInvoke(() =>
            {
                var p = _recorderPage!;
                _progressDurationSec = durationSec;
                p.pbarProgress.BeginAnimation(ProgressBar.ValueProperty, null);
                p.pbarProgress.Value = 0;
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
                var p = _recorderPage!;
                p.pbarProgress.BeginAnimation(ProgressBar.ValueProperty, null);
                p.pbarProgress.Value = 0;
            });
        }

        private void FinishRecProgress()
        {
            Dispatcher.BeginInvoke(() =>
            {
                _progressTimer?.Stop();
                _progressTimer = null;
                var p = _recorderPage!;
                p.pbarProgress.BeginAnimation(ProgressBar.ValueProperty, null);
                p.pbarProgress.Value = 1;
                p.pbarProgress.Foreground = (Brush)FindResource("DoneBrush");
            });
        }

        private void ProgressTimer_Tick(object? sender, EventArgs e)
        {
            if (_progressSw == null || _progressDurationSec <= 0) return;
            double fraction = Math.Min(_progressSw.Elapsed.TotalSeconds / _progressDurationSec, 1.0);
            var anim = new DoubleAnimation(fraction, new Duration(TimeSpan.FromMilliseconds(60)));
            _recorderPage!.pbarProgress.BeginAnimation(ProgressBar.ValueProperty, anim);
        }

        private void ResetControls() => Dispatcher.BeginInvoke(() =>
        {
            var p = _recorderPage!;
            p.btnStart.IsEnabled = true;
            p.btnStop.IsEnabled = false;
            p.lblStatus.Text = "Done.";
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
                    Dispatcher.BeginInvoke(() => _recorderPage!.lblRedline.Text = $"(redline: {maxRpm.Value:F0})");
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
            string p = _recorderPage!.txtPrefix.Text.Trim();
            if (string.IsNullOrEmpty(p)) return "";
            return p.EndsWith("_") ? p : p + "_";
        }

        private string GetCarType()
        {
            return (_optionsPage!.cmbCarType.SelectedItem as ComboBoxItem)?.Content?.ToString() ?? "Sedan";
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
            var o = _optionsPage!;
            o.cmbSampleRate.SelectedIndex = _settings.SampleRate == 48000 ? 1 : 0;
            o.cmbChannels.SelectedIndex = _settings.Channels == 1 ? 1 : 0;
            o.rbInterior.IsChecked = _settings.InteriorMode;
            o.rbExterior.IsChecked = !_settings.InteriorMode;
            o.pnlCutoff.Visibility = _settings.InteriorMode ? Visibility.Visible : Visibility.Collapsed;
            o.chkRecordLimiter.IsChecked = _settings.RecordLimiter;
            o.chkGeneratePowerLut.IsChecked = _settings.GeneratePowerLut;

            for (int i = 0; i < o.cmbCarType.Items.Count; i++)
            {
                if ((o.cmbCarType.Items[i] as ComboBoxItem)?.Content?.ToString() == _settings.CarType)
                {
                    o.cmbCarType.SelectedIndex = i;
                    break;
                }
            }

            RefreshProfiles();
            if (!string.IsNullOrEmpty(_settings.LastProfile))
            {
                for (int i = 0; i < o.cmbProfiles.Items.Count; i++)
                {
                    if (o.cmbProfiles.Items[i]?.ToString() == _settings.LastProfile)
                    {
                        o.cmbProfiles.SelectedIndex = i;
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
            var o = _optionsPage!;
            _settings.SampleRate = o.cmbSampleRate.SelectedIndex == 1 ? 48000 : 44100;
            _settings.Channels = o.cmbChannels.SelectedIndex == 1 ? 1 : 2;
            _settings.InteriorMode = o.rbInterior.IsChecked == true;
            _settings.CarType = GetCarType();
            _settings.RecordLimiter = o.chkRecordLimiter.IsChecked == true;
            _settings.GeneratePowerLut = o.chkGeneratePowerLut.IsChecked == true;
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
                    _recorderPage!.lblStatus.Foreground = new SolidColorBrush(Colors.OrangeRed);
                    Log("⚠ Neither window focused — click this or Engine Sim");
                }
            }
            else
            {
                if (_focusWarned)
                {
                    _focusWarned = false;
                    _recorderPage!.lblStatus.Foreground = Brushes.Gray;
                }
            }
        }

        // ── Profiles ──

        private void RefreshProfiles()
        {
            var o = _optionsPage!;
            o.cmbProfiles.Items.Clear();
            foreach (string name in RpmProfile.GetProfileNames())
                o.cmbProfiles.Items.Add(name);
        }

        private void btnSaveProfile_Click(object sender, RoutedEventArgs e)
        {
            var p = _recorderPage!;
            var o = _optionsPage!;

            string name = o.txtProfileName.Text.Trim();
            if (string.IsNullOrEmpty(name))
            {
                o.lblProfileStatus.Text = "Enter a profile name first.";
                return;
            }

            var targets = new List<int>();
            foreach (var item in p.lstTargetRpms.Items)
                targets.Add(Convert.ToInt32(item));

            var profile = new RpmProfile
            {
                Name = name,
                CarName = p.txtCarName.Text.Trim(),
                Prefix = p.txtPrefix.Text.Trim(),
                OutputDir = p.txtOutputDir.Text.Trim(),
                TargetRpms = targets,
                SampleRate = o.cmbSampleRate.SelectedIndex == 1 ? 48000 : 44100,
                Channels = o.cmbChannels.SelectedIndex == 1 ? 1 : 2,
            };
            profile.Save();

            _settings.LastProfile = name;
            RefreshProfiles();
            for (int i = 0; i < o.cmbProfiles.Items.Count; i++)
            {
                if (o.cmbProfiles.Items[i]?.ToString() == name)
                {
                    o.cmbProfiles.SelectedIndex = i;
                    break;
                }
            }
            o.lblProfileStatus.Text = $"Saved '{name}' ({targets.Count} RPM targets)";
        }

        private void btnLoadProfile_Click(object sender, RoutedEventArgs e)
        {
            var p = _recorderPage!;
            var o = _optionsPage!;

            if (o.cmbProfiles.SelectedItem is not string name)
            {
                o.lblProfileStatus.Text = "Select a profile to load.";
                return;
            }

            var profile = RpmProfile.Load(name);
            if (profile == null)
            {
                o.lblProfileStatus.Text = $"Failed to load '{name}'.";
                RefreshProfiles();
                return;
            }

            p.txtCarName.Text = profile.CarName;
            p.txtPrefix.Text = profile.Prefix;
            p.txtOutputDir.Text = profile.OutputDir ?? "recordings";

            p.lstTargetRpms.Items.Clear();
            foreach (int rpm in profile.TargetRpms)
                p.lstTargetRpms.Items.Add(rpm);

            o.cmbSampleRate.SelectedIndex = profile.SampleRate == 48000 ? 1 : 0;
            o.cmbChannels.SelectedIndex = profile.Channels == 1 ? 1 : 0;

            _settings.LastProfile = name;
            o.lblProfileStatus.Text = $"Loaded '{name}' ({profile.TargetRpms.Count} RPM targets)";
        }

        private void btnDeleteProfile_Click(object sender, RoutedEventArgs e)
        {
            var o = _optionsPage!;

            if (o.cmbProfiles.SelectedItem is not string name)
            {
                o.lblProfileStatus.Text = "Select a profile to delete.";
                return;
            }

            var result = MessageBox.Show($"Delete profile '{name}'?", "Confirm",
                MessageBoxButton.YesNo, MessageBoxImage.Question);
            if (result == MessageBoxResult.Yes)
            {
                RpmProfile.Delete(name);
                RefreshProfiles();
                o.lblProfileStatus.Text = $"Deleted '{name}'.";
            }
        }

        private void btnNudgeUp_Click(object sender, RoutedEventArgs e)
        {
            var p = _recorderPage!;
            if (p.lstTargetRpms.SelectedItem is int selected)
            {
                int idx = p.lstTargetRpms.SelectedIndex;
                p.lstTargetRpms.Items[idx] = selected + 250;
            }
        }

        private void btnNudgeDown_Click(object sender, RoutedEventArgs e)
        {
            var p = _recorderPage!;
            if (p.lstTargetRpms.SelectedItem is int selected)
            {
                int idx = p.lstTargetRpms.SelectedIndex;
                int newVal = selected - 250;
                if (newVal > 0) p.lstTargetRpms.Items[idx] = newVal;
            }
        }

        private void cmbChannels_SelectionChanged(object sender, SelectionChangedEventArgs e) { }

        private void rbMode_Checked(object sender, RoutedEventArgs e)
        {
            var o = _optionsPage!;
            if (o.pnlCutoff != null)
                o.pnlCutoff.Visibility = o.rbInterior.IsChecked == true ? Visibility.Visible : Visibility.Collapsed;
        }

        private void cmbCarType_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            var o = _optionsPage!;
            if (o.pnlCustom == null) return;
            string? type = (o.cmbCarType.SelectedItem as ComboBoxItem)?.Content?.ToString();
            o.pnlCustom.Visibility = type == "Custom" ? Visibility.Visible : Visibility.Collapsed;
        }

        private void slCustom_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            var o = _optionsPage!;
            if (!this.IsLoaded || o == null) return;
            o.lblCutoff.Text = $"{(int)o.slCutoff.Value} Hz";
            o.lblRumbleHz.Text = $"{(int)o.slRumbleHz.Value} Hz";
            o.lblRumbleDb.Text = $"+{(int)o.slRumbleDb.Value} dB";
            o.lblRes1Hz.Text = $"{(int)o.slRes1Hz.Value} Hz";
            o.lblRes1Db.Text = $"+{(int)o.slRes1Db.Value} dB";
            o.lblRes2Hz.Text = $"{(int)o.slRes2Hz.Value} Hz";
            o.lblRes2Db.Text = $"+{(int)o.slRes2Db.Value} dB";
            o.lblWidth.Text = $"{(int)o.slWidth.Value}%";
            o.lblReverbMix.Text = $"{(int)o.slReverbMix.Value}%";
            o.lblReverbMs.Text = $"{(int)o.slReverbMs.Value} ms";
            o.lblCompRatio.Text = $"{(int)o.slCompRatio.Value}:1";
            o.lblCompThresh.Text = $"{(int)o.slCompThresh.Value} dB";
        }
    }
}
