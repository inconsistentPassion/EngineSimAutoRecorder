using System;
using System.Collections.Generic;
using System.IO;

namespace EngineSimRecorder.Services;

/// <summary>
/// Handles post-build operations: filters GUIDs.txt and copies the .bank file to the
/// Assetto Corsa content folder.
/// </summary>
public sealed class FmodPostBuildHandler
{
    /// <summary>Absolute path to the root of the FMOD Studio project (.fspro directory).</summary>
    public string FmodProjectPath { get; set; } = string.Empty;

    /// <summary>Absolute path to the Assetto Corsa "content" root (e.g. C:\...\assettocorsa\content).</summary>
    public string AcContentPath { get; set; } = string.Empty;

    /// <summary>
    /// Filters Build/GUIDs.txt to lines relevant to <paramref name="carName"/> plus all "bus" lines,
    /// then copies the filtered GUIDs.txt and the built .bank file to
    /// <c>content/cars/{carName}/sfx/</c>.
    /// </summary>
    /// <returns>Array of destination file paths that were written.</returns>
    /// <exception cref="InvalidOperationException">Thrown if required source files are missing.</exception>
    public string[] CopyBuildArtifacts(string carName)
    {
        if (string.IsNullOrWhiteSpace(FmodProjectPath))
            throw new InvalidOperationException("FmodProjectPath is not set.");
        if (string.IsNullOrWhiteSpace(AcContentPath))
            throw new InvalidOperationException("AcContentPath is not set.");
        if (string.IsNullOrWhiteSpace(carName))
            throw new ArgumentException("carName must not be empty.", nameof(carName));

        // ── Source paths ──────────────────────────────────────────────────────
        string buildDir   = Path.Combine(FmodProjectPath, "Build");
        if (!Directory.Exists(buildDir))
            throw new InvalidOperationException($"Build directory not found in the FMOD project: {buildDir}");

        string? guidsFile  = FindFile(buildDir, "GUIDs.txt");
        if (guidsFile == null)
            throw new InvalidOperationException("GUIDs.txt not found in the FMOD Build directory. Ensure the project is built and outputs GUIDs.");

        string? bankFile = FindBankFile(buildDir, carName);
        if (bankFile == null)
            throw new InvalidOperationException($"No compiled .bank file found for car {carName} in the FMOD Build directory.");


        // ── Destination ───────────────────────────────────────────────────────
        string sfxDir = Path.Combine(AcContentPath, "cars", carName, "sfx");
        Directory.CreateDirectory(sfxDir);

        var copiedFiles = new List<string>();

        // ── Filter GUIDs.txt ─────────────────────────────────────────────────
        string destGuids = Path.Combine(sfxDir, "GUIDs.txt");
        FilterGuidsFile(guidsFile, destGuids, carName);
        copiedFiles.Add(destGuids);

        // ── Copy .bank ────────────────────────────────────────────────────────
        string destBank = Path.Combine(sfxDir, $"{carName}.bank");
        File.Copy(bankFile, destBank, overwrite: true);
        copiedFiles.Add(destBank);

        return copiedFiles.ToArray();
    }

    // ── Helpers ───────────────────────────────────────────────────────────────

    private static void FilterGuidsFile(string sourcePath, string destPath, string carName)
    {
        var lines    = File.ReadAllLines(sourcePath);
        var filtered = new List<string>();

        foreach (string line in lines)
        {
            // Keep lines that mention the car name (case-insensitive)
            if (line.IndexOf(carName, StringComparison.OrdinalIgnoreCase) >= 0)
            {
                filtered.Add(line);
                continue;
            }

            // Keep all "bus" lines (e.g. mixer groups like bus:/ or mixerBus:/)
            if (line.IndexOf("bus", StringComparison.OrdinalIgnoreCase) >= 0)
            {
                filtered.Add(line);
            }
        }

        File.WriteAllLines(destPath, filtered);
    }

    private static string? FindFile(string directory, string fileName)
    {
        var files = Directory.GetFiles(directory, fileName, SearchOption.AllDirectories);
        return files.Length > 0 ? files[0] : null;
    }

    private static string? FindBankFile(string directory, string carName)
    {
        var files = Directory.GetFiles(directory, "*.bank", SearchOption.AllDirectories);
        string? fallback = null;

        foreach (string file in files)
        {
            string name = Path.GetFileName(file);
            if (name.Equals("Master Bank.strings.bank", StringComparison.OrdinalIgnoreCase) ||
                name.EndsWith(".strings.bank", StringComparison.OrdinalIgnoreCase))
                continue;

            if (name.Equals($"{carName}.bank", StringComparison.OrdinalIgnoreCase))
                return file; // Exact match found!
            
            // If it's a default Master bank or any other bank, keep it as fallback
            if (fallback == null)
                fallback = file;
        }

        return fallback;
    }
}
