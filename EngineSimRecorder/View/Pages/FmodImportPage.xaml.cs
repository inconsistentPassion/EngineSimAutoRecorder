using System;
using System.IO;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using EngineSimRecorder.Services;

namespace EngineSimRecorder.View.Pages;

public partial class FmodImportPage : Page
{
    public static FmodImportPage? Instance { get; private set; }

    private FmodTcpService? _fmod;
    private CancellationTokenSource? _cts;

    public FmodImportPage()
    {
        InitializeComponent();
        Instance = this;
        Loaded += OnLoaded;
    }

    private bool _wired;
    private void OnLoaded(object sender, RoutedEventArgs e)
    {
        if (_wired) return;
        _wired = true;

        btnConnect.Click                  += BtnConnect_Click;
        btnDisconnect.Click               += BtnDisconnect_Click;
        btnBrowseFmod.Click               += BtnBrowseFmod_Click;
        btnBrowseRecordings.Click         += BtnBrowseRecordings_Click;
        btnBrowseRecordingsInt.Click      += BtnBrowseRecordingsInt_Click;
        btnBrowseRecordingsLimiter.Click  += BtnBrowseRecordingsLimiter_Click;
        btnBrowseAc.Click                 += BtnBrowseAc_Click;
        btnClearLog.Click                 += (_, _) => { txtLog.Clear(); HideResult(); ShowViolations(null); };
        btnCopyLog.Click                  += (_, _) => { Clipboard.SetText(txtLog.Text); };
        btnRun.Click                      += BtnRun_Click;
        btnCopyArtifactsOnly.Click        += BtnCopyArtifactsOnly_Click;

        // Pre-fill recordings dir from RecorderPage if available
        if (RecorderPage.Instance != null)
        {
            string recDir = RecorderPage.Instance.txtOutputDir.Text.Trim();
            if (!string.IsNullOrEmpty(recDir))
                txtRecordingsDir.Text = recDir;

            string carName = RecorderPage.Instance.txtCarName.Text.Trim();
            if (!string.IsNullOrEmpty(carName))
                txtCarName.Text = carName;
        }
    }

    // ── Connection ────────────────────────────────────────────────────────────

    private void BtnConnect_Click(object sender, RoutedEventArgs e)
    {
        if (_fmod?.IsConnected == true) return;

        SetStage("Connecting to FMOD Studio...");
        btnConnect.IsEnabled = false;

        Task.Run(() =>
        {
            try
            {
                _fmod?.Dispose();
                _fmod = new FmodTcpService();
                _fmod.Connect();
                Dispatcher.BeginInvoke(OnConnected);
            }
            catch (Exception ex)
            {
                Dispatcher.BeginInvoke(() => OnConnectFailed(ex.Message));
            }
        });
    }

    private void OnConnected()
    {
        SetConnStatus(connected: true);
        btnConnect.IsEnabled    = false;
        btnDisconnect.IsEnabled = true;
        btnRun.IsEnabled        = true;
        btnCopyArtifactsOnly.IsEnabled = true;
        SetStage("Connected to FMOD Studio on 127.0.0.1:3663");
        AppendLog("Connected to FMOD Studio.");
    }

    private void OnConnectFailed(string msg)
    {
        SetConnStatus(connected: false);
        btnConnect.IsEnabled = true;
        SetStage(string.Empty);
        AppendLog($"Connection failed: {msg}");
        ShowResult(success: false, $"Connection failed: {msg}");
    }

    private void BtnDisconnect_Click(object sender, RoutedEventArgs e)
    {
        _fmod?.Dispose();
        _fmod = null;
        SetConnStatus(connected: false);
        btnConnect.IsEnabled    = true;
        btnDisconnect.IsEnabled = false;
        btnRun.IsEnabled        = false;
        btnCopyArtifactsOnly.IsEnabled = false;
        SetStage(string.Empty);
        AppendLog("Disconnected.");
    }

    // ── Browse helpers ────────────────────────────────────────────────────────

    private void BtnBrowseFmod_Click(object sender, RoutedEventArgs e)
        => BrowseFolder("Select FMOD Studio project folder", path => txtFmodProject.Text = path);

    private void BtnBrowseRecordings_Click(object sender, RoutedEventArgs e)
        => BrowseFolder("Select EXT recordings folder (.wav exterior files)", path => txtRecordingsDir.Text = path);

    private void BtnBrowseRecordingsInt_Click(object sender, RoutedEventArgs e)
        => BrowseFolder("Select INT recordings folder (.wav interior files)", path => txtRecordingsDirInt.Text = path);

    private void BtnBrowseRecordingsLimiter_Click(object sender, RoutedEventArgs e)
        => BrowseFolder("Select Limiter recordings folder (engine_limiter.wav)", path => txtRecordingsDirLimiter.Text = path);

    private void BtnBrowseAc_Click(object sender, RoutedEventArgs e)
        => BrowseFolder("Select Assetto Corsa content\\ folder", path => txtAcContent.Text = path);

    private static void BrowseFolder(string description, Action<string> onSelected)
    {
        var dlg = new System.Windows.Forms.FolderBrowserDialog
        {
            Description = description,
            UseDescriptionForTitle = true
        };
        if (dlg.ShowDialog() == System.Windows.Forms.DialogResult.OK)
            onSelected(dlg.SelectedPath);
    }


    // ── Run Import ────────────────────────────────────────────────────────────

    private void BtnRun_Click(object sender, RoutedEventArgs e)
    {
        if (!ValidateInputs(requireJs: true)) return;
        RunImport(sendJs: true);
    }

    private void BtnCopyArtifactsOnly_Click(object sender, RoutedEventArgs e)
    {
        if (!ValidateInputs(requireJs: false)) return;
        RunImport(sendJs: false);
    }

    private bool ValidateInputs(bool requireJs)
    {
        if (string.IsNullOrWhiteSpace(txtCarName.Text))
        {
            MessageBox.Show("Enter a car name.", "Missing Input", MessageBoxButton.OK, MessageBoxImage.Warning);
            return false;
        }
        if (requireJs && string.IsNullOrWhiteSpace(txtRecordingsDir.Text))
        {
            MessageBox.Show("Set the EXT Recordings Dir.", "Missing Input", MessageBoxButton.OK, MessageBoxImage.Warning);
            return false;
        }
        if (chkCopyArtifacts.IsChecked == true)
        {
            if (string.IsNullOrWhiteSpace(txtFmodProject.Text))
            {
                MessageBox.Show("Set the FMOD project path (needed for GUIDs.txt).", "Missing Input", MessageBoxButton.OK, MessageBoxImage.Warning);
                return false;
            }
            if (string.IsNullOrWhiteSpace(txtAcContent.Text))
            {
                MessageBox.Show("Set the Assetto Corsa content path.", "Missing Input", MessageBoxButton.OK, MessageBoxImage.Warning);
                return false;
            }
        }
        return true;
    }

    private void RunImport(bool sendJs)
    {
        _cts?.Cancel();
        _cts = new CancellationTokenSource();
        var ct = _cts.Token;

        SetProgress(0);
        HideResult();
        txtLog.Clear();
        btnRun.IsEnabled = false;
        btnCopyArtifactsOnly.IsEnabled = false;

        string carName              = txtCarName.Text.Trim();
        string fmodProject          = txtFmodProject.Text.Trim();
        string acContent            = txtAcContent.Text.Trim();
        string recordingsDir        = txtRecordingsDir.Text.Trim();
        string recordingsDirInt     = txtRecordingsDirInt.Text.Trim();
        string recordingsDirLimiter = txtRecordingsDirLimiter.Text.Trim();
        bool   copyArt              = chkCopyArtifacts.IsChecked == true;
        int    genModeIndex         = cmbGenerationMode.SelectedIndex;

        Task.Run(async () =>
        {
            var result = new FmodImportResult();
            try
            {
                // ── Stage 1: Generate & Validate JS ──────────────────────────────────
                string jsCode = string.Empty;
                if (sendJs)
                {
                    Dispatch(() => { SetStage("Generating JavaScript..."); SetProgress(10); });
                    AppendLogThreadSafe("Generating JS script for Assetto Corsa FMOD setup...");

                    var generator = new FmodScriptGenerator 
                    { 
                        CarName              = carName,
                        FmodProjectPath      = fmodProject,
                        RecordingsDirExt     = recordingsDir,
                        RecordingsDirInt     = recordingsDirInt,
                        RecordingsDirLimiter = recordingsDirLimiter,
                        Mode = genModeIndex == 0 ? FmodGenerationMode.UseExistingTemplate : FmodGenerationMode.FromScratch
                    };
                    jsCode = generator.GenerateScript();
                    Dispatch(() => txtJsCode.Text = jsCode);

                    int jsLines = jsCode.Split('\n').Length;
                    AppendLogThreadSafe($"Script generated: {jsLines} lines, mode={generator.Mode}.");

                    // Log which WAV files were found
                    int extCount = jsCode.Split(new[]{"extWavPaths"}, StringSplitOptions.None).Length - 1;
                    if (!string.IsNullOrEmpty(recordingsDir))
                        AppendLogThreadSafe($"EXT dir: {recordingsDir}");
                    if (!string.IsNullOrEmpty(recordingsDirInt))
                        AppendLogThreadSafe($"INT dir: {recordingsDirInt}");
                    if (!string.IsNullOrEmpty(recordingsDirLimiter))
                        AppendLogThreadSafe($"Limiter dir: {recordingsDirLimiter}");
                    if (!string.IsNullOrEmpty(fmodProject))
                        AppendLogThreadSafe($"FMOD project: {fmodProject}");

                    AppendLogThreadSafe("Validating generated JavaScript for ES3 compatibility...");

                    bool valid = FmodEs3Validator.IsValid(jsCode, out string[] violations);
                    result.Violations = violations;

                    if (!valid)
                    {
                        AppendLogThreadSafe($"WARNING: {violations.Length} ES6 pattern(s) detected:");
                        foreach (string v in violations)
                            AppendLogThreadSafe($"  - {v}");
                        AppendLogThreadSafe("Proceeding anyway — FMOD may reject unsupported syntax.");
                        Dispatch(() => ShowViolations(violations));
                    }
                    else
                    {
                        AppendLogThreadSafe("Validation passed.");
                        Dispatch(() => ShowViolations(null));
                    }

                    ct.ThrowIfCancellationRequested();

                    // ── Stage 2: Connect (re-use or fail) ─────────────────
                    Dispatch(() => { SetStage("Connecting..."); SetProgress(20); });

                    if (_fmod == null || !_fmod.IsConnected)
                    {
                        AppendLogThreadSafe("Connecting to FMOD Studio...");
                        _fmod?.Dispose();
                        _fmod = new FmodTcpService();
                        _fmod.Connect();
                        Dispatch(OnConnected);
                    }

                    ct.ThrowIfCancellationRequested();

                    // ── Stage 3: Send JS ──────────────────────────────────
                    Dispatch(() => { SetStage("Sending JavaScript to FMOD..."); SetProgress(40); });
                    AppendLogThreadSafe("Sending JS to FMOD Studio...");

                    string response = await _fmod.ExecuteAsync(jsCode, ct);
                    result.FmodResponse = response;

                    // Always log the full FMOD response line by line
                    AppendLogThreadSafe("── FMOD Response ──────────────────────");
                    foreach (string line in response.Split('\n'))
                    {
                        string l = line.Trim();
                        if (!string.IsNullOrEmpty(l))
                            AppendLogThreadSafe($"  {l}");
                    }
                    AppendLogThreadSafe("───────────────────────────────────────");

                    if (_fmod.LastResponseHadError)
                    {
                        result.Success = false;
                        result.ErrorMessage = response;
                        Dispatch(() =>
                        {
                            SetProgress(0);
                            SetStage("FMOD reported an error.");
                            ShowResult(success: false, $"FMOD error — see log for details.");
                        });
                        return;
                    }

                    Dispatch(() => SetProgress(70));
                }

                ct.ThrowIfCancellationRequested();

                // ── Stage 4: Post-build copy ──────────────────────────────
                if (copyArt && !string.IsNullOrEmpty(fmodProject) && !string.IsNullOrEmpty(acContent))
                {
                    Dispatch(() => { SetStage("Copying build artifacts..."); SetProgress(85); });
                    AppendLogThreadSafe("Running post-build copy...");

                    var handler = new FmodPostBuildHandler
                    {
                        FmodProjectPath = fmodProject,
                        AcContentPath   = acContent
                    };

                    string[] copied = handler.CopyBuildArtifacts(carName);
                    result.CopiedFiles = copied;

                    foreach (string path in copied)
                        AppendLogThreadSafe($"Copied: {path}");
                }

                result.Success = true;
                Dispatch(() =>
                {
                    SetProgress(100);
                    SetStage("Done.");
                    if (copyArt)
                    {
                        string sfxDir = Path.Combine(acContent, "cars", carName, "sfx");
                        ShowResult(success: true, $"Import completed! Files copied to:\n{sfxDir}");
                        try { System.Diagnostics.Process.Start("explorer.exe", sfxDir); } catch { }
                    }
                    else
                    {
                        ShowResult(success: true, "Import completed successfully.");
                    }
                    AppendLog("All stages complete.");
                });
            }
            catch (OperationCanceledException)
            {
                result.ErrorMessage = "Cancelled by user.";
                AppendLogThreadSafe("Cancelled.");
                Dispatch(() => { SetStage(string.Empty); ShowResult(success: false, "Cancelled."); });
            }
            catch (Exception ex)
            {
                result.ErrorMessage = ex.Message;
                AppendLogThreadSafe($"Error: {ex.Message}");
                Dispatch(() =>
                {
                    SetStage(string.Empty);
                    ShowResult(success: false, $"Error: {ex.Message}");
                });
            }
            finally
            {
                Dispatch(() =>
                {
                    btnRun.IsEnabled = _fmod?.IsConnected == true;
                    btnCopyArtifactsOnly.IsEnabled = _fmod?.IsConnected == true || !sendJs;
                });
            }
        }, ct);
    }

    // ── UI helpers ────────────────────────────────────────────────────────────

    private void AppendLog(string msg)
    {
        string line = $"[{DateTime.Now:HH:mm:ss}] {msg}";
        txtLog.AppendText(line + "\n");
        txtLog.ScrollToEnd();
        LogPage.Instance?.AppendLog(line);
    }

    private void AppendLogThreadSafe(string msg)
        => Dispatcher.BeginInvoke(() => AppendLog(msg));

    private void Dispatch(Action a) => Dispatcher.BeginInvoke(a);

    private void SetStage(string text) => lblStage.Text = text;

    private void SetProgress(double value) => pbar.Value = value;

    private void SetConnStatus(bool connected)
    {
        if (connected)
        {
            borderConnStatus.Background = new SolidColorBrush(Color.FromArgb(0x40, 0x00, 0xFF, 0x00));
            lblConnStatus.Text = "Connected";
            lblConnStatus.Foreground = new SolidColorBrush(Color.FromRgb(0x80, 0xFF, 0x80));
            lblConnDetail.Text = "127.0.0.1:3663";
        }
        else
        {
            borderConnStatus.Background = new SolidColorBrush(Color.FromArgb(0x40, 0xFF, 0x40, 0x40));
            lblConnStatus.Text = "Disconnected";
            lblConnStatus.Foreground = new SolidColorBrush(Color.FromRgb(0xFF, 0x80, 0x80));
            lblConnDetail.Text = string.Empty;
        }
    }

    private void ShowViolations(string[]? violations)
    {
        if (violations == null || violations.Length == 0)
        {
            borderViolations.Visibility = Visibility.Collapsed;
            return;
        }
        borderViolations.Visibility = Visibility.Visible;
        var sb = new StringBuilder();
        foreach (string v in violations) sb.AppendLine($"• {v}");
        lblViolations.Text = sb.ToString().TrimEnd();
    }

    private void ShowResult(bool success, string message)
    {
        borderResult.Visibility = Visibility.Visible;
        lblResult.Text = message;
        if (success)
        {
            borderResult.Background = new SolidColorBrush(Color.FromArgb(0x40, 0x00, 0xFF, 0x00));
            borderResult.BorderBrush = new SolidColorBrush(Color.FromArgb(0x80, 0x00, 0xFF, 0x00));
            borderResult.BorderThickness = new Thickness(1);
            lblResult.Foreground = new SolidColorBrush(Color.FromRgb(0x80, 0xFF, 0x80));
        }
        else
        {
            borderResult.Background = new SolidColorBrush(Color.FromArgb(0x40, 0xFF, 0x40, 0x40));
            borderResult.BorderBrush = new SolidColorBrush(Color.FromArgb(0x80, 0xFF, 0x40, 0x40));
            borderResult.BorderThickness = new Thickness(1);
            lblResult.Foreground = new SolidColorBrush(Color.FromRgb(0xFF, 0x80, 0x80));
        }
    }

    private void HideResult() => borderResult.Visibility = Visibility.Collapsed;
}
