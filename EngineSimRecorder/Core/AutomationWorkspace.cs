using EngineSimRecorder.Services;

namespace EngineSimRecorder.Core;

/// <summary>
/// Paths and options for the FMOD → Assetto Corsa automation pipeline (persisted in settings.json).
/// </summary>
public sealed class AutomationWorkspace
{
    public string CarName { get; set; } = "";
    public string FmodProjectPath { get; set; } = "";
    public string AcContentPath { get; set; } = "";
    public string RecordingsDirExt { get; set; } = "";
    public string RecordingsDirInt { get; set; } = "";
    public string RecordingsDirLimiter { get; set; } = "";
    public FmodGenerationMode FmodGenerationMode { get; set; } = FmodGenerationMode.UseExistingTemplate;
    public bool CopyArtifactsAfterBuild { get; set; } = true;
}
