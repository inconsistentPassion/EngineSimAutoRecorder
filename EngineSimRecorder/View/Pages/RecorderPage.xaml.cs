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
using EngineSimRecorder.View.Pages;
using NAudio.Wave;
using Wpf.Ui.Appearance;
using Wpf.Ui.Controls;
using Wpf.Ui;

namespace EngineSimRecorder.View.Pages;

public partial class RecorderPage : Page
{
    public static RecorderPage? Instance { get; private set; }
    private readonly ISnackbarService _snackbarService;
    private readonly IContentDialogService _dialogService;
    private CancellationTokenSource? _cts;
    private Task? _workerTask;
    private DispatcherTimer? _progressTimer;
    private Stopwatch? _progressSw;
    private double _progressDurationSec;
    private KeyboardBackend? _backend;
    internal KeyboardBackend? Backend => _backend;
    internal CancellationTokenSource? Cts => _cts;

    public RecorderPage()
    {
        Instance = this;
        InitializeComponent();
        _snackbarService = App.GetService<ISnackbarService>();
        _dialogService = App.GetService<IContentDialogService>();
        Loaded += OnLoaded;
    }

    private bool _wired = false;
    private void OnLoaded(object sender, RoutedEventArgs e)
    {
        if (_wired) return;
        _wired = true;
        WireEvents();
        // Default RPM targets
        if (lstRpm.Items.Count == 0)
            foreach (int rpm in new[] { 1500, 2000, 3000, 4000, 5000, 6000 })
                lstRpm.Items.Add(rpm);
        btnStart.IsEnabled = false;
    }

    private void WireEvents()
    {
        btnConnect.Click += btnConnect_Click;
        btnBrowse.Click += btnBrowse_Click;
        btnOpenFolder.Click += btnOpenFolder_Click;
        btnAdd.Click += btnAdd_Click;
        btnEdit.Click += btnEdit_Click;
        btnRemove.Click += btnRemove_Click;
        btnSort.Click += btnSort_Click;
        btnClear.Click += btnClear_Click;
        btnNudgeUp.Click += btnNudgeUp_Click;
        btnNudgeDown.Click += btnNudgeDown_Click;
        btnAuto.Click += btnAuto_Click;
        btnStart.Click += btnStart_Click;
        btnStop.Click += btnStop_Click;
        chkTop.Checked += chkTop_Changed;
        chkTop.Unchecked += chkTop_Changed;
        lstRpm.SelectionChanged += lstRpm_SelectionChanged;
        lstRpm.MouseDoubleClick += btnEdit_Click;
        // Preset buttons
        foreach (var child in FindVisualChildren<System.Windows.Controls.Button>(this))
            if (child.Tag is string tag && int.TryParse(tag, out _))
                child.Click += btnPreset_Click;
    }

    private static IEnumerable<T> FindVisualChildren<T>(DependencyObject depObj) where T : DependencyObject
    {
        if (depObj == null) yield break;
        for (int i = 0; i < VisualTreeHelper.GetChildrenCount(depObj); i++)
        {
            var child = VisualTreeHelper.GetChild(depObj, i);
            if (child is T t) yield return t;
            foreach (var c in FindVisualChildren<T>(child)) yield return c;
        }
    }

    private void Log(string msg)
    {
        string line = $"[{DateTime.Now:HH:mm:ss}] {msg}";
        Dispatcher.BeginInvoke(() =>
        {
            LogPage.Instance?.AppendLog(line);
            // Show last message in Recorder page status
            lblStatus.Text = msg;
        });
        // Buffer for LogPage if it doesn't exist yet
        if (LogPage.Instance == null)
            LogPage.AppendBuffered(line);
    }

    // ── Process ──
    private Process? FindEngineProcess()
    {
        foreach (var name in new[] { "engine-sim-app", "engine-sim", "engine_sim", "EngineSimulator" })
        {
            var procs = Process.GetProcessesByName(name);
            if (procs.Length > 0) return procs[0];
        }
        return null;
    }

    private async void btnConnect_Click(object sender, RoutedEventArgs e)
    {
        if (_backend != null)
        {
            // Show confirmation if recording is in progress
            if (_cts != null && !_cts.IsCancellationRequested)
            {
                var uiMessageBox = new Wpf.Ui.Controls.MessageBox
                {
                    Title = "Stop Recording & Disconnect",
                    Content = "A recording is currently in progress. Do you want to stop it and disconnect?",
                    PrimaryButtonText = "Yes",
                    CloseButtonText = "No"
                };
                
                var result = await uiMessageBox.ShowDialogAsync();
                
                if (result != Wpf.Ui.Controls.MessageBoxResult.Primary)
                    return;
                    
                // Stop recording first before disconnecting
                _cts?.Cancel();
                Log("Stopping recording before disconnect...");
                lblStatus.Text = "Stopping recording...";
                btnConnect.IsEnabled = false;
                btnConnect.Content = "Disconnecting...";
                
                // Wait a moment for the recording to stop gracefully
                await Task.Delay(800);
                await Dispatcher.InvokeAsync(() => Disconnect());
                return;
            }
            
            Disconnect();
            return;
        }

        var proc = FindEngineProcess();
        if (proc == null) 
        { 
            _snackbarService.Show("Not Found", "No Engine Simulator process found.\nMake sure the simulator is running before connecting.", ControlAppearance.Info, new SymbolIcon(SymbolRegular.Search24), TimeSpan.FromSeconds(5));
            return; 
        }

        btnConnect.IsEnabled = false;
        btnConnect.Content = "Connecting...";
        LogPage.Instance?.Clear();
        LogPage.ClearBuffer();

        var cfg = new RecorderConfig { ProcessId = proc.Id };
        var displayName = $"{proc.ProcessName} (PID {proc.Id})";
        _ = Task.Run(() =>
        {
            try
            {
                var backend = new KeyboardBackend();
                bool ok = backend.Initialize(cfg, msg => Log(msg), CancellationToken.None);
                Dispatcher.BeginInvoke(() =>
                {
                    if (ok)
                    {
                        _backend = backend;
                        SetConnStatus(true);
                        btnStart.IsEnabled = true;
                        double? maxRpm = backend.ReadMaxRpm();
                        lblConnDetail.Text = displayName;
                        if (maxRpm.HasValue) { Log($"Connected. Engine redline: {maxRpm.Value:F0} RPM"); lblRedline.Text = $"(redline: {maxRpm.Value:F0})"; }
                        else Log("Connected. Waiting for redline data...");
                    }
                    else 
                    { 
                        backend.Dispose(); 
                        btnConnect.Content = "Connect"; 
                        btnConnect.IsEnabled = true; 
                        Log("Connection failed."); 
                        _snackbarService.Show("Connection Failed", "Connection to Engine Simulator failed.\nSee the status label above or the Log tab for details.", ControlAppearance.Danger, new SymbolIcon(SymbolRegular.ErrorCircle24), TimeSpan.FromSeconds(5));
                    }
                });
            }
            catch (Exception ex)
            {
                Log($"Connection error: {ex.Message}");
                Dispatcher.BeginInvoke(() =>
                {
                    SetConnStatus(false);
                    _snackbarService.Show("Error", $"Connection error:\n{ex.Message}", ControlAppearance.Danger, new SymbolIcon(SymbolRegular.ErrorCircle24), TimeSpan.FromSeconds(5));
                });
            }
        });
    }

    private void Disconnect()
    {
        try
        {
            // Stop any background tasks first
            if (_workerTask != null && !_workerTask.IsCompleted)
            {
                _cts?.Cancel();
                try
                {
                    _workerTask.Wait(TimeSpan.FromSeconds(3));
                }
                catch (Exception ex)
                {
                    Log($"Warning while stopping worker: {ex.Message}");
                }
            }

            // Dispose backend and clear state
            _backend?.Dispose();
            _backend = null;

            // Reset UI state
            SetConnStatus(false);
            btnStart.IsEnabled = false;
            btnStop.IsEnabled = false;
            lblRpm.Text = "RPM: ---";
            lblRedline.Text = "";
            lblStatus.Text = "Disconnected.";
            lblConnDetail.Text = "";
            pbar.Value = 0;

            Log("Disconnected from Engine Simulator.");
        }
        catch (Exception ex)
        {
            Log($"Error during disconnect: {ex.Message}");
            // Force reset UI even on error
            _backend = null;
            btnConnect.Content = "Connect";
            btnConnect.IsEnabled = true;
            btnStart.IsEnabled = false;
            btnStop.IsEnabled = false;
        }
    }

    // ── Profile State Management ──
    internal RpmProfile ExportProfileState()
    {
        var targets = new List<int>();
        foreach (var item in lstRpm.Items) targets.Add(Convert.ToInt32(item));
        return new RpmProfile
        {
            CarName = txtCarName.Text.Trim(),
            Prefix = txtPrefix.Text.Trim(),
            OutputDir = txtOutputDir.Text.Trim(),
            TargetRpms = targets
        };
    }

    internal void ImportProfileState(RpmProfile profile)
    {
        txtCarName.Text = profile.CarName;
        txtPrefix.Text = profile.Prefix;
        txtOutputDir.Text = profile.OutputDir ?? "recordings";
        lstRpm.Items.Clear();
        foreach (int rpm in profile.TargetRpms) lstRpm.Items.Add(rpm);
    }

    // ── Output ──
    private void btnBrowse_Click(object sender, RoutedEventArgs e)
    {
        var dlg = new System.Windows.Forms.FolderBrowserDialog { Description = "Select output folder" };
        if (dlg.ShowDialog() == System.Windows.Forms.DialogResult.OK) txtOutputDir.Text = dlg.SelectedPath;
    }

    private void btnOpenFolder_Click(object sender, RoutedEventArgs e)
    {
        string dir = txtOutputDir.Text.Trim();
        if (!Directory.Exists(dir)) Directory.CreateDirectory(dir);
        Process.Start("explorer.exe", dir);
    }

    // ── RPM ──
    private void btnAdd_Click(object sender, RoutedEventArgs e)
    {
        if (int.TryParse(txtRpm.Text, out int rpm) && !lstRpm.Items.Contains(rpm)) lstRpm.Items.Add(rpm);
    }
    private void btnRemove_Click(object sender, RoutedEventArgs e)
    {
        if (lstRpm.SelectedItem != null) lstRpm.Items.Remove(lstRpm.SelectedItem);
    }
    private void btnPreset_Click(object sender, RoutedEventArgs e)
    {
        if (sender is FrameworkElement btn && int.TryParse(btn.Tag?.ToString(), out int rpm))
            if (!lstRpm.Items.Contains(rpm)) lstRpm.Items.Add(rpm);
    }
    private void btnSort_Click(object sender, RoutedEventArgs e)
    {
        var items = new List<int>();
        foreach (var item in lstRpm.Items) items.Add(Convert.ToInt32(item));
        items.Sort(); lstRpm.Items.Clear();
        foreach (var rpm in items) lstRpm.Items.Add(rpm);
    }
    private void btnClear_Click(object sender, RoutedEventArgs e) => lstRpm.Items.Clear();
    private void btnAuto_Click(object sender, RoutedEventArgs e)
    {
        double? maxRpm = _backend?.ReadMaxRpm();
        if (!maxRpm.HasValue || maxRpm.Value <= 0) 
        { 
            _snackbarService.Show("No Redline Data", "Redline not detected. Connect first.", ControlAppearance.Info, new SymbolIcon(SymbolRegular.Info24), TimeSpan.FromSeconds(5));
            return; 
        }
        
        lstRpm.Items.Clear();
        int limit = (int)maxRpm.Value - 200;
        for (int r = 1000; r <= limit; r += 500)
        {
            if (!lstRpm.Items.Contains(r)) lstRpm.Items.Add(r);
        }
        if (!lstRpm.Items.Contains(limit)) lstRpm.Items.Add(limit);
        
        Log($"Auto-generated RPM targets: 1000 to {limit} (500 step)");
    }
    private void btnEdit_Click(object sender, RoutedEventArgs e)
    {
        if (lstRpm.SelectedItem is not int) return;
        if (int.TryParse(txtRpm.Text, out int newVal)) lstRpm.Items[lstRpm.SelectedIndex] = newVal;
    }
    private void lstRpm_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (lstRpm.SelectedItem is int selected) txtRpm.Text = selected.ToString();
    }
    private void btnNudgeUp_Click(object sender, RoutedEventArgs e)
    {
        if (lstRpm.SelectedItem is int s) lstRpm.Items[lstRpm.SelectedIndex] = s + 250;
    }
    private void btnNudgeDown_Click(object sender, RoutedEventArgs e)
    {
        if (lstRpm.SelectedItem is int s) { int v = s - 250; if (v > 0) lstRpm.Items[lstRpm.SelectedIndex] = v; }
    }

    // ── Start / Stop ──
    private void btnStart_Click(object sender, RoutedEventArgs e)
    {
        if (lstRpm.Items.Count == 0) 
        { 
            _snackbarService.Show("No targets", "Add at least one target RPM.", ControlAppearance.Caution, new SymbolIcon(SymbolRegular.Warning24), TimeSpan.FromSeconds(5));
            return; 
        }
        if (_backend == null) 
        { 
            _snackbarService.Show("Not connected", "Connect to Engine Simulator first.", ControlAppearance.Caution, new SymbolIcon(SymbolRegular.Warning24), TimeSpan.FromSeconds(5));
            return; 
        }

        var opt = OptionsPage.Instance;
        string outputDir = txtOutputDir.Text.Trim();
        Directory.CreateDirectory(outputDir);

        var targets = new List<int>();
        foreach (var item in lstRpm.Items) targets.Add(Convert.ToInt32(item));
        targets.Sort();

        var cfg = BuildRecorderConfig(targets);

        btnStart.IsEnabled = false; btnStop.IsEnabled = true;
        pbar.BeginAnimation(ProgressBar.ValueProperty, null); pbar.Value = 0;
        _cts = new CancellationTokenSource();
        _workerTask = Task.Run(() => RunAsync(cfg, _cts.Token));
    }

    private void btnStop_Click(object sender, RoutedEventArgs e) => _cts?.Cancel();
    private void chkTop_Changed(object sender, RoutedEventArgs e)
    {
        if (Application.Current.MainWindow is Window w) w.Topmost = chkTop.IsChecked == true;
    }

    // ── UI Helpers ──
    private void SetConnStatus(bool connected)
    {
        bool isDark = ApplicationThemeManager.GetAppTheme() == ApplicationTheme.Dark;
        
        if (connected)
        {
            borderConnStatus.Background = new SolidColorBrush(Color.FromArgb(0x33, 0x00, 0xFF, 0x00));
            borderConnStatus.BorderBrush = new SolidColorBrush(Color.FromArgb(0x66, 0x00, 0xFF, 0x00));
            borderConnStatus.BorderThickness = new Thickness(1);
            lblConnStatus.Text = "Connected";
            lblConnStatus.Foreground = new SolidColorBrush(isDark ? Color.FromRgb(0x57, 0xF2, 0x87) : Color.FromRgb(0x1E, 0x8E, 0x3E));
            btnConnect.Content = "Disconnect";
        }
        else
        {
            borderConnStatus.Background = new SolidColorBrush(Color.FromArgb(0x33, 0xFF, 0x00, 0x00));
            borderConnStatus.BorderBrush = new SolidColorBrush(Color.FromArgb(0x66, 0xFF, 0x00, 0x00));
            borderConnStatus.BorderThickness = new Thickness(1);
            lblConnStatus.Text = "Disconnected";
            lblConnStatus.Foreground = new SolidColorBrush(isDark ? Color.FromRgb(0xFF, 0x80, 0x80) : Color.FromRgb(0xD9, 0x30, 0x25));
            btnConnect.Content = "Connect";
        }
        btnConnect.IsEnabled = true;
    }

    private void SetStatus(string t) => Dispatcher.BeginInvoke(() => lblStatus.Text = t);
    private void SetRpm(string t) => Dispatcher.BeginInvoke(() => lblRpm.Text = t);
    private void SetEta(string t) => Dispatcher.BeginInvoke(() => lblEta.Text = t);

    private void StartRecProgress(double durationSec)
    {
        Dispatcher.BeginInvoke(() =>
        {
            _progressDurationSec = durationSec;
            pbar.BeginAnimation(ProgressBar.ValueProperty, null); pbar.Value = 0;
            _progressSw = Stopwatch.StartNew();
            _progressTimer?.Stop();
            _progressTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(60) };
            _progressTimer.Tick += ProgressTimer_Tick;
            _progressTimer.Start();
        });
    }
    private void StopRecProgress()
    {
        Dispatcher.BeginInvoke(() => { _progressTimer?.Stop(); _progressTimer = null; pbar.BeginAnimation(ProgressBar.ValueProperty, null); pbar.Value = 0; });
    }
    private void FinishRecProgress()
    {
        Dispatcher.BeginInvoke(() =>
        {
            _progressTimer?.Stop(); _progressTimer = null;
            pbar.BeginAnimation(ProgressBar.ValueProperty, null); pbar.Value = 1;
            pbar.Foreground = (Brush)FindResource("DoneBrush");
        });
    }
    private void ProgressTimer_Tick(object? sender, EventArgs e)
    {
        if (_progressSw == null || _progressDurationSec <= 0) return;
        double fraction = Math.Min(_progressSw.Elapsed.TotalSeconds / _progressDurationSec, 1.0);
        pbar.BeginAnimation(ProgressBar.ValueProperty, new DoubleAnimation(fraction, new Duration(TimeSpan.FromMilliseconds(60))));
    }
    private void ResetControls() => Dispatcher.BeginInvoke(() => { btnStart.IsEnabled = true; btnStop.IsEnabled = false; lblStatus.Text = "Done."; });

    private string GetPrefix() { string p = txtPrefix.Text.Trim(); return string.IsNullOrEmpty(p) ? "" : p.EndsWith("_") ? p : p + "_"; }
    private string GetCarType()
    {
        var opt = OptionsPage.Instance;
        return (opt?.cmbCarType?.SelectedItem as ComboBoxItem)?.Content?.ToString() ?? "Sedan";
    }

    // ══════════════════════════════════════════════════════════
    //  WORKFLOW — full port from old MainWindow
    // ══════════════════════════════════════════════════════════

    private RecorderConfig BuildRecorderConfig(List<int> targets)
    {
        var opt = OptionsPage.Instance;
        return new RecorderConfig
        {
            OutputDir = txtOutputDir.Text.Trim(),
            ProcessId = 0, // Injected via Backend if needed
            TargetRpms = targets,
            CarName = txtCarName.Text.Trim(),
            Prefix = GetPrefix(),
            SampleRate = opt?.cmbSampleRate?.SelectedIndex == 1 ? 48000 : 44100,
            Channels = opt?.cmbChannels?.SelectedIndex == 1 ? 1 : 2,
            InteriorMode = opt?.rbInterior?.IsChecked == true,
            CarType = GetCarType(),
            RecordLimiter = opt?.chkRecordLimiter?.IsChecked == true,
            GeneratePowerLut = opt?.chkGeneratePowerLut?.IsChecked == true,
            Interior = new InteriorSettings
            {
                CutoffHz = (float)(opt?.slCutoff?.Value ?? 2000),
                RumbleHz = (float)(opt?.slRumbleHz?.Value ?? 80),
                RumbleDb = (float)(opt?.slRumbleDb?.Value ?? 6),
                Res1Hz = (float)(opt?.slRes1Hz?.Value ?? 180),
                Res1Db = (float)(opt?.slRes1Db?.Value ?? 5),
                Res2Hz = (float)(opt?.slRes2Hz?.Value ?? 350),
                Res2Db = (float)(opt?.slRes2Db?.Value ?? 4),
                StereoWidth = (float)((opt?.slWidth?.Value ?? 30) / 100.0),
                ReverbMix = (float)((opt?.slReverbMix?.Value ?? 7) / 100.0),
                ReverbMs = (float)(opt?.slReverbMs?.Value ?? 30),
                CompRatio = (float)(opt?.slCompRatio?.Value ?? 3),
                CompThreshDb = (float)(opt?.slCompThresh?.Value ?? -12),
            },
            ExteriorPreset = opt?.GetExteriorPreset() ?? ExteriorPreset.Raw,
            Exterior = new ExteriorSettings
            {
                LpHz        = (float)(opt?.slExtLpHz?.Value        ?? 8000),
                LpQ         = (float)(opt?.slExtLpQ?.Value         ?? 0.7),
                HsHz        = (float)(opt?.slExtHsHz?.Value        ?? 5000),
                HsGainDb    = (float)(opt?.slExtHsGainDb?.Value    ?? -3),
                MidHz       = (float)(opt?.slExtMidHz?.Value       ?? 150),
                MidGainDb   = (float)(opt?.slExtMidGainDb?.Value   ?? 3),
                SatDrive    = (float)(opt?.slExtSatDrive?.Value    ?? 2.5),
                EnableNoise = opt?.chkExtNoise?.IsChecked           ?? true,
                ReverbMs    = (float)(opt?.slExtReverbMs?.Value    ?? 25),
                ReverbMix   = (float)((opt?.slExtReverbMix?.Value  ?? 10) / 100.0),
                CompRatio   = (float)(opt?.slExtCompRatio?.Value   ?? 3),
                CompThreshDb= (float)(opt?.slExtCompThresh?.Value  ?? -12),
            },
        };
    }

    private void RunAsync(RecorderConfig cfg, CancellationToken ct)
    {
        KeyboardBackend? backend = null;
        bool ownsBackend = false;
        try
        {
            if (_backend != null) { backend = _backend; Log($"Mode: {backend.Name}"); }
            else
            {
                backend = new KeyboardBackend(); ownsBackend = true;
                Log($"Mode: {backend.Name}");
                if (!backend.Initialize(cfg, Log, ct)) { Log("Backend initialization failed."); return; }
            }

            double? maxRpm = backend.ReadMaxRpm();
            if (maxRpm.HasValue) { Log($"Engine redline: {maxRpm.Value:F0} RPM (max target: {maxRpm.Value - 250:F0})"); Dispatcher.BeginInvoke(() => lblRedline.Text = $"(redline: {maxRpm.Value:F0})"); }

            IntPtr hwnd = backend.Hwnd;
            MainWindow.EngineSimHwnd = hwnd;

            Log("Focusing Engine Sim window...");
            KeyboardSim.FocusWindow(hwnd);

            Dispatcher.BeginInvoke(() => MainWindow.StartFocusMonitor(this));

            // Step 1: Ignition
            Log("=== STEP 1: Ignition ===");
            KeyboardSim.KeyPress(hwnd, KeyboardSim.VK_A, 120);
            Log("Pressed A (Ignition ON)");
            ct.WaitHandle.WaitOne(500);

            // Step 2: Starter
            Log("=== STEP 2: Starting engine ===");
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
            if (idleRpm.HasValue && idleRpm.Value > 100) Log($"Engine running - idle RPM: {idleRpm.Value:F0}");
            else Log($"Warning: RPM = {idleRpm?.ToString("F0") ?? "null"} - engine may not have started");
            if (ct.IsCancellationRequested) return;

            // Step 3: Dyno mode
            Log("=== STEP 3: Dyno mode ===");
            KeyboardSim.KeyPress(hwnd, KeyboardSim.VK_D, 120);
            KeyboardSim.KeyDown(hwnd, KeyboardSim.VK_S);
            ct.WaitHandle.WaitOne(500);
            KeyboardSim.KeyUp(hwnd, KeyboardSim.VK_S);
            ct.WaitHandle.WaitOne(300);

            int completedTargets = 0;
            var overallSw = Stopwatch.StartNew();
            var torqueData = new List<(int rpm, double torqueNm)>();

            string prefix = cfg.Prefix ?? "";
            string carName = cfg.CarName ?? "";
            for (int i = 0; i < cfg.TargetRpms.Count; i++)
            {
                if (ct.IsCancellationRequested) break;
                int target = cfg.TargetRpms[i];
                Log($"=== TARGET {i + 1}/{cfg.TargetRpms.Count}: {target} RPM ===");
                SetStatus($"Revving to {target} RPM..."); SetEta("");

                backend.SetThrottle(1.0);
                WaitForRpm(backend, cfg, target, ct);
                if (ct.IsCancellationRequested) break;

                double? rpmAtHold = backend.ReadRpm();
                KeyboardSim.KeyPress(hwnd, KeyboardSim.VK_H, 120);
                ct.WaitHandle.WaitOne(500);
                double? rpmAfterHold = backend.ReadRpm();
                if (rpmAtHold.HasValue && rpmAfterHold.HasValue)
                    Log($"Hold verified: before={rpmAtHold.Value:F0}, after={rpmAfterHold.Value:F0}, delta={Math.Abs(rpmAfterHold.Value - rpmAtHold.Value):F0} RPM");

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
                    else Log($"Warning: no torque reading at {target} RPM");
                }

                // Interior naming must always be int_<car>_* (do not carry exterior prefix).
                string baseName = string.IsNullOrEmpty(carName)
                    ? ""
                    : (cfg.InteriorMode ? $"int_{carName}_" : $"{prefix}{carName}_");
                string modePrefix = "";

                string loadFile = $"{baseName}{modePrefix}on_{target}.wav";
                string loadPath = Path.Combine(cfg.OutputDir, loadFile);
                SetStatus($"Recording {target} RPM (load)...");
                Log($"Recording LOAD for {cfg.RecordSeconds}s -> {loadPath}");
                RecordAudio(backend, loadPath, cfg, ct);
                if (ct.IsCancellationRequested) break;
                Log($"Saved: {loadPath}");

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

                double elapsed = overallSw.Elapsed.TotalSeconds;
                double avgPerTarget = elapsed / completedTargets;
                double remaining = avgPerTarget * (cfg.TargetRpms.Count - completedTargets);
                if (completedTargets < cfg.TargetRpms.Count) SetEta($"ETA: {remaining:F0}s / {elapsed + remaining:F0}s");

                if (i < cfg.TargetRpms.Count - 1)
                {
                    backend.SetThrottle(1.0);
                    ct.WaitHandle.WaitOne(16);
                    KeyboardSim.KeyPress(hwnd, KeyboardSim.VK_H, 120);
                    ct.WaitHandle.WaitOne(300);
                }
                else KeyboardSim.KeyPress(hwnd, KeyboardSim.VK_H, 120);
            }

            // Step 5: Engine limiter (optional)
            if (cfg.RecordLimiter && !ct.IsCancellationRequested)
            {
                double? det = backend.ReadMaxRpm();
                int limiterRpm = det.HasValue ? (int)det.Value : (cfg.TargetRpms.Count > 0 ? cfg.TargetRpms[^1] + 250 : 7000);
                Log($"=== RECORDING ENGINE LIMITER at {limiterRpm} RPM ===");
                SetStatus($"Revving to redline ({limiterRpm} RPM)..."); SetEta("");
                backend.SetThrottle(1.0);
                WaitForRpm(backend, cfg, limiterRpm, ct);
                Log("Waiting 0.2s for rev limiter to engage...");
                ct.WaitHandle.WaitOne(200);
                if (!ct.IsCancellationRequested)
                {
                    string limiterBase = string.IsNullOrEmpty(carName)
                        ? ""
                        : (cfg.InteriorMode ? $"int_{carName}_" : $"{prefix}{carName}_");
                    string f = $"{limiterBase}engine_limiter.wav";
                    string p = Path.Combine(cfg.OutputDir, f);
                    Log($"Recording LIMITER for {cfg.RecordSeconds}s -> {p}");
                    RecordAudio(backend, p, cfg, ct);
                    Log($"Saved: {p}");
                }
                backend.SetThrottle(0);
            }

            // Step 6: power.lut (optional)
            if (cfg.GeneratePowerLut && torqueData.Count > 0 && !ct.IsCancellationRequested)
            {
                string lutPath = Path.Combine(cfg.OutputDir, "power.lut");
                Log($"=== GENERATING power.lut ({torqueData.Count} points) ===");
                try
                {
                    using var writer = new StreamWriter(lutPath);
                    foreach (var (rpm, torqueNm) in torqueData) writer.WriteLine($"{rpm}|{torqueNm:F2}");
                    Log($"Saved: {lutPath}");
                }
                catch (Exception ex) { Log($"Error writing power.lut: {ex.Message}"); }
            }

            // Shutdown
            Log("=== SHUTTING DOWN ==="); SetEta("");
            backend.SetThrottle(0);
            ct.WaitHandle.WaitOne(200);
            KeyboardSim.KeyPress(hwnd, KeyboardSim.VK_D, 120);
            ct.WaitHandle.WaitOne(200);
            KeyboardSim.KeyPress(hwnd, KeyboardSim.VK_A, 120);
            Log("Engine stopped. All recordings complete!");
            FinishRecProgress();
            try { Process.Start("explorer.exe", cfg.OutputDir); } catch { Log("Could not open output folder."); }
        }
        catch (OperationCanceledException) { Log("Stopped by user."); }
        catch (Exception ex)
        {
            Log($"ERROR: {ex.Message}");
            Dispatcher.BeginInvoke(() => _snackbarService.Show("Error", ex.Message, ControlAppearance.Danger, new SymbolIcon(SymbolRegular.ErrorCircle24), TimeSpan.FromSeconds(10)));
        }
        finally
        {
            if (ownsBackend) { try { backend?.SetThrottle(0); } catch { } backend?.Dispose(); }
            else try { backend?.SetThrottle(0); } catch { }
            _backend = ownsBackend ? null : backend;
            Dispatcher.BeginInvoke(() => MainWindow.StopFocusMonitor());
            MainWindow.EngineSimHwnd = IntPtr.Zero;
            StopRecProgress(); SetEta(""); ResetControls();
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
                if (rpm.Value >= targetRpm - cfg.RpmTolerance) { Log($"RPM {rpm.Value:F0} reached target {targetRpm}"); return; }
            }
            if (sw.Elapsed.TotalSeconds > 30) { Log($"Warning: timeout waiting for {targetRpm} RPM"); return; }
            ct.WaitHandle.WaitOne(20);
        }
    }

    private void RecordAudio(KeyboardBackend backend, string outputPath, RecorderConfig cfg, CancellationToken ct)
    {
        using var capture = new WasapiLoopbackCapture();
        capture.WaveFormat = WaveFormat.CreateIeeeFloatWaveFormat(cfg.SampleRate, cfg.Channels);
        var done = new ManualResetEventSlim(false);

        InteriorProcessor? interior = null;
        ExteriorProcessor? exterior = null;
        var recorded = new List<float>(cfg.SampleRate * cfg.Channels * Math.Max(1, cfg.RecordSeconds));

        if (cfg.InteriorMode)
        {
            if (cfg.CarType == "Custom")
            {
                var i = cfg.Interior;
                interior = new InteriorProcessor(cfg.SampleRate, cfg.Channels, i.CutoffHz, i.StereoWidth, i.RumbleHz, i.RumbleDb, i.Res1Hz, i.Res1Db, i.Res2Hz, i.Res2Db, i.ReverbMs, i.ReverbMix, i.CompRatio, i.CompThreshDb);
            }
            else
            {
                var (cutoff, width) = InteriorProcessor.GetPreset(cfg.CarType);
                interior = new InteriorProcessor(cfg.SampleRate, cfg.Channels, cutoff, width);
            }
        }
        else if (cfg.ExteriorPreset != ExteriorPreset.Raw)
        {
            exterior = cfg.ExteriorPreset == ExteriorPreset.Custom
                ? new ExteriorProcessor(cfg.SampleRate, cfg.Channels, cfg.Exterior)
                : new ExteriorProcessor(cfg.SampleRate, cfg.Channels, cfg.ExteriorPreset);
        }

        StartRecProgress(cfg.RecordSeconds);
        capture.DataAvailable += (s, e) =>
        {
            if (e.BytesRecorded == 0) return;

            int samples = e.BytesRecorded / 4;
            var floats = new float[samples];
            Buffer.BlockCopy(e.Buffer, 0, floats, 0, e.BytesRecorded);

            interior?.Process(floats, samples);
            exterior?.Process(floats, samples);

            // Buffer entire recording so we can post-process loop points.
            for (int i = 0; i < samples; i++) recorded.Add(floats[i]);
        };
        capture.RecordingStopped += (s, e) => done.Set();
        capture.StartRecording();

        DateTime start = DateTime.UtcNow;
        while (!ct.IsCancellationRequested)
        {
            double elapsed = (DateTime.UtcNow - start).TotalSeconds;
            if (elapsed >= cfg.RecordSeconds) break;
            double? rpm = backend.ReadRpm();
            if (rpm.HasValue) { SetRpm($"RPM: {rpm.Value:F0}"); SetStatus($"Recording - {rpm.Value:F0} RPM - {elapsed:F1}s / {cfg.RecordSeconds}s"); }
            ct.WaitHandle.WaitOne(16);
        }
        capture.StopRecording();
        done.Wait(TimeSpan.FromSeconds(5));

        // Post-process to prevent loop clicks (DC offset removal -> zero-cross trim -> 10ms crossfade)
        if (recorded.Count > 0 && cfg.Channels > 0 && cfg.SampleRate > 0)
        {
            float[] samples = FixLoopClick(recorded.ToArray(), cfg.SampleRate, cfg.Channels);
            using var writer = new WaveFileWriter(outputPath, capture.WaveFormat);
            writer.WriteSamples(samples, 0, samples.Length);
        }
    }

    private static float[] FixLoopClick(float[] samples, int sampleRate, int channels)
    {
        if (channels <= 0) return samples;
        int frames = samples.Length / channels;
        if (frames <= 4) return samples;

        RemoveDcOffset(samples, channels);

        // Snap to nearest zero-crossing at start and end (use channel 0 as reference)
        int searchFrames = Math.Max(8, (int)(sampleRate * 0.02)); // 20ms window
        int startFrame = FindNearestZeroCrossingFrame(samples, channels, 0, searchFrames);
        int endFrame = FindNearestZeroCrossingFrame(samples, channels, frames - 1, searchFrames);
        if (endFrame <= startFrame + 4) { startFrame = 0; endFrame = frames - 1; }

        if (startFrame != 0 || endFrame != frames - 1)
        {
            int newFrames = endFrame - startFrame + 1;
            var trimmed = new float[newFrames * channels];
            Buffer.BlockCopy(samples, startFrame * channels * sizeof(float), trimmed, 0, trimmed.Length * sizeof(float));
            samples = trimmed;
        }

        ApplyLoopCrossfadeInPlace(samples, sampleRate, channels, 0.010);
        NormalizeIfNeededInPlace(samples);
        return samples;
    }

    private static void RemoveDcOffset(float[] samples, int channels)
    {
        double[] sum = new double[channels];
        int frames = samples.Length / channels;
        for (int f = 0; f < frames; f++)
        {
            int idx = f * channels;
            for (int c = 0; c < channels; c++) sum[c] += samples[idx + c];
        }

        for (int c = 0; c < channels; c++)
        {
            float mean = (float)(sum[c] / Math.Max(1, frames));
            for (int f = 0; f < frames; f++)
                samples[f * channels + c] -= mean;
        }
    }

    private static int FindNearestZeroCrossingFrame(float[] samples, int channels, int frameIndex, int searchRadiusFrames)
    {
        int frames = samples.Length / channels;
        frameIndex = Math.Clamp(frameIndex, 0, frames - 1);
        int start = Math.Max(1, frameIndex - searchRadiusFrames);
        int end = Math.Min(frames - 2, frameIndex + searchRadiusFrames);

        int best = frameIndex;
        int bestDist = int.MaxValue;

        for (int f = start; f <= end; f++)
        {
            float a = samples[(f - 1) * channels];
            float b = samples[f * channels];

            // sign change or exact zero indicates a crossing
            if ((a <= 0f && b >= 0f) || (a >= 0f && b <= 0f))
            {
                int dist = Math.Abs(f - frameIndex);
                if (dist < bestDist)
                {
                    bestDist = dist;
                    best = f;
                    if (bestDist == 0) break;
                }
            }
        }

        return best;
    }

    private static void ApplyLoopCrossfadeInPlace(float[] samples, int sampleRate, int channels, double seconds)
    {
        int frames = samples.Length / channels;
        int fadeFrames = (int)Math.Round(sampleRate * seconds);
        fadeFrames = Math.Clamp(fadeFrames, 1, frames / 2);

        int tailStart = frames - fadeFrames;
        if (tailStart <= 0) return;

        for (int i = 0; i < fadeFrames; i++)
        {
            float t = fadeFrames == 1 ? 1f : (float)i / (fadeFrames - 1);
            int headFrame = i;
            int tailFrame = tailStart + i;
            int headIdx = headFrame * channels;
            int tailIdx = tailFrame * channels;

            for (int c = 0; c < channels; c++)
            {
                float head = samples[headIdx + c];
                float tail = samples[tailIdx + c];
                samples[headIdx + c] = head * t + tail * (1f - t);
                samples[tailIdx + c] = tail * t + head * (1f - t);
            }
        }
    }

    private static void NormalizeIfNeededInPlace(float[] samples)
    {
        float maxAbs = 0f;
        for (int i = 0; i < samples.Length; i++)
        {
            float a = Math.Abs(samples[i]);
            if (a > maxAbs) maxAbs = a;
        }

        if (maxAbs <= 1.0f || maxAbs <= 0f) return;
        float scale = 1.0f / maxAbs;
        for (int i = 0; i < samples.Length; i++) samples[i] *= scale;
    }

}
