using System;
using System.Linq;
using System.Runtime.Versioning;
using System.Threading;
using System.Windows;
using System.Windows.Controls;
using EngineSimRecorder.Core;
using EngineSimRecorder.ViewModel.Pages;
using Wpf.Ui.Appearance;
using Wpf.Ui.Controls;
using Wpf.Ui;

namespace EngineSimRecorder.View.Pages;

[SupportedOSPlatform("windows7.0")]
public partial class OptionsPage : Page
{
    public static OptionsPage? Instance { get; private set; }
    private readonly IContentDialogService _dialogService;
    private bool _isUpdatingSliders = false;

    public OptionsPage()
    {
        Instance = this;
        InitializeComponent();
        _dialogService = App.GetService<IContentDialogService>();
        Loaded += OnLoaded;
    }

    private bool _wired = false;
    private void OnLoaded(object sender, RoutedEventArgs e)
    {
        if (_wired) return;
        _wired = true;

        // Theme
        cmbTheme.SelectionChanged += cmbTheme_SelectionChanged;

        rbExterior.Checked += rbMode_Checked;
        rbInterior.Checked += rbMode_Checked;
        cmbCarType.SelectionChanged += cmbCarType_SelectionChanged;
        cmbExhaustPreset.SelectionChanged += cmbExhaustPreset_SelectionChanged;

        expSound.Expanded += (s, e) => SaveSettings();
        expSound.Collapsed += (s, e) => SaveSettings();

        chkExtNoise.Click += (s, e) => {
            if (!_isUpdatingSliders && GetExteriorPreset() != ExteriorPreset.Custom && GetExteriorPreset() != ExteriorPreset.Raw)
                cmbExhaustPreset.SelectedIndex = (int)ExteriorPreset.Custom;
            SaveSettings();
        };

        var sliders = new[] { slCutoff, slRumbleHz, slRumbleDb, slRes1Hz, slRes1Db, slRes2Hz, slRes2Db, slWidth, slReverbMix, slReverbMs, slCompRatio, slCompThresh };
        foreach (var sl in sliders) sl.ValueChanged += slCustom_ValueChanged;

        var extSliders = new[] { slExtLpHz, slExtLpQ, slExtHsHz, slExtHsGainDb, slExtMidHz, slExtMidGainDb, slExtSatDrive, slExtReverbMs, slExtReverbMix, slExtCompRatio, slExtCompThresh };
        foreach (var sl in extSliders) sl.ValueChanged += slCustomExt_ValueChanged;

        // Load saved settings
        var settings = AppSettings.Load();
        cmbSampleRate.SelectedIndex = settings.SampleRate == 48000 ? 1 : 0;
        cmbChannels.SelectedIndex = settings.Channels == 1 ? 1 : 0;
        rbInterior.IsChecked = settings.InteriorMode;
        rbExterior.IsChecked = !settings.InteriorMode;
        expSound.IsExpanded = settings.SoundExpanderExpanded;
        chkRecordLimiter.IsChecked = settings.RecordLimiter;
        chkGeneratePowerLut.IsChecked = settings.GeneratePowerLut;
        
        cmbCarType.SelectedItem = cmbCarType.Items.OfType<ComboBoxItem>().FirstOrDefault(i => i.Content?.ToString() == settings.CarType);

        // Restore exterior preset
        int exhaustIdx = (int)(settings.ExteriorPreset);
        if (exhaustIdx >= 0 && exhaustIdx < cmbExhaustPreset.Items.Count)
            cmbExhaustPreset.SelectedIndex = exhaustIdx;
        pnlCustomExterior.Visibility = settings.ExteriorPreset == ExteriorPreset.Custom
            ? Visibility.Visible : Visibility.Collapsed;

        // Restore exterior custom values
        var ext = settings.Exterior;
        slExtLpHz.Value       = ext.LpHz;
        slExtLpQ.Value        = ext.LpQ;
        slExtHsHz.Value       = ext.HsHz;
        slExtHsGainDb.Value   = ext.HsGainDb;
        slExtMidHz.Value      = ext.MidHz;
        slExtMidGainDb.Value  = ext.MidGainDb;
        slExtSatDrive.Value   = ext.SatDrive;
        chkExtNoise.IsChecked = ext.EnableNoise;
        slExtReverbMs.Value   = ext.ReverbMs;
        slExtReverbMix.Value  = ext.ReverbMix * 100.0;
        slExtCompRatio.Value  = ext.CompRatio;
        slExtCompThresh.Value = ext.CompThreshDb;

        // Theme
        cmbTheme.SelectedIndex = settings.Theme switch { "Light" => 1, "System" => 2, _ => 0 };

        // Profiles
        btnLoadProfile.Click += btnLoadProfile_Click;
        btnDeleteProfile.Click += btnDeleteProfile_Click;
        btnSaveProfile.Click += btnSaveProfile_Click;
        RefreshProfiles();
        
        if (!string.IsNullOrEmpty(settings.LastProfile))
            cmbProfiles.SelectedItem = cmbProfiles.Items.OfType<string>().FirstOrDefault(i => i == settings.LastProfile);

        UpdateCustomPanelsVisibility();
    }

    private void UpdateCustomPanelsVisibility()
    {
        if (!IsLoaded || pnlCustomExterior == null || pnlCustom == null || lblAdvancedEmpty == null) return;

        bool isExterior = rbExterior.IsChecked == true;
        bool isInterior = rbInterior.IsChecked == true;

        // Exterior
        pnlExhaustPreset.Visibility = isExterior ? Visibility.Visible : Visibility.Collapsed;
        bool isExtCustom = isExterior && GetExteriorPreset() == ExteriorPreset.Custom;
        pnlCustomExterior.Visibility = isExtCustom ? Visibility.Visible : Visibility.Collapsed;

        // Interior
        pnlCutoff.Visibility = isInterior ? Visibility.Visible : Visibility.Collapsed;
        string? carType = (cmbCarType.SelectedItem as ComboBoxItem)?.Content?.ToString();
        bool isIntCustom = isInterior && carType == "Custom";
        pnlCustom.Visibility = isIntCustom ? Visibility.Visible : Visibility.Collapsed;

        // Advanced Placeholders/Empty State
        bool isRaw = isExterior && GetExteriorPreset() == ExteriorPreset.Raw;
        lblAdvancedEmpty.Visibility = isRaw ? Visibility.Visible : Visibility.Collapsed;
    }

    [SupportedOSPlatform("windows7.0")]
    private void cmbTheme_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (!IsLoaded) return;
        string theme = cmbTheme.SelectedIndex switch { 1 => "Light", 2 => "System", _ => "Dark" };
        var appTheme = theme switch
        {
            "Light" => ApplicationTheme.Light,
            "System" => ApplicationTheme.Unknown,
            _ => ApplicationTheme.Dark
        };
        ApplicationThemeManager.Apply(appTheme);
    }

    private void rbMode_Checked(object sender, RoutedEventArgs e)
    {
        if (!IsLoaded) return;
        UpdateCustomPanelsVisibility();
        SaveSettings();
    }

    private void cmbCarType_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (!IsLoaded || pnlCustom == null) return;
        
        string? type = (cmbCarType.SelectedItem as ComboBoxItem)?.Content?.ToString();
        if (type != "Custom" && !_isUpdatingSliders)
        {
            SyncInteriorSlidersToPreset(type ?? "Sedan");
        }

        UpdateCustomPanelsVisibility();
        SaveSettings();
    }

    private void SyncInteriorSlidersToPreset(string carType)
    {
        _isUpdatingSliders = true;
        var p = InteriorProcessor.GetPresetParams(carType);
        slCutoff.Value = p.CutoffHz;
        slWidth.Value = p.Width * 100.0;
        slRumbleHz.Value = p.RumbleHz;
        slRumbleDb.Value = p.RumbleDb;
        slRes1Hz.Value = p.Res1Hz;
        slRes1Db.Value = p.Res1Db;
        slRes2Hz.Value = p.Res2Hz;
        slRes2Db.Value = p.Res2Db;
        slReverbMs.Value = p.ReverbMs;
        slReverbMix.Value = p.ReverbMix * 100.0;
        slCompRatio.Value = p.CompRatio;
        slCompThresh.Value = p.CompThreshDb;
        _isUpdatingSliders = false;
    }

    private void cmbExhaustPreset_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (!IsLoaded || pnlCustomExterior == null) return;

        var preset = GetExteriorPreset();
        if (preset != ExteriorPreset.Custom && preset != ExteriorPreset.Raw && !_isUpdatingSliders)
        {
            SyncExteriorSlidersToPreset(preset);
        }

        UpdateCustomPanelsVisibility();
        SaveSettings();
    }

    private void SyncExteriorSlidersToPreset(ExteriorPreset preset)
    {
        _isUpdatingSliders = true;
        var p = ExteriorProcessor.GetPresetParams(preset);
        slExtLpHz.Value = p.LpHz;
        slExtLpQ.Value = p.LpQ;
        slExtHsHz.Value = p.HsHz;
        slExtHsGainDb.Value = p.HsGainDb;
        slExtMidHz.Value = p.MidHz;
        slExtMidGainDb.Value = p.MidGainDb;
        slExtSatDrive.Value = p.SatDrive;
        chkExtNoise.IsChecked = p.EnableNoise;
        slExtReverbMs.Value = p.ReverbMs;
        slExtReverbMix.Value = p.ReverbMix * 100.0;
        slExtCompRatio.Value = p.CompRatio;
        slExtCompThresh.Value = p.CompThreshDb;
        _isUpdatingSliders = false;
    }

    private void slCustomExt_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
    {
        if (!IsLoaded || lblExtLpHz == null) return;

        if (!_isUpdatingSliders && GetExteriorPreset() != ExteriorPreset.Custom && GetExteriorPreset() != ExteriorPreset.Raw)
        {
            cmbExhaustPreset.SelectedIndex = (int)ExteriorPreset.Custom;
        }

        lblExtLpHz.Text      = $"{(int)slExtLpHz.Value} Hz";
        lblExtLpQ.Text       = $"{slExtLpQ.Value:F1}";
        lblExtHsHz.Text      = $"{(int)slExtHsHz.Value} Hz";
        lblExtHsGainDb.Text  = $"{(int)slExtHsGainDb.Value} dB";
        lblExtMidHz.Text     = $"{(int)slExtMidHz.Value} Hz";
        lblExtMidGainDb.Text = slExtMidGainDb.Value >= 0
            ? $"+{(int)slExtMidGainDb.Value} dB" : $"{(int)slExtMidGainDb.Value} dB";
        lblExtSatDrive.Text  = $"{slExtSatDrive.Value:F1}";
        lblExtReverbMs.Text  = $"{(int)slExtReverbMs.Value} ms";
        lblExtReverbMix.Text = $"{(int)slExtReverbMix.Value}%";
        lblExtCompRatio.Text = $"{(int)slExtCompRatio.Value}:1";
        lblExtCompThresh.Text= $"{(int)slExtCompThresh.Value} dB";
    }

    private void slCustom_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
    {
        if (!IsLoaded || lblCutoff == null) return;

        if (!_isUpdatingSliders && (cmbCarType.SelectedItem as ComboBoxItem)?.Content?.ToString() != "Custom")
        {
            cmbCarType.SelectedItem = cmbCarType.Items.OfType<ComboBoxItem>().FirstOrDefault(i => i.Content?.ToString() == "Custom");
        }

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

    private void RefreshProfiles()
    {
        cmbProfiles.Items.Clear();
        foreach (string name in RpmProfile.GetProfileNames()) cmbProfiles.Items.Add(name);
    }

    private void btnSaveProfile_Click(object sender, RoutedEventArgs e)
    {
        var rec = RecorderPage.Instance;
        string name = txtProfileName.Text.Trim();
        if (string.IsNullOrEmpty(name)) { lblProfileStatus.Text = "Enter a profile name."; return; }
        if (rec == null) return;
        
        var profile = rec.ExportProfileState();
        profile.Name = name;
        profile.SampleRate = cmbSampleRate.SelectedIndex == 1 ? 48000 : 44100;
        profile.Channels = cmbChannels.SelectedIndex == 1 ? 1 : 2;
        
        profile.Save();
        RefreshProfiles();
        cmbProfiles.SelectedItem = name;
        lblProfileStatus.Text = $"Saved '{name}' ({profile.TargetRpms.Count} RPM targets)";
    }

    private void btnLoadProfile_Click(object sender, RoutedEventArgs e)
    {
        var rec = RecorderPage.Instance;
        if (cmbProfiles.SelectedItem is not string name) { lblProfileStatus.Text = "Select a profile."; return; }
        var profile = RpmProfile.Load(name);
        if (profile == null) { lblProfileStatus.Text = $"Failed to load '{name}'."; RefreshProfiles(); return; }
        
        if (rec != null)
        {
            rec.ImportProfileState(profile);
        }
        
        cmbSampleRate.SelectedIndex = profile.SampleRate == 48000 ? 1 : 0;
        cmbChannels.SelectedIndex = profile.Channels == 1 ? 1 : 0;
        lblProfileStatus.Text = $"Loaded '{name}' ({profile.TargetRpms.Count} RPM targets)";
    }

    private async void btnDeleteProfile_Click(object sender, RoutedEventArgs e)
    {
        if (cmbProfiles.SelectedItem is not string name) { lblProfileStatus.Text = "Select a profile."; return; }
        
        var uiMessageBox = new Wpf.Ui.Controls.MessageBox
        {
            Title = "Confirm Deletion",
            Content = $"Delete profile '{name}'?",
            PrimaryButtonText = "Delete",
            CloseButtonText = "Cancel"
        };

        var result = await uiMessageBox.ShowDialogAsync();

        if (result == Wpf.Ui.Controls.MessageBoxResult.Primary)
        { 
            RpmProfile.Delete(name); 
            RefreshProfiles(); 
            lblProfileStatus.Text = $"Deleted '{name}'."; 
        }
    }

    public void SaveSettings()
    {
        var settings = AppSettings.Load();
        settings.SampleRate = cmbSampleRate.SelectedIndex == 1 ? 48000 : 44100;
        settings.Channels = cmbChannels.SelectedIndex == 1 ? 1 : 2;
        settings.InteriorMode = rbInterior.IsChecked == true;
        settings.SoundExpanderExpanded = expSound.IsExpanded;
        settings.CarType = (cmbCarType.SelectedItem as ComboBoxItem)?.Content?.ToString() ?? "Sedan";
        settings.RecordLimiter = chkRecordLimiter.IsChecked == true;
        settings.GeneratePowerLut = chkGeneratePowerLut.IsChecked == true;
        settings.Theme = cmbTheme.SelectedIndex switch { 1 => "Light", 2 => "System", _ => "Dark" };
        settings.ExteriorPreset = GetExteriorPreset();
        settings.Exterior = new ExteriorSettings
        {
            LpHz         = (float)slExtLpHz.Value,
            LpQ          = (float)slExtLpQ.Value,
            HsHz         = (float)slExtHsHz.Value,
            HsGainDb     = (float)slExtHsGainDb.Value,
            MidHz        = (float)slExtMidHz.Value,
            MidGainDb    = (float)slExtMidGainDb.Value,
            SatDrive     = (float)slExtSatDrive.Value,
            EnableNoise  = chkExtNoise.IsChecked == true,
            ReverbMs     = (float)slExtReverbMs.Value,
            ReverbMix    = (float)(slExtReverbMix.Value / 100.0),
            CompRatio    = (float)slExtCompRatio.Value,
            CompThreshDb = (float)slExtCompThresh.Value,
        };
        settings.Save();
    }

    /// <summary>Returns the currently selected exterior exhaust preset.</summary>
    public ExteriorPreset GetExteriorPreset()
    {
        int idx = cmbExhaustPreset?.SelectedIndex ?? 0;
        return idx >= 0 && idx <= (int)ExteriorPreset.Custom
            ? (ExteriorPreset)idx
            : ExteriorPreset.Raw;
    }
}

