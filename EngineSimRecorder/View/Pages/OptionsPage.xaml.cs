using System;
using System.Windows;
using System.Windows.Controls;
using EngineSimRecorder.Core;

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

        rbExterior.Checked += rbMode_Checked;
        rbInterior.Checked += rbMode_Checked;
        cmbCarType.SelectionChanged += cmbCarType_SelectionChanged;

        foreach (var sl in new[] { slCutoff, slRumbleHz, slRumbleDb, slRes1Hz, slRes1Db, slRes2Hz, slRes2Db, slWidth, slReverbMix, slReverbMs, slCompRatio, slCompThresh })
            sl.ValueChanged += slCustom_ValueChanged;

        // Load saved settings
        var settings = AppSettings.Load();
        cmbSampleRate.SelectedIndex = settings.SampleRate == 48000 ? 1 : 0;
        cmbChannels.SelectedIndex = settings.Channels == 1 ? 1 : 0;
        rbInterior.IsChecked = settings.InteriorMode;
        rbExterior.IsChecked = !settings.InteriorMode;
        pnlCutoff.Visibility = settings.InteriorMode ? Visibility.Visible : Visibility.Collapsed;
        chkRecordLimiter.IsChecked = settings.RecordLimiter;
        chkGeneratePowerLut.IsChecked = settings.GeneratePowerLut;
        for (int i = 0; i < cmbCarType.Items.Count; i++)
            if ((cmbCarType.Items[i] as ComboBoxItem)?.Content?.ToString() == settings.CarType)
            { cmbCarType.SelectedIndex = i; break; }

        // Profiles
        btnLoadProfile.Click += btnLoadProfile_Click;
        btnDeleteProfile.Click += btnDeleteProfile_Click;
        btnSaveProfile.Click += btnSaveProfile_Click;
        RefreshProfiles();
        if (!string.IsNullOrEmpty(settings.LastProfile))
            for (int i = 0; i < cmbProfiles.Items.Count; i++)
                if (cmbProfiles.Items[i]?.ToString() == settings.LastProfile)
                { cmbProfiles.SelectedIndex = i; break; }
    }

    private void rbMode_Checked(object sender, RoutedEventArgs e)
    {
        if (pnlCutoff != null) pnlCutoff.Visibility = rbInterior.IsChecked == true ? Visibility.Visible : Visibility.Collapsed;
    }

    private void cmbCarType_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (pnlCustom == null) return;
        string? type = (cmbCarType.SelectedItem as ComboBoxItem)?.Content?.ToString();
        pnlCustom.Visibility = type == "Custom" ? Visibility.Visible : Visibility.Collapsed;
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

    // ── Profiles ──
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
        var targets = new System.Collections.Generic.List<int>();
        foreach (var item in rec.lstRpm.Items) targets.Add(Convert.ToInt32(item));
        var profile = new RpmProfile
        {
            Name = name, CarName = rec.txtCarName.Text.Trim(), Prefix = rec.txtPrefix.Text.Trim(),
            OutputDir = rec.txtOutputDir.Text.Trim(), TargetRpms = targets,
            SampleRate = cmbSampleRate.SelectedIndex == 1 ? 48000 : 44100,
            Channels = cmbChannels.SelectedIndex == 1 ? 1 : 2,
        };
        profile.Save();
        RefreshProfiles();
        for (int i = 0; i < cmbProfiles.Items.Count; i++)
            if (cmbProfiles.Items[i]?.ToString() == name) { cmbProfiles.SelectedIndex = i; break; }
        lblProfileStatus.Text = $"Saved '{name}' ({targets.Count} RPM targets)";
    }

    private void btnLoadProfile_Click(object sender, RoutedEventArgs e)
    {
        var rec = RecorderPage.Instance;
        if (cmbProfiles.SelectedItem is not string name) { lblProfileStatus.Text = "Select a profile."; return; }
        var profile = RpmProfile.Load(name);
        if (profile == null) { lblProfileStatus.Text = $"Failed to load '{name}'."; RefreshProfiles(); return; }
        if (rec != null)
        {
            rec.txtCarName.Text = profile.CarName;
            rec.txtPrefix.Text = profile.Prefix;
            rec.txtOutputDir.Text = profile.OutputDir ?? "recordings";
            rec.lstRpm.Items.Clear();
            foreach (int rpm in profile.TargetRpms) rec.lstRpm.Items.Add(rpm);
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
        settings.Save();
    }
}
