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
        }

        _auto.LoadFromSettings();
        RefreshFromService();
        IsVisibleChanged += (_, _) => { if (IsVisible) RefreshFromService(); };
    }

    private void RefreshFromService()
    {
        if (_auto.LastPipelineSuccess == true)
        {
            borderStatus.Background = new SolidColorBrush(Color.FromArgb(0x40, 0x00, 0xFF, 0x00));
            borderStatus.BorderBrush = new SolidColorBrush(Color.FromArgb(0x80, 0x00, 0xFF, 0x00));
            borderStatus.BorderThickness = new Thickness(1);
            lblStatus.Foreground = new SolidColorBrush(Color.FromRgb(0x80, 0xFF, 0x80));
            lblStatus.Text = _auto.LastCompletionMessage ?? "Last pipeline run succeeded.";
        }
        else if (_auto.LastPipelineSuccess == false)
        {
            borderStatus.Background = new SolidColorBrush(Color.FromArgb(0x40, 0xFF, 0x40, 0x40));
            borderStatus.BorderBrush = new SolidColorBrush(Color.FromArgb(0x80, 0xFF, 0x40, 0x40));
            borderStatus.BorderThickness = new Thickness(1);
            lblStatus.Foreground = new SolidColorBrush(Color.FromRgb(0xFF, 0x80, 0x80));
            lblStatus.Text = _auto.LastCompletionMessage ?? "Last pipeline run did not complete successfully.";
        }
        else
        {
            borderStatus.Background = Brushes.Transparent;
            borderStatus.BorderThickness = new Thickness(0);
            lblStatus.Foreground = (Brush)(TryFindResource("TextFillColorSecondaryBrush")
                ?? new SolidColorBrush(Color.FromRgb(0xA0, 0xA0, 0xA0)));
            lblStatus.Text = "Run automation from Execution to see deploy results here.";
        }

        if (_auto.LastCopiedFiles is { Length: > 0 })
        {
            var sb = new StringBuilder();
            foreach (string p in _auto.LastCopiedFiles)
                sb.AppendLine(p);
            txtCopied.Text = sb.ToString().TrimEnd();
        }
        else
            txtCopied.Text = _auto.LastPipelineSuccess == null
                ? ""
                : "(No files copied — post-build copy may have been skipped.)";
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
            RefreshFromService();
            TryOpenDir(Path.Combine(_auto.AcContentPath, "cars", _auto.CarName, "sfx"));
        }
        catch (Exception ex)
        {
            _auto.LastPipelineSuccess = false;
            _auto.LastCompletionMessage = ex.Message;
            lblCopyOnlyResult.Text = ex.Message;
            _snackbarService.Show("Copy failed", ex.Message, ControlAppearance.Danger, new SymbolIcon(SymbolRegular.ErrorCircle24), TimeSpan.FromSeconds(5));
            RefreshFromService();
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
