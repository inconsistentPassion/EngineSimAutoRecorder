using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;

namespace EngineSimRecorder.Services;

public enum FmodGenerationMode
{
    UseExistingTemplate,
    FromScratch
}

public class FmodScriptGenerator
{
    public FmodGenerationMode Mode { get; set; } = FmodGenerationMode.UseExistingTemplate;

    /// <summary>
    /// The name of the FMOD event/folder to use as a template (e.g. "tatuusfa1").
    /// Only used in FromScratch mode if the template exists in the project.
    /// </summary>
    public string TemplateEventName { get; set; } = "tatuusfa1";

    /// <summary>
    /// The car folder name — used both for FMOD event naming and the Assets sub-folder.
    /// e.g. "Tatuus"
    /// </summary>
    public string CarName { get; set; } = string.Empty;

    /// <summary>
    /// Path to the FMOD Studio .fspro project folder.
    /// e.g. C:\Users\HORACE\Desktop\audio\ac_fmod_sdk_1_9
    /// When set the generator will:
    ///   1. Scan Assets\{Vendor}\{CarName}\ to auto-discover Engine EXT / Engine INT / Accessories.
    ///   2. Copy any user-supplied recordings into the correct sub-folders before generating JS.
    /// </summary>
    public string FmodProjectPath { get; set; } = string.Empty;

    /// <summary>
    /// Vendor folder inside Assets (e.g. "Abarth").
    /// Auto-detected from FmodProjectPath + CarName when left empty.
    /// </summary>
    public string VendorName { get; set; } = string.Empty;

    // ── Optional user-supplied source recording dirs ─────────────────────────
    // Leave empty to rely solely on existing SDK asset files.

    /// <summary>Exterior recordings to copy into Engine EXT (optional).</summary>
    public string RecordingsDirExt { get; set; } = string.Empty;

    /// <summary>Interior recordings to copy into Engine INT (optional).</summary>
    public string RecordingsDirInt { get; set; } = string.Empty;

    /// <summary>Limiter recording to copy into Accessories (optional, falls back to RecordingsDirExt).</summary>
    public string RecordingsDirLimiter { get; set; } = string.Empty;

    // Backwards-compat alias
    public string RecordingsDir
    {
        get => RecordingsDirExt;
        set => RecordingsDirExt = value;
    }

    // ── Public entry point ────────────────────────────────────────────────────

    public string GenerateScript()
    {
        // Resolve the SDK asset base: Assets\{Vendor}\{CarName}
        string? sdkBase = ResolveAssetBase();

        if (sdkBase != null)
        {
            // Step 1: copy user recordings into the correct SDK sub-folders
            CopyWavsToSdk(sdkBase);

            // Step 2: read WAVs directly from SDK sub-folders
            string sdkExt = Path.Combine(sdkBase, "Engine EXT");
            string sdkInt = Path.Combine(sdkBase, "Engine INT");
            string sdkAcc = Path.Combine(sdkBase, "Accessories");

            var extWavs    = GetAudioFiles(sdkExt);
            var intWavs    = GetAudioFiles(sdkInt);
            var limiterWav = FindLimiterFileIn(sdkAcc);

            // Fall back to user dirs if SDK folders are empty / missing
            if (extWavs.Count == 0) extWavs    = GetAudioFiles(RecordingsDirExt);
            if (intWavs.Count == 0) intWavs    = GetAudioFiles(RecordingsDirInt);
            if (limiterWav == null) limiterWav  = FindLimiterFile();

            return BuildScript(extWavs, intWavs, limiterWav);
        }

        // No SDK path — use the user-supplied dirs directly
        return BuildScript(
            GetAudioFiles(RecordingsDirExt),
            GetAudioFiles(RecordingsDirInt),
            FindLimiterFile());
    }

    /// <summary>
    /// Copies user-supplied WAVs into the correct SDK Assets sub-folders.
    /// Returns a log of what was copied (also called internally by GenerateScript).
    /// </summary>
    public List<string> CopyWavsToSdk()
    {
        string? sdkBase = ResolveAssetBase();
        return sdkBase != null ? CopyWavsToSdk(sdkBase) : new List<string>();
    }

    // ── Internal helpers ──────────────────────────────────────────────────────

    /// <summary>
    /// Finds Assets\{Vendor}\{CarName} inside FmodProjectPath.
    /// If VendorName is set, uses it directly.
    /// Otherwise scans Assets\ for any sub-folder that itself contains a folder named CarName.
    /// Returns null if FmodProjectPath is not set or the car folder cannot be found.
    /// </summary>
    private string? ResolveAssetBase()
    {
        if (string.IsNullOrWhiteSpace(FmodProjectPath) || !Directory.Exists(FmodProjectPath))
            return null;
        if (string.IsNullOrWhiteSpace(CarName))
            return null;

        string assetsRoot = Path.Combine(FmodProjectPath, "Assets");
        if (!Directory.Exists(assetsRoot))
            return null;

        // If vendor is explicitly set, trust it
        if (!string.IsNullOrWhiteSpace(VendorName))
        {
            string explicit_ = Path.Combine(assetsRoot, VendorName, CarName);
            return explicit_; // return even if not yet created — CopyWavsToSdk will mkdir
        }

        // Auto-detect: walk one level of vendor folders and look for CarName
        foreach (string vendorDir in Directory.GetDirectories(assetsRoot))
        {
            string candidate = Path.Combine(vendorDir, CarName);
            if (Directory.Exists(candidate))
            {
                // Cache the vendor name so subsequent calls are consistent
                VendorName = Path.GetFileName(vendorDir);
                return candidate;
            }
        }

        // Car folder doesn't exist yet — use first vendor dir, or "Custom"
        string[] vendors = Directory.GetDirectories(assetsRoot);
        string vendor = vendors.Length > 0 ? Path.GetFileName(vendors[0]) : "Custom";
        VendorName = vendor;
        return Path.Combine(assetsRoot, vendor, CarName);
    }

    private List<string> CopyWavsToSdk(string sdkBase)
    {
        var log = new List<string>();

        void CopyDir(string srcDir, string subFolder)
        {
            if (string.IsNullOrWhiteSpace(srcDir) || !Directory.Exists(srcDir)) return;
            string destDir = Path.Combine(sdkBase, subFolder);
            Directory.CreateDirectory(destDir);
            foreach (string f in Directory.GetFiles(srcDir, "*.wav"))
            {
                string dest = Path.Combine(destDir, Path.GetFileName(f));
                File.Copy(f, dest, overwrite: true);
                log.Add($"Copied {Path.GetFileName(f)} → {subFolder}/");
            }
        }

        CopyDir(RecordingsDirExt, "Engine EXT");
        CopyDir(RecordingsDirInt, "Engine INT");

        // Limiter → Accessories (only limiter-named files)
        string limiterSrc = !string.IsNullOrWhiteSpace(RecordingsDirLimiter) && Directory.Exists(RecordingsDirLimiter)
            ? RecordingsDirLimiter
            : RecordingsDirExt;

        if (!string.IsNullOrWhiteSpace(limiterSrc) && Directory.Exists(limiterSrc))
        {
            string accDir = Path.Combine(sdkBase, "Accessories");
            Directory.CreateDirectory(accDir);
            foreach (string f in Directory.GetFiles(limiterSrc, "*.wav"))
            {
                if (Path.GetFileNameWithoutExtension(f).IndexOf("limiter", StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    string dest = Path.Combine(accDir, Path.GetFileName(f));
                    File.Copy(f, dest, overwrite: true);
                    log.Add($"Copied {Path.GetFileName(f)} → Accessories/");
                }
            }
        }

        return log;
    }

    private string BuildScript(List<AudioRecord> extWavs, List<AudioRecord> intWavs, string? limiterWav)
    {
        var sb = new StringBuilder();
        sb.AppendLine("// Auto-generated FMOD 1.08 ES3 Script by EngineSimAutoRecorder");
        sb.AppendLine("var project = studio.project;");
        sb.AppendLine("var system = studio.system;");
        sb.AppendLine("var carName = '" + CarName + "';");
        sb.AppendLine("var templateName = '" + TemplateEventName + "';");
        sb.AppendLine();

        EmitWavArray(sb, "extWavPaths", extWavs);
        EmitWavArray(sb, "intWavPaths", intWavs);

        string limiterEscaped = limiterWav != null ? limiterWav.Replace("\\", "/") : "";
        sb.AppendLine("var limiterPath = '" + limiterEscaped + "';");
        sb.AppendLine();

        GenerateHelpers(sb);

        if (Mode == FmodGenerationMode.FromScratch)
            GenerateFromScratch(sb);
        else
            GenerateUpdateTemplate(sb);

        sb.AppendLine("project.save();");
        sb.AppendLine("project.build();");
        sb.AppendLine("\"success\";");

        return sb.ToString();
    }

    // ── Audio file scanning ───────────────────────────────────────────────────

    private static void EmitWavArray(StringBuilder sb, string varName, List<AudioRecord> wavs)
    {
        sb.AppendLine("var " + varName + " = [");
        foreach (var af in wavs)
        {
            string p    = af.Path.Replace("\\", "/");
            string load = af.IsOnLoad ? "true" : "false";
            sb.AppendLine("    { path: \"" + p + "\", rpm: " + af.Rpm + ", isOn: " + load + " },");
        }
        sb.AppendLine("];");
        sb.AppendLine();
    }

    private List<AudioRecord> GetAudioFiles(string dir)
    {
        var list = new List<AudioRecord>();
        if (string.IsNullOrWhiteSpace(dir) || !Directory.Exists(dir))
            return list;

        foreach (string f in Directory.GetFiles(dir, "*.wav"))
        {
            string name = Path.GetFileNameWithoutExtension(f);
            if (name.IndexOf("limiter", StringComparison.OrdinalIgnoreCase) >= 0)
                continue;

            var match = Regex.Match(name, @"(?:.*_)?(on|off)_(\d+)$", RegexOptions.IgnoreCase);
            if (match.Success)
            {
                bool isOn = string.Equals(match.Groups[1].Value, "on", StringComparison.OrdinalIgnoreCase);
                if (int.TryParse(match.Groups[2].Value, out int rpm))
                    list.Add(new AudioRecord { Path = f, Rpm = rpm, IsOnLoad = isOn });
            }
            else
            {
                var fallback = Regex.Match(name, @"(?:.*_)?(\d+)$");
                if (fallback.Success && int.TryParse(fallback.Groups[1].Value, out int rpm2))
                    list.Add(new AudioRecord { Path = f, Rpm = rpm2, IsOnLoad = true });
            }
        }
        return list.OrderBy(x => x.Rpm).ToList();
    }

    /// <summary>Search for a limiter WAV in the given directory only.</summary>
    private static string? FindLimiterFileIn(string dir)
    {
        if (string.IsNullOrWhiteSpace(dir) || !Directory.Exists(dir))
            return null;
        return Directory.GetFiles(dir, "*.wav")
            .FirstOrDefault(f => Path.GetFileNameWithoutExtension(f)
                .IndexOf("limiter", StringComparison.OrdinalIgnoreCase) >= 0);
    }

    /// <summary>Fallback: search user-supplied dirs for a limiter WAV.</summary>
    private string? FindLimiterFile()
    {
        var dirs = new[] { RecordingsDirLimiter, RecordingsDirExt, RecordingsDirInt }
                   .Where(d => !string.IsNullOrWhiteSpace(d) && Directory.Exists(d));
        foreach (string dir in dirs)
        {
            string? f = FindLimiterFileIn(dir);
            if (f != null) return f;
        }
        return null;
    }

    // ── Shared JS Helpers ─────────────────────────────────────────────────────

    private void GenerateHelpers(StringBuilder sb)
    {
        sb.AppendLine(@"
// ── Common Helpers ────────────────────────────────────────────────────────
function getOrCreateFolder(parent, folderName) {
    if (!parent) return null;
    var items = parent.items;
    for (var i = 0; i < items.length; i++) {
        if (items[i].isOfType('EventFolder') && items[i].name === folderName)
            return items[i];
    }
    var f = project.create('EventFolder');
    f.name = folderName;
    f.folder = parent;
    return f;
}

function findByName(folder, typeName, name) {
    if (!folder) return null;
    var items = folder.items;
    for (var i = 0; i < items.length; i++) {
        var item = items[i];
        if (item.isOfType(typeName) && item.name === name) return item;
        if (item.isOfType('EventFolder')) {
            var found = findByName(item, typeName, name);
            if (found) return found;
        }
    }
    return null;
}

function findFolderByName(name) {
    function walk(folder) {
        if (!folder) return null;
        var items = folder.items;
        for (var i = 0; i < items.length; i++) {
            if (items[i].isOfType('EventFolder') && items[i].name === name) return items[i];
            if (items[i].isOfType('EventFolder')) { var r = walk(items[i]); if (r) return r; }
        }
        return null;
    }
    return walk(project.workspace.masterEventFolder);
}

function getOrCreateGroupTrack(evt, trackName) {
    // FMOD 1.08: groupTracks lives under relationships
    var rel = evt.relationships;
    if (rel && rel.groupTracks) {
        var items = rel.groupTracks;
        for (var i = 0; i < items.length; i++) {
            if (items[i] && items[i].mixerGroup && items[i].mixerGroup.name === trackName)
                return items[i];
        }
    }
    var t = project.create('GroupTrack');
    t.mixerGroup.output = evt.mixer.masterBus;
    t.mixerGroup.name = trackName;
    evt.relationships.groupTracks.add(t);
    return t;
}

function addSoundToTrack(track, snd) {
    // FMOD 1.08: modules is the correct relationship for SingleSound on GroupTrack
    if (track.relationships && track.relationships.modules) {
        track.relationships.modules.add(snd);
    } else if (track.relationships && track.relationships.sounds) {
        track.relationships.sounds.add(snd);
    }
}

function clearTrack(track) {
    // Remove all existing SingleSound modules so we can replace with new recordings
    if (!track || !track.relationships || !track.relationships.modules) return;
    var mods = track.relationships.modules;
    var toRemove = [];
    for (var i = 0; i < mods.length; i++) {
        toRemove.push(mods[i]);
    }
    for (var j = 0; j < toRemove.length; j++) {
        if (toRemove[j] && toRemove[j].isOfType('SingleSound')) {
            mods.remove(toRemove[j]);
        }
    }
}

function buildSmoothBand(evt, wavPaths) {
    if (!evt || wavPaths.length === 0) return;

    // Ensure rpms parameter exists
    var param = null;
    var params = evt.parameters;
    for (var i = 0; i < params.length; i++) {
        if (params[i].name === 'rpms') { param = params[i]; break; }
    }
    if (!param) {
        param = evt.addGameParameter({ name: 'rpms', type: 0, minimum: 0, maximum: 10000 });
    }

    var onWavs = [];
    var offWavs = [];
    for (var i = 0; i < wavPaths.length; i++) {
        if (wavPaths[i].isOn) onWavs.push(wavPaths[i]); else offWavs.push(wavPaths[i]);
    }

    function processTrack(trackName, wavList) {
        if (wavList.length === 0) return;
        var track = getOrCreateGroupTrack(evt, trackName);
        clearTrack(track);  // remove old recordings before placing new ones
        wavList.sort(function(a, b) { return a.rpm - b.rpm; });

        for (var i = 0; i < wavList.length; i++) {
            var currentRpm = wavList[i].rpm;
            var prevRpm    = (i > 0) ? wavList[i-1].rpm : 0;
            var nextRpm    = (i < wavList.length - 1) ? wavList[i+1].rpm : 10000;
            var startPos   = (i === 0) ? 0 : prevRpm;
            var endPos     = (i === wavList.length - 1) ? 10000 : nextRpm;
            var len        = endPos - startPos;

            var audioObj = project.importAudioFile(wavList[i].path);
            if (!audioObj) continue;

            var snd = project.create('SingleSound');
            snd.audioFile = audioObj;
            snd.start = startPos;
            snd.length = len;
            snd.looping = true;
            addSoundToTrack(track, snd);

            // Autopitch modulator: FMOD 1.08 uses AutopitchModulator with root=RPM
            var ap = project.create('AutopitchModulator');
            ap.root = currentRpm;
            snd.relationships.modulators.add(ap);

            // Fade in: first clip has no fade-in, others fade in from prevRpm
            if (i > 0 && snd.relationships.fadeInCurve) {
                snd.relationships.fadeInCurve.shape = 1;
            }
            // Fade out: last clip has no fade-out, others fade out to nextRpm
            if (i < wavList.length - 1 && snd.relationships.fadeOutCurve) {
                snd.relationships.fadeOutCurve.shape = 1;
            }
        }
    }

    processTrack(carName + ' load',  onWavs);
    processTrack(carName + ' coast', offWavs);
}
");
    }

    // ── FromScratch JS ────────────────────────────────────────────────────────

    private void GenerateFromScratch(StringBuilder sb)
    {
        sb.AppendLine(@"
// ── Folder structure ──────────────────────────────────────────────────────
var rootFolder = project.workspace.masterEventFolder;
var carsFolder = getOrCreateFolder(rootFolder, 'cars');
var vendorName = '" + (string.IsNullOrWhiteSpace(VendorName) ? "Custom" : VendorName) + @"';

// ── Bank ──────────────────────────────────────────────────────────────────
var bank = findByName(project.workspace.masterBankFolder, 'Bank', carName);
if (!bank) {
    bank = project.create('Bank');
    bank.name = carName;
}

// ── Template Cloning ──────────────────────────────────────────────────────
var targetFolder = null;
var eventExt = null;
var eventInt = null;
var eventLimiter = null;

var templateFolder = findByName(carsFolder, 'EventFolder', templateName);
if (templateFolder) {
    // Manually copy the template folder (studio.window.actions not available in 1.08 TCP)
    targetFolder = getOrCreateFolder(carsFolder, carName);

    var srcItems = templateFolder.items;
    for (var i = 0; i < srcItems.length; i++) {
        if (srcItems[i].isOfType('Event')) {
            var srcEvt = srcItems[i];
            var newEvt = project.create('Event');
            newEvt.name = srcEvt.name;
            newEvt.folder = targetFolder;
            newEvt.relationships.banks.add(bank);

            if (srcEvt.name === 'engine_ext') eventExt = newEvt;
            if (srcEvt.name === 'engine_int') eventInt = newEvt;
            if (srcEvt.name === 'limiter')    eventLimiter = newEvt;
        }
    }
} else {
    // Generic Fallback
    var assetFolder  = getOrCreateFolder(rootFolder,   'Assets');
    var vendorFolder = getOrCreateFolder(assetFolder,  vendorName);
    var carAssetFolder = getOrCreateFolder(vendorFolder, carName);
    var extAssetFolder = getOrCreateFolder(carAssetFolder, 'Engine EXT');
    var intAssetFolder = getOrCreateFolder(carAssetFolder, 'Engine INT');
    var accAssetFolder = getOrCreateFolder(carAssetFolder, 'Accessories');

    targetFolder = getOrCreateFolder(carsFolder, carName);
    
    eventExt = project.create('Event');
    eventExt.name = 'engine_ext';
    eventExt.folder = targetFolder;
    eventExt.relationships.banks.add(bank);

    eventInt = project.create('Event');
    eventInt.name = 'engine_int';
    eventInt.folder = targetFolder;
    eventInt.relationships.banks.add(bank);
}

// ── Apply recordings ──────────────────────────────────────────────────────
if (eventExt) buildSmoothBand(eventExt, extWavPaths);
if (eventInt) buildSmoothBand(eventInt, intWavPaths);

if (limiterPath !== '') {
    if (!eventLimiter) {
        eventLimiter = project.create('Event');
        eventLimiter.name = 'limiter';
        eventLimiter.folder = targetFolder;
        eventLimiter.relationships.banks.add(bank);
    }
    
    var limAudio = project.importAudioFile(limiterPath);
    if (limAudio) {
        var limTrack = getOrCreateGroupTrack(eventLimiter, 'limiter');
        var limSnd = project.create('SingleSound');
        limSnd.audioFile = limAudio;
        limSnd.start = 0;
        limSnd.length = 10000;
        limSnd.looping = false;
        addSoundToTrack(limTrack, limSnd);
    }
}
");
    }

    // ── UpdateTemplate JS ─────────────────────────────────────────────────────

    private void GenerateUpdateTemplate(StringBuilder sb)
    {
        sb.AppendLine(@"
// ── Locate events ─────────────────────────────────────────────────────────
var masterFolder = project.workspace.masterEventFolder;
var eventExt     = findByName(masterFolder, 'Event', 'engine_ext');
var eventInt     = findByName(masterFolder, 'Event', 'engine_int');
var eventLimiter = findByName(masterFolder, 'Event', 'engine_limiter');

buildSmoothBand(eventExt, extWavPaths);
buildSmoothBand(eventInt, intWavPaths);

// ── Limiter ───────────────────────────────────────────────────────────────
if (limiterPath !== '') {
    if (!eventLimiter) {
        var accFolder = findFolderByName('Accessories');
        if (accFolder) {
            eventLimiter = project.create('Event');
            eventLimiter.name = 'engine_limiter';
            eventLimiter.folder = accFolder;
            if (eventExt && eventExt.relationships.banks.length > 0) {
                eventLimiter.relationships.banks.add(eventExt.relationships.banks[0]);
            }
        }
    }
    if (eventLimiter) {
        var limAudio = project.importAudioFile(limiterPath);
        if (limAudio) {
            var limTrack = getOrCreateGroupTrack(eventLimiter, 'limiter');
            var limSnd = project.create('SingleSound');
            limSnd.audioFile = limAudio;
            limSnd.start = 0;
            limSnd.length = 10000;
            limSnd.looping = false;
            addSoundToTrack(limTrack, limSnd);
        }
    }
}
");
    }

    private class AudioRecord
    {
        public string Path { get; set; } = "";
        public int Rpm { get; set; }
        public bool IsOnLoad { get; set; }
    }
}
