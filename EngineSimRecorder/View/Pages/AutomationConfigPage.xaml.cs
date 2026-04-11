using System;
using System.IO;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using EngineSimRecorder.Core;
using EngineSimRecorder.Services;
using Wpf.Ui.Appearance;
using Wpf.Ui.Controls;
using Wpf.Ui;

namespace EngineSimRecorder.View.Pages;

public partial class AutomationConfigPage : Page
{
    private readonly AutomationService _auto;
    private readonly ISnackbarService _snackbarService;
    private bool _wired;

    public AutomationConfigPage(AutomationService auto)
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

        PushServiceToUi();
        TryPrefillFromRecorder();

        btnConnect.Click += BtnConnect_Click;
        btnDisconnect.Click += BtnDisconnect_Click;
        btnBrowseFmod.Click += (_, _) => BrowseFolder("Select FMOD Studio project folder", p => { txtFmodProject.Text = p; });
        btnBrowseRecordings.Click += (_, _) => BrowseFolder("Select EXT recordings folder", p => { txtRecordingsDir.Text = p; });
        btnBrowseRecordingsInt.Click += (_, _) => BrowseFolder("Select INT recordings folder", p => { txtRecordingsDirInt.Text = p; });
        btnBrowseRecordingsLimiter.Click += (_, _) => BrowseFolder("Select Limiter recordings folder", p => { txtRecordingsDirLimiter.Text = p; });
        btnBrowseAc.Click += (_, _) => BrowseFolder("Select Assetto Corsa content folder", p => { txtAcContent.Text = p; });

        txtCarName.TextChanged += (_, _) => { PullUiToService(); SaveQuiet(); };
        txtFmodProject.TextChanged += (_, _) => { PullUiToService(); SaveQuiet(); };
        txtRecordingsDir.TextChanged += (_, _) => { PullUiToService(); SaveQuiet(); };
        txtRecordingsDirInt.TextChanged += (_, _) => { PullUiToService(); SaveQuiet(); };
        txtRecordingsDirLimiter.TextChanged += (_, _) => { PullUiToService(); SaveQuiet(); };
        txtAcContent.TextChanged += (_, _) => { PullUiToService(); SaveQuiet(); };
        cmbGenerationMode.SelectionChanged += (_, _) =>
        {
            PullUiToService();
            ApplyModeUi();
            SaveQuiet();
        };
        chkCopyArtifacts.Checked += (_, _) => { PullUiToService(); SaveQuiet(); };
        chkCopyArtifacts.Unchecked += (_, _) => { PullUiToService(); SaveQuiet(); };

        ApplyModeUi();
        SetConnUi(_auto.IsConnected);
        IsVisibleChanged += (_, _) =>
        {
            if (IsVisible)
                SetConnUi(_auto.IsConnected);
        };
    }

    private void TryPrefillFromRecorder()
    {
        if (RecorderPage.Instance == null) return;
        string recDir = RecorderPage.Instance.txtOutputDir.Text.Trim();
        if (!string.IsNullOrEmpty(recDir))
        {
            if (string.IsNullOrWhiteSpace(txtRecordingsDir.Text))
                txtRecordingsDir.Text = Path.Combine(recDir, "ext");
            if (string.IsNullOrWhiteSpace(txtRecordingsDirInt.Text))
                txtRecordingsDirInt.Text = Path.Combine(recDir, "int");
        }

        string car = RecorderPage.Instance.txtCarName.Text.Trim();
        if (!string.IsNullOrEmpty(car) && _auto.GenerationMode != FmodGenerationMode.UseExistingTemplate
            && string.IsNullOrWhiteSpace(txtCarName.Text))
            txtCarName.Text = car;

        PullUiToService();
    }

    private void PushServiceToUi()
    {
        txtCarName.Text = _auto.CarName;
        txtFmodProject.Text = _auto.FmodProjectPath;
        txtAcContent.Text = _auto.AcContentPath;
        txtRecordingsDir.Text = _auto.RecordingsDirExt;
        txtRecordingsDirInt.Text = _auto.RecordingsDirInt;
        txtRecordingsDirLimiter.Text = _auto.RecordingsDirLimiter;
        chkCopyArtifacts.IsChecked = _auto.CopyArtifactsAfterBuild;
        cmbGenerationMode.SelectedIndex = _auto.GenerationMode == FmodGenerationMode.UseExistingTemplate ? 0 : 1;
    }

    private void PullUiToService()
    {
        _auto.CarName = txtCarName.Text.Trim();
        _auto.FmodProjectPath = txtFmodProject.Text.Trim();
        _auto.AcContentPath = txtAcContent.Text.Trim();
        _auto.RecordingsDirExt = txtRecordingsDir.Text.Trim();
        _auto.RecordingsDirInt = txtRecordingsDirInt.Text.Trim();
        _auto.RecordingsDirLimiter = txtRecordingsDirLimiter.Text.Trim();
        _auto.CopyArtifactsAfterBuild = chkCopyArtifacts.IsChecked == true;
        _auto.GenerationMode = cmbGenerationMode.SelectedIndex == 0
            ? FmodGenerationMode.UseExistingTemplate
            : FmodGenerationMode.FromScratch;
        _auto.ApplyTemplateModeRules();
    }

    private void ApplyModeUi()
    {
        bool template = cmbGenerationMode.SelectedIndex == 0;
        if (template)
        {
            txtCarName.Text = "fa01";
            txtCarName.IsEnabled = false;
            chkCopyArtifacts.IsEnabled = false;
            chkCopyArtifacts.IsChecked = false;
        }
        else
        {
            txtCarName.IsEnabled = true;
            chkCopyArtifacts.IsEnabled = true;
        }
        PullUiToService();
    }

    private void SaveQuiet()
    {
        try { _auto.SaveToSettings(); } catch { /* ignore */ }
    }

    private void BtnConnect_Click(object sender, RoutedEventArgs e)
    {
        if (_auto.IsConnected) return;
        btnConnect.IsEnabled = false;
        Task.Run(() =>
        {
            try
            {
                _auto.Connect();
                Dispatcher.BeginInvoke(() =>
                {
                    SetConnUi(connected: true);
                    btnConnect.IsEnabled = false;
                    btnDisconnect.IsEnabled = true;
                    _auto.AppendGlobalLog($"[{DateTime.Now:HH:mm:ss}] Connected to FMOD Studio.");
                });
            }
            catch (Exception ex)
            {
                Dispatcher.BeginInvoke(() =>
                {
                    SetConnUi(connected: false);
                    btnConnect.IsEnabled = true;
                    _auto.AppendGlobalLog($"[{DateTime.Now:HH:mm:ss}] Connection failed: {ex.Message}");
                    _snackbarService.Show("FMOD connection", ex.Message, ControlAppearance.Caution, new SymbolIcon(SymbolRegular.Warning24), TimeSpan.FromSeconds(5));
                });
            }
        });
    }

    private void BtnDisconnect_Click(object sender, RoutedEventArgs e)
    {
        _auto.Disconnect();
        SetConnUi(connected: false);
        btnConnect.IsEnabled = true;
        btnDisconnect.IsEnabled = false;
        _auto.AppendGlobalLog($"[{DateTime.Now:HH:mm:ss}] Disconnected from FMOD Studio.");
    }

    private void SetConnUi(bool connected)
    {
        bool isDark = ApplicationThemeManager.GetAppTheme() == ApplicationTheme.Dark;
        if (connected)
        {
            borderConnStatus.Background = new SolidColorBrush(Color.FromArgb(0x33, 0x00, 0xFF, 0x00));
            borderConnStatus.BorderBrush = new SolidColorBrush(Color.FromArgb(0x66, 0x00, 0xFF, 0x00));
            borderConnStatus.BorderThickness = new Thickness(1);
            lblConnStatus.Text = "Connected";
            lblConnStatus.Foreground = new SolidColorBrush(isDark ? Color.FromRgb(0x57, 0xF2, 0x87) : Color.FromRgb(0x1E, 0x8E, 0x3E));
            lblConnDetail.Text = "127.0.0.1:3663";
        }
        else
        {
            borderConnStatus.Background = new SolidColorBrush(Color.FromArgb(0x33, 0xFF, 0x00, 0x00));
            borderConnStatus.BorderBrush = new SolidColorBrush(Color.FromArgb(0x66, 0xFF, 0x00, 0x00));
            borderConnStatus.BorderThickness = new Thickness(1);
            lblConnStatus.Text = "Disconnected";
            lblConnStatus.Foreground = new SolidColorBrush(isDark ? Color.FromRgb(0xFF, 0x80, 0x80) : Color.FromRgb(0xD9, 0x30, 0x25));
            lblConnDetail.Text = "";
        }
    }

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
}
