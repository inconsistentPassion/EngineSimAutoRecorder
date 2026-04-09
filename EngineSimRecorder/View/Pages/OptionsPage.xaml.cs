using System;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using EngineSimRecorder.Core;
using Wpf.Ui.Appearance;

namespace EngineSimRecorder.View.Pages;

public partial class OptionsPage : Page
{
    public static OptionsPage? Instance { get; private set; }

    public OptionsPage()
    {
        InitializeComponent();
        Instance = this;
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
        pnlCutoff.Visibility = settings.InteriorMode ? Visibility.Visible : Visibility.Collapsed;
        pnlExhaustPreset.Visibility = settings.InteriorMode ? Visibility.Collapsed : Visibility.Visible;
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
    }

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
        if (pnlCutoff == null || pnlExhaustPreset == null) return;
        bool isInterior = rbInterior.IsChecked == true;
        pnlCutoff.Visibility = isInterior ? Visibility.Visible : Visibility.Collapsed;
        pnlExhaustPreset.Visibility = isInterior ? Visibility.Collapsed : Visibility.Visible;
    }

    private void cmbCarType_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (pnlCustom == null) return;
        string? type = (cmbCarType.SelectedItem as ComboBoxItem)?.Content?.ToString();
        pnlCustom.Visibility = type == "Custom" ? Visibility.Visible : Visibility.Collapsed;
    }

    private void cmbExhaustPreset_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (pnlCustomExterior == null) return;
        pnlCustomExterior.Visibility = GetExteriorPreset() == ExteriorPreset.Custom
            ? Visibility.Visible : Visibility.Collapsed;
    }

    private void slCustomExt_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
    {
        if (!IsLoaded || lblExtLpHz == null) return;
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

    private void btnDeleteProfile_Click(object sender, RoutedEventArgs e)
    {
        if (cmbProfiles.SelectedItem is not string name) { lblProfileStatus.Text = "Select a profile."; return; }
        if (System.Windows.MessageBox.Show($"Delete profile '{name}'?", "Confirm", System.Windows.MessageBoxButton.YesNo, System.Windows.MessageBoxImage.Question) == System.Windows.MessageBoxResult.Yes)
        { RpmProfile.Delete(name); RefreshProfiles(); lblProfileStatus.Text = $"Deleted '{name}'."; }
    }

    public void SaveSettings()
    {
        var settings = AppSettings.Load();
        settings.SampleRate = cmbSampleRate.SelectedIndex == 1 ? 48000 : 44100;
        settings.Channels = cmbChannels.SelectedIndex == 1 ? 1 : 2;
        settings.InteriorMode = rbInterior.IsChecked == true;
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

