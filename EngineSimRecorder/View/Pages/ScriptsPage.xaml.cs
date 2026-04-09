using System;
using System.Diagnostics;
using System.IO;
using System.Reflection;
using System.Windows;
using System.Windows.Controls;

namespace EngineSimRecorder.View.Pages;

public partial class ScriptsPage : Page
{
    public static ScriptsPage? Instance { get; private set; }

    private const string ScriptResourceName = "EngineSimRecorder.Scripts.fmod_import_recordings.js";
    private const string ScriptFileName = "fmod_import_recordings.js";
    private string? _scriptContent;

    public ScriptsPage()
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
        WireEvents();
        LoadScriptPreview();
    }

    private void WireEvents()
    {
        btnBrowseFmod.Click += BtnBrowseFmod_Click;
        btnDeploy.Click += BtnDeploy_Click;
        btnCopyScript.Click += BtnCopyScript_Click;
        btnOpenRecordings.Click += BtnOpenRecordings_Click;
        txtFmodProjectDir.TextChanged += TxtFmodProjectDir_TextChanged;
    }

    // ── Script Loading ──
    private void LoadScriptPreview()
    {
        try
        {
            var assembly = Assembly.GetExecutingAssembly();
            using var stream = assembly.GetManifestResourceStream(ScriptResourceName);
            if (stream == null)
            {
                txtScriptPreview.Text = "// Error: Script resource not found.";
                return;
            }
            using var reader = new StreamReader(stream);
            _scriptContent = reader.ReadToEnd();
            txtScriptPreview.Text = _scriptContent;
        }
        catch (Exception ex)
        {
            txtScriptPreview.Text = $"// Error loading script: {ex.Message}";
        }
    }

    // ── Browse for FMOD project ──
    private void BtnBrowseFmod_Click(object sender, RoutedEventArgs e)
    {
        var dlg = new System.Windows.Forms.FolderBrowserDialog
        {
            Description = "Select FMOD Studio project folder",
            UseDescriptionForTitle = true
        };

        if (dlg.ShowDialog() == System.Windows.Forms.DialogResult.OK)
        {
            txtFmodProjectDir.Text = dlg.SelectedPath;
        }
    }

    // ── Update recordings dir label when project dir changes ──
    private void TxtFmodProjectDir_TextChanged(object sender, TextChangedEventArgs e)
    {
        string dir = txtFmodProjectDir.Text.Trim();
        if (string.IsNullOrEmpty(dir))
        {
            lblRecordingsDir.Text = "(set an FMOD project above)";
            btnOpenRecordings.IsEnabled = false;
            return;
        }

        string recordingsPath = Path.Combine(dir, "Recordings");
        lblRecordingsDir.Text = recordingsPath;
        btnOpenRecordings.IsEnabled = true;
    }

    // ── Open Recordings folder ──
    private void BtnOpenRecordings_Click(object sender, RoutedEventArgs e)
    {
        string dir = txtFmodProjectDir.Text.Trim();
        if (string.IsNullOrEmpty(dir)) return;

        string recordingsPath = Path.Combine(dir, "Recordings");
        try
        {
            if (!Directory.Exists(recordingsPath))
                Directory.CreateDirectory(recordingsPath);
            Process.Start("explorer.exe", recordingsPath);
        }
        catch (Exception ex)
        {
            lblDeployStatus.Text = $"Error: {ex.Message}";
        }
    }

    // ── Deploy Script ──
    private void BtnDeploy_Click(object sender, RoutedEventArgs e)
    {
        string projectDir = txtFmodProjectDir.Text.Trim();
        if (string.IsNullOrEmpty(projectDir))
        {
            lblDeployStatus.Text = "Please select an FMOD project folder first.";
            return;
        }

        if (!Directory.Exists(projectDir))
        {
            lblDeployStatus.Text = "Directory does not exist.";
            return;
        }

        // Validate this looks like an FMOD project (should contain an .fspro file)
        var fsproPfiles = Directory.GetFiles(projectDir, "*.fspro");
        if (fsproPfiles.Length == 0)
        {
            var result = MessageBox.Show(
                "No .fspro file found in this directory.\nThis may not be an FMOD Studio project.\n\nDeploy anyway?",
                "No FMOD Project Detected",
                MessageBoxButton.YesNo,
                MessageBoxImage.Question);
            if (result != MessageBoxResult.Yes) return;
        }

        try
        {
            string scriptsDir = Path.Combine(projectDir, "scripts");
            Directory.CreateDirectory(scriptsDir);

            string targetPath = Path.Combine(scriptsDir, ScriptFileName);
            bool existed = File.Exists(targetPath);

            if (_scriptContent == null)
            {
                lblDeployStatus.Text = "Error: Script content not loaded.";
                return;
            }

            File.WriteAllText(targetPath, _scriptContent);

            // Also create the Recordings folder if it doesn't exist
            string recordingsDir = Path.Combine(projectDir, "Recordings");
            Directory.CreateDirectory(recordingsDir);

            lblDeployStatus.Text = existed
                ? $"Updated: {targetPath}"
                : $"Deployed: {targetPath}";

            lblRecordingsDir.Text = recordingsDir;
            btnOpenRecordings.IsEnabled = true;
        }
        catch (Exception ex)
        {
            lblDeployStatus.Text = $"Deploy failed: {ex.Message}";
            MessageBox.Show($"Failed to deploy script:\n{ex.Message}", "Deploy Error",
                MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    // ── Copy to Clipboard ──
    private void BtnCopyScript_Click(object sender, RoutedEventArgs e)
    {
        if (string.IsNullOrEmpty(_scriptContent))
        {
            return;
        }

        try
        {
            Clipboard.SetText(_scriptContent);
            string original = btnCopyScript.Content?.ToString() ?? "";
            btnCopyScript.Content = "Copied!";
            var timer = new System.Windows.Threading.DispatcherTimer
            {
                Interval = TimeSpan.FromSeconds(2)
            };
            timer.Tick += (s, _) =>
            {
                btnCopyScript.Content = original;
                timer.Stop();
            };
            timer.Start();
        }
        catch (Exception ex)
        {
            lblDeployStatus.Text = $"Copy failed: {ex.Message}";
        }
    }
}
