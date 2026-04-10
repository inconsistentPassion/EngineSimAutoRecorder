using System.IO;
using System.Windows;
using EngineSimRecorder.Core;
using EngineSimRecorder.View.Pages;

namespace EngineSimRecorder.Services;

/// <summary>
/// Shared FMOD TCP automation: workspace paths, script generation, execution, and AC deployment.
/// </summary>
public sealed class AutomationService : IDisposable
{
    private FmodTcpService? _tcp;

    public string CarName { get; set; } = "";
    public string FmodProjectPath { get; set; } = "";
    public string AcContentPath { get; set; } = "";
    public string RecordingsDirExt { get; set; } = "";
    public string RecordingsDirInt { get; set; } = "";
    public string RecordingsDirLimiter { get; set; } = "";
    public FmodGenerationMode GenerationMode { get; set; } = FmodGenerationMode.UseExistingTemplate;
    public bool CopyArtifactsAfterBuild { get; set; } = true;

    public string GeneratedScript { get; private set; } = "";
    public string[] LastViolations { get; private set; } = [];
    public bool LastScriptValid { get; private set; }
    public string? LastFmodResponse { get; set; }
    public string[] LastCopiedFiles { get; set; } = [];
    public bool? LastPipelineSuccess { get; set; }
    public string? LastCompletionMessage { get; set; }

    public bool IsConnected => _tcp?.IsConnected == true;

    public bool TcpLastResponseHadError => _tcp?.LastResponseHadError == true;

    public AutomationService()
    {
        LoadFromSettings();
    }

    public void LoadFromSettings()
    {
        var a = AppSettings.Load().Automation;
        CarName = a.CarName;
        FmodProjectPath = a.FmodProjectPath;
        AcContentPath = a.AcContentPath;
        RecordingsDirExt = a.RecordingsDirExt;
        RecordingsDirInt = a.RecordingsDirInt;
        RecordingsDirLimiter = a.RecordingsDirLimiter;
        GenerationMode = a.FmodGenerationMode;
        CopyArtifactsAfterBuild = a.CopyArtifactsAfterBuild;
        ApplyTemplateModeRules();
    }

    public void SaveToSettings()
    {
        var settings = AppSettings.Load();
        settings.Automation = ToWorkspace();
        settings.Save();
    }

    public AutomationWorkspace ToWorkspace() => new()
    {
        CarName = CarName,
        FmodProjectPath = FmodProjectPath,
        AcContentPath = AcContentPath,
        RecordingsDirExt = RecordingsDirExt,
        RecordingsDirInt = RecordingsDirInt,
        RecordingsDirLimiter = RecordingsDirLimiter,
        FmodGenerationMode = GenerationMode,
        CopyArtifactsAfterBuild = CopyArtifactsAfterBuild
    };

    /// <summary>Template mode uses fixed car id and skips AC copy (matches legacy FMOD import behavior).</summary>
    public void ApplyTemplateModeRules()
    {
        if (GenerationMode != FmodGenerationMode.UseExistingTemplate)
            return;
        CarName = "fa01";
        CopyArtifactsAfterBuild = false;
    }

    public void Connect()
    {
        _tcp?.Dispose();
        _tcp = new FmodTcpService();
        _tcp.Connect();
    }

    public void Disconnect()
    {
        _tcp?.Dispose();
        _tcp = null;
    }

    /// <summary>Ensure TCP is connected; connects if needed.</summary>
    public void EnsureConnected()
    {
        if (_tcp?.IsConnected == true)
            return;
        Connect();
    }

    public Task<string> ExecuteScriptAsync(string jsCode, CancellationToken ct = default)
    {
        EnsureConnected();
        return _tcp!.ExecuteAsync(jsCode, ct);
    }

    public string GenerateAndValidateScript()
    {
        var generator = new FmodScriptGenerator
        {
            CarName = CarName,
            FmodProjectPath = FmodProjectPath,
            RecordingsDirExt = RecordingsDirExt,
            RecordingsDirInt = RecordingsDirInt,
            RecordingsDirLimiter = RecordingsDirLimiter,
            Mode = GenerationMode
        };

        GeneratedScript = generator.GenerateScript();
        LastScriptValid = FmodEs3Validator.IsValid(GeneratedScript, out string[] violations);
        LastViolations = violations;
        return GeneratedScript;
    }

    public string[] DeployToAc(string carFolderName)
    {
        var handler = new FmodPostBuildHandler
        {
            FmodProjectPath = FmodProjectPath,
            AcContentPath = AcContentPath
        };
        return handler.CopyBuildArtifacts(carFolderName);
    }

    /// <param name="formattedLine">Full line including timestamp (same text as local log panes).</param>
    public void AppendGlobalLog(string formattedLine)
    {
        if (Application.Current?.Dispatcher == null) return;
        Application.Current.Dispatcher.BeginInvoke(() => LogPage.Instance?.AppendLog(formattedLine));
    }

    public (int ext, int intCount) GetRecordingCounts()
    {
        int ext = Directory.Exists(RecordingsDirExt)
            ? Directory.GetFiles(RecordingsDirExt, "*.wav").Length
            : 0;
        int intCount = Directory.Exists(RecordingsDirInt)
            ? Directory.GetFiles(RecordingsDirInt, "*.wav").Length
            : 0;
        return (ext, intCount);
    }

    public void Dispose()
    {
        _tcp?.Dispose();
        _tcp = null;
    }
}
