namespace EngineSimRecorder.Services;

/// <summary>
/// Captures the outcome of a complete FMOD import + build + post-build operation.
/// </summary>
public sealed class FmodImportResult
{
    /// <summary>Overall success flag — true only if every stage completed without error.</summary>
    public bool Success { get; set; }

    /// <summary>Raw string returned by FMOD after executing the JavaScript.</summary>
    public string? FmodResponse { get; set; }

    /// <summary>
    /// ES3 validation failures found in the submitted JavaScript.
    /// Empty array when the JS is valid.
    /// </summary>
    public string[] Violations { get; set; } = [];

    /// <summary>Absolute paths of files written during the post-build copy step.</summary>
    public string[] CopiedFiles { get; set; } = [];

    /// <summary>Human-readable error message when <see cref="Success"/> is false.</summary>
    public string? ErrorMessage { get; set; }
}
