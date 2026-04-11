using System;
using System.Diagnostics;
using System.IO;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using EngineSimRecorder.Services;
using Wpf.Ui;
using Wpf.Ui.Controls;

namespace EngineSimRecorder.View.Pages;

public partial class AutomationPackagePage : Page
{
    private readonly AutomationService _auto;
    private readonly ISnackbarService _snackbarService;
    private bool _wired;

    public AutomationPackagePage(AutomationService auto)
    {
        _auto = auto;
        _snackbarService = App.GetService<ISnackbarService>();
        InitializeComponent();
        Loaded += OnLoaded;
    }

    private void OnLoaded(object sender, RoutedEventArgs e)
    {
        if (!_wired)
        {
            _wired = true;
            btnOpenFmodBuild.Click += (_, _) => TryOpenDir(Path.Combine(_auto.FmodProjectPath, "Build"));
            btnOpenAcSfx.Click += (_, _) =>
            {
                string sfx = Path.Combine(_auto.AcContentPath, "cars", _auto.CarName, "sfx");
                TryOpenDir(sfx);
            };
            btnOpenRecordingsExt.Click += (_, _) => TryOpenDir(_auto.RecordingsDirExt);
            btnCopyArtifactsOnly.Click += BtnCopyArtifactsOnly_Click;
            btnBuildAndDeploy.Click += BtnBuildAndDeploy_Click;
        }

        _auto.LoadFromSettings();
        IsVisibleChanged += (_, _) => { if (IsVisible) _auto.LoadFromSettings(); };
    }

    private async void BtnBuildAndDeploy_Click(object sender, RoutedEventArgs e)
    {
        lblDeployStatus.Text = "";
        btnBuildAndDeploy.IsEnabled = false;
        pbarDeploy.Visibility = Visibility.Visible;

        try
        {
            _auto.LoadFromSettings();
            if (string.IsNullOrWhiteSpace(_auto.FmodProjectPath))
            {
                throw new Exception("Set FMOD project path on Workspace.");
            }

            lblDeployStatus.Text = "Sending build command to FMOD...";
            _auto.AppendGlobalLog($"[{DateTime.Now:HH:mm:ss}] Build & Deploy: Telling FMOD to build...");
            
            // FMOD 1.08 Studio Scripting API build command
            await _auto.ExecuteScriptAsync("studio.project.build();");
            
            if (string.IsNullOrWhiteSpace(_auto.AcContentPath))
            {
                lblDeployStatus.Text = "Build complete. (Deploy skipped: AC Content Path not set)";
                _auto.AppendGlobalLog($"[{DateTime.Now:HH:mm:ss}] Build & Deploy: Build complete. Skipped copy.");
                _snackbarService.Show("Success", "Build complete using FMOD Studio.", ControlAppearance.Success, new SymbolIcon(SymbolRegular.CheckmarkCircle24), TimeSpan.FromSeconds(5));
            }
            else
            {
                lblDeployStatus.Text = "Build complete. Deploying to AC car sfx...";
                _auto.AppendGlobalLog($"[{DateTime.Now:HH:mm:ss}] Build & Deploy: Build complete. Copying files...");

                string[] copied = _auto.DeployToAc(_auto.CarName);

                lblDeployStatus.Text = $"Successfully deployed {copied.Length} file(s).";
                _auto.AppendGlobalLog($"[{DateTime.Now:HH:mm:ss}] Build & Deploy: Success! {copied.Length} files copied.");

                _snackbarService.Show("Success", $"Deployed {copied.Length} file(s) to {_auto.CarName}.", ControlAppearance.Success, new SymbolIcon(SymbolRegular.CheckmarkCircle24), TimeSpan.FromSeconds(5));
            }
        }
        catch (Exception ex)
        {
            lblDeployStatus.Text = $"Error: {ex.Message}";
            _auto.AppendGlobalLog($"[{DateTime.Now:HH:mm:ss}] Build & Deploy Error: {ex.Message}");
            _snackbarService.Show("Process failed", ex.Message, ControlAppearance.Danger, new SymbolIcon(SymbolRegular.ErrorCircle24), TimeSpan.FromSeconds(5));
        }
        finally
        {
            btnBuildAndDeploy.IsEnabled = true;
            pbarDeploy.Visibility = Visibility.Collapsed;
        }
    }

    private void BtnCopyArtifactsOnly_Click(object sender, RoutedEventArgs e)
    {
        lblCopyOnlyResult.Text = "";
        _auto.LoadFromSettings();
        if (string.IsNullOrWhiteSpace(_auto.CarName))
        {
            _snackbarService.Show("Missing input", "Set car name on Workspace.", ControlAppearance.Caution, new SymbolIcon(SymbolRegular.Warning24), TimeSpan.FromSeconds(5));
            return;
        }
        if (string.IsNullOrWhiteSpace(_auto.FmodProjectPath) || string.IsNullOrWhiteSpace(_auto.AcContentPath))
        {
            _snackbarService.Show("Missing input", "Set FMOD project and AC content paths on Workspace.", ControlAppearance.Caution, new SymbolIcon(SymbolRegular.Warning24), TimeSpan.FromSeconds(5));
            return;
        }

        try
        {
            string[] copied = _auto.DeployToAc(_auto.CarName);
            _auto.LastCopiedFiles = copied;
            _auto.LastPipelineSuccess = true;
            _auto.LastCompletionMessage = "Artifacts copied.";
            _auto.AppendGlobalLog($"[{DateTime.Now:HH:mm:ss}] Copy artifacts only: {copied.Length} file(s).");
            foreach (string p in copied)
                _auto.AppendGlobalLog($"[{DateTime.Now:HH:mm:ss}] Copied: {p}");
            lblCopyOnlyResult.Text = $"Copied {copied.Length} file(s).";
            TryOpenDir(Path.Combine(_auto.AcContentPath, "cars", _auto.CarName, "sfx"));
        }
        catch (Exception ex)
        {
            _auto.LastPipelineSuccess = false;
            _auto.LastCompletionMessage = ex.Message;
            lblCopyOnlyResult.Text = ex.Message;
            _snackbarService.Show("Copy failed", ex.Message, ControlAppearance.Danger, new SymbolIcon(SymbolRegular.ErrorCircle24), TimeSpan.FromSeconds(5));
        }
    }

    private void TryOpenDir(string? path)
    {
        if (string.IsNullOrWhiteSpace(path) || !Directory.Exists(path))
        {
            _snackbarService.Show("Explorer", "Folder does not exist or path is not set.", ControlAppearance.Info, new SymbolIcon(SymbolRegular.Folder24), TimeSpan.FromSeconds(5));
            return;
        }
        try
        {
            Process.Start(new ProcessStartInfo { FileName = "explorer.exe", Arguments = path, UseShellExecute = true });
        }
        catch (Exception ex)
        {
            _snackbarService.Show("Explorer", ex.Message, ControlAppearance.Caution, new SymbolIcon(SymbolRegular.Warning24), TimeSpan.FromSeconds(5));
        }
    }
}
