using System;
using System.IO;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using EngineSimRecorder.Services;
using Wpf.Ui;
using Wpf.Ui.Controls;

namespace EngineSimRecorder.View.Pages;

public partial class AutomationScriptPage : Page
{
    private readonly AutomationService _auto;
    private readonly ISnackbarService _snackbarService;
    private CancellationTokenSource? _cts;
    private bool _wired;

    public AutomationScriptPage(AutomationService auto)
    {
        _auto = auto;
        _snackbarService = App.GetService<ISnackbarService>();
        InitializeComponent();
        Loaded += OnLoaded;
    }

    private void OnLoaded(object sender, RoutedEventArgs e)
    {
        if (_wired) return;
        _wired = true;

        txtJsCode.Text = _auto.GeneratedScript;
        ShowViolations(_auto.LastViolations);

        btnGenerate.Click += BtnGenerate_Click;
        btnRun.Click += BtnRun_Click;
        btnClearLog.Click += (_, _) => { txtLog.Clear(); HideResult(); ShowViolations(null); };
        btnCopyLog.Click += (_, _) => Clipboard.SetText(txtLog.Text);
    }

    private void BtnGenerate_Click(object sender, RoutedEventArgs e)
    {
        SyncWorkspaceFromSettings();
        if (!ValidateForGenerate()) return;
        HideResult();
        SetStage("Generating script...");
        SetProgress(15);
        try
        {
            _auto.GenerateAndValidateScript();
            txtJsCode.Text = _auto.GeneratedScript;
            AppendLog($"Script generated ({_auto.GeneratedScript.Split('\n').Length} lines).");
            var (ext, intC) = _auto.GetRecordingCounts();
            AppendLog($"Found {ext} exterior and {intC} interior recordings.");
            if (_auto.LastScriptValid)
            {
                AppendLog("ES3 validation passed.");
                ShowViolations(null);
            }
            else
            {
                AppendLog($"Warning: {_auto.LastViolations.Length} ES3 compatibility issue(s).");
                ShowViolations(_auto.LastViolations);
            }
            SetProgress(100);
            SetStage("Script ready.");
        }
        catch (Exception ex)
        {
            AppendLog($"Error: {ex.Message}");
            SetStage("");
            ShowResult(false, ex.Message);
        }
    }

    private void BtnRun_Click(object sender, RoutedEventArgs e)
    {
        if (!ValidateForRun()) return;
        _cts?.Cancel();
        _cts = new CancellationTokenSource();
        var ct = _cts.Token;

        SetProgress(0);
        HideResult();
        txtLog.Clear();
        btnRun.IsEnabled = false;
        btnGenerate.IsEnabled = false;

        Task.Run(async () =>
        {
            try
            {
                Dispatch(() => { SetStage("Preparing…"); SetProgress(8); });
                string carName = _auto.CarName;
                string fmodProject = _auto.FmodProjectPath;
                string acContent = _auto.AcContentPath;
                bool copyArt = _auto.CopyArtifactsAfterBuild;

                AppendLogThreadSafe($"Starting automation for car: {carName}");
                if (!string.IsNullOrEmpty(fmodProject))
                    AppendLogThreadSafe($"FMOD project: {Path.GetFileName(fmodProject)}");

                Dispatch(() => { SetStage("Generating script…"); SetProgress(12); });
                AppendLogThreadSafe("Scanning recordings and building JavaScript…");

                _auto.GenerateAndValidateScript();
                string jsCode = _auto.GeneratedScript;
                Dispatch(() =>
                {
                    txtJsCode.Text = jsCode;
                    if (_auto.LastScriptValid)
                        ShowViolations(null);
                    else
                        ShowViolations(_auto.LastViolations);
                });

                int lines = jsCode.Split('\n').Length;
                AppendLogThreadSafe($"Generator finished ({lines} lines). Mode: {_auto.GenerationMode}");
                var (ext, intC) = _auto.GetRecordingCounts();
                AppendLogThreadSafe($"Found {ext} exterior and {intC} interior recordings.");

                if (_auto.LastScriptValid)
                    AppendLogThreadSafe("ES3 validation passed.");
                else
                    AppendLogThreadSafe($"Warning: {_auto.LastViolations.Length} ES3 issue(s) — review before shipping.");

                ct.ThrowIfCancellationRequested();

                Dispatch(() => { SetStage("Connecting to FMOD…"); SetProgress(22); });
                AppendLogThreadSafe("TCP 127.0.0.1:3663…");
                await Task.Run(() => _auto.EnsureConnected(), ct);
                Dispatch(() => { /* connection state lives on Workspace page */ });

                ct.ThrowIfCancellationRequested();

                Dispatch(() => { SetStage("Executing in FMOD…"); SetProgress(45); });
                AppendLogThreadSafe("Sending script to FMOD Studio…");
                string response = await _auto.ExecuteScriptAsync(jsCode, ct);
                _auto.LastFmodResponse = response;

                AppendLogThreadSafe("── FMOD response ──");
                foreach (string line in response.Split('\n'))
                {
                    string l = line.Trim();
                    if (!string.IsNullOrEmpty(l))
                        AppendLogThreadSafe($"  [FMOD] {l}");
                }
                AppendLogThreadSafe("──────────────────");

                if (_auto.TcpLastResponseHadError)
                {
                    _auto.LastPipelineSuccess = false;
                    _auto.LastCompletionMessage = "FMOD reported an error.";
                    _auto.LastCopiedFiles = [];
                    Dispatch(() =>
                    {
                        SetProgress(0);
                        SetStage("FMOD error.");
                        ShowResult(false, "FMOD error — see log.");
                    });
                    return;
                }

                Dispatch(() => SetProgress(72));

                if (copyArt && !string.IsNullOrEmpty(fmodProject) && !string.IsNullOrEmpty(acContent))
                {
                    Dispatch(() => { SetStage("Deploying to AC…"); SetProgress(88); });
                    AppendLogThreadSafe("Copying build artifacts…");
                    string[] copied = await Task.Run(() => _auto.DeployToAc(carName), ct);
                    _auto.LastCopiedFiles = copied;
                    foreach (string path in copied)
                        AppendLogThreadSafe($"Copied: {path}");
                    _auto.LastPipelineSuccess = true;
                    string sfxDir = Path.Combine(acContent, "cars", carName, "sfx");
                    _auto.LastCompletionMessage = $"Files copied to:\n{sfxDir}";
                    Dispatch(() =>
                    {
                        SetProgress(100);
                        SetStage("Done.");
                        ShowResult(true, _auto.LastCompletionMessage ?? "Completed.");
                    });
                }
                else
                {
                    _auto.LastCopiedFiles = [];
                    _auto.LastPipelineSuccess = true;
                    _auto.LastCompletionMessage = "FMOD automation finished (no AC copy).";
                    Dispatch(() =>
                    {
                        SetProgress(100);
                        SetStage("Done.");
                        ShowResult(true, _auto.LastCompletionMessage);
                    });
                }

                AppendLogThreadSafe("All stages complete.");
            }
            catch (OperationCanceledException)
            {
                _auto.LastPipelineSuccess = false;
                _auto.LastCompletionMessage = "Cancelled.";
                AppendLogThreadSafe("Cancelled.");
                Dispatch(() => { SetStage(""); ShowResult(false, "Cancelled."); });
            }
            catch (Exception ex)
            {
                _auto.LastPipelineSuccess = false;
                _auto.LastCompletionMessage = ex.Message;
                AppendLogThreadSafe($"Error: {ex.Message}");
                Dispatch(() => { SetStage(""); ShowResult(false, ex.Message); });
            }
            finally
            {
                Dispatch(() =>
                {
                    btnRun.IsEnabled = true;
                    btnGenerate.IsEnabled = true;
                });
            }
        }, ct);
    }

    private bool ValidateForGenerate()
    {
        SyncWorkspaceFromSettings();
        if (string.IsNullOrWhiteSpace(_auto.CarName))
        {
            _snackbarService.Show("Missing input", "Enter a car name on the Workspace page.", ControlAppearance.Caution, new SymbolIcon(SymbolRegular.Warning24), TimeSpan.FromSeconds(5));
            return false;
        }
        if (string.IsNullOrWhiteSpace(_auto.RecordingsDirExt))
        {
            _snackbarService.Show("Missing input", "Set the exterior recordings folder on the Workspace page.", ControlAppearance.Caution, new SymbolIcon(SymbolRegular.Warning24), TimeSpan.FromSeconds(5));
            return false;
        }
        return true;
    }

    private bool ValidateForRun()
    {
        if (!ValidateForGenerate()) return false;
        if (_auto.CopyArtifactsAfterBuild)
        {
            if (string.IsNullOrWhiteSpace(_auto.FmodProjectPath))
            {
                _snackbarService.Show("Missing input", "Set the FMOD project path (needed for GUIDs.txt).", ControlAppearance.Caution, new SymbolIcon(SymbolRegular.Warning24), TimeSpan.FromSeconds(5));
                return false;
            }
            if (string.IsNullOrWhiteSpace(_auto.AcContentPath))
            {
                _snackbarService.Show("Missing input", "Set the Assetto Corsa content path.", ControlAppearance.Caution, new SymbolIcon(SymbolRegular.Warning24), TimeSpan.FromSeconds(5));
                return false;
            }
        }
        return true;
    }

    /// <summary>Reload paths from disk so Execution matches latest Workspace saves.</summary>
    private void SyncWorkspaceFromSettings()
    {
        _auto.LoadFromSettings();
    }

    private void AppendLog(string msg)
    {
        string line = $"[{DateTime.Now:HH:mm:ss}] {msg}";
        txtLog.AppendText(line + "\n");
        txtLog.ScrollToEnd();
        _auto.AppendGlobalLog(line);
    }

    private void AppendLogThreadSafe(string msg) => Dispatcher.BeginInvoke(() => AppendLog(msg));

    private void Dispatch(Action a) => Dispatcher.BeginInvoke(a);

    private void SetStage(string text) => lblStage.Text = text;

    private void SetProgress(double v) => pbar.Value = v;

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
