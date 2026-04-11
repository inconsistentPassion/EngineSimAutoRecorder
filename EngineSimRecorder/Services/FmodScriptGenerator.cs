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
    /// The car folder name - used both for FMOD event naming and the Assets sub-folder.
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

    // -- Optional user-supplied source recording dirs -------------------------
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

    // -- Public entry point ----------------------------------------------------

    public string GenerateScript()
    {
        // Step 0: If in FromScratch mode, try to clone the template folder first (FMOD 1.08 fix)
        if (Mode == FmodGenerationMode.FromScratch && !string.IsNullOrWhiteSpace(FmodProjectPath))
        {
            try
            {
                // Check if the car folder already exists in Metadata
                if (GetCarFolderId() == null)
                {
                    var cloner = new FmodProjectCloner(FmodProjectPath);
                    cloner.CloneFolder(TemplateEventName, CarName);
                }
            }
            catch (Exception) { /* Fallback to JS creation if C# cloning fails */ }
        }

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

        // No SDK path - use the user-supplied dirs directly
        return BuildScript(
            GetAudioFiles(RecordingsDirExt),
            GetAudioFiles(RecordingsDirInt),
            FindLimiterFile());
    }

    private string? GetCarFolderId()
    {
        if (string.IsNullOrWhiteSpace(FmodProjectPath)) return null;
        string eventFolderDir = Path.Combine(FmodProjectPath, "Metadata", "EventFolder");
        if (!Directory.Exists(eventFolderDir)) return null;

        return Directory.GetFiles(eventFolderDir, "*.xml")
            .FirstOrDefault(f => File.ReadAllText(f).Contains($"<property name=\"name\"><value>{CarName}</value></property>"));
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

    // -- Internal helpers ------------------------------------------------------

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
            return explicit_; // return even if not yet created - CopyWavsToSdk will mkdir
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

        // Car folder doesn't exist yet - use first vendor dir, or "Custom"
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

    // -- Audio file scanning ---------------------------------------------------

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

    // -- Shared JS Helpers -----------------------------------------------------

    private void GenerateHelpers(StringBuilder sb)
    {
        sb.AppendLine(@"
// -- Common Helpers --
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

function findBestTrack(evt, kw1, kw2, fallbackName) {
    // FMOD 1.08: track items are directly on evt.groupTracks
    var items = evt.groupTracks;
    if (!items || items.length === 0) return createTrack(evt, fallbackName);
    
    // 1. Exact match on full track name
    for (var i = 0; i < items.length; i++) {
        var t = items[i];
        if (t && t.mixerGroup && t.mixerGroup.name === fallbackName)
            return t;
    }
    // 2. Fuzzy match - track name contains kw1 (""load"" / ""coast"") or kw2 (""on"" / ""off"")
    // We prioritize tracks that ALREADY HAVE automation/modulators if possible
    var k1 = kw1.toLowerCase();
    var k2 = kw2.toLowerCase();
    
    var candidates = [];
    for (var i = 0; i < items.length; i++) {
        var t = items[i];
        if (!t || !t.mixerGroup) continue;
        var name = t.mixerGroup.name.toLowerCase();
        if (name.indexOf(k1) !== -1 || name.indexOf(k2) !== -1) {
            candidates.push(t);
        }
    }

    if (candidates.length > 0) {
        // If multiple candidates, pick the one that likely belongs to the template
        // (usually the one created first or the one with existing volume automation)
        return candidates[0];
    }

    // 3. Last ditch: if there's only 2 tracks and we are looking for load/coast
    // and they are named something generic like ""Audio 1"", ""Audio 2""
    if (items.length >= 2) {
        if (kw1 === 'load') return items[0];
        if (kw1 === 'coast') return items[1];
    }

    // 4. Fallback to creation
    return createTrack(evt, fallbackName);
}

function createTrack(evt, name) {
    var t = project.create('GroupTrack');
    t.mixerGroup.output = evt.mixer.masterBus;
    t.mixerGroup.name = name;
    evt.relationships.groupTracks.add(t);
    return t;
}

function getOrCreateGroupTrack(evt, trackName) {
    // This is kept for backward compat if needed, but we prefer findBestTrack
    var rel = evt.relationships;
    if (rel && rel.groupTracks) {
        var items = rel.groupTracks;
        for (var i = 0; i < items.length; i++) {
            if (items[i] && items[i].mixerGroup && items[i].mixerGroup.name === trackName)
                return items[i];
        }
    }
    return createTrack(evt, trackName);
}

function bindSound(track, param, snd) {
    // FMOD 1.08 manual binding
    if (track.relationships && track.relationships.modules) {
        track.relationships.modules.add(snd);
    } else if (track.relationships && track.relationships.sounds) {
        track.relationships.sounds.add(snd);
    }
    
    // Bind to the parameter sheet (if this is on a parameter and not the main timeline)
    if (param) {
        if (param.relationships && param.relationships.modules) {
            param.relationships.modules.add(snd);
        } else if (param.relationships && param.relationships.sounds) {
            param.relationships.sounds.add(snd);
        }
    }
}

function addFadeCurve(snd, isFadeIn, startRpm, endRpm) {
    if (startRpm >= endRpm) return;
    var fade = project.create('FadeCurve');
    var p1 = project.create('AutomationPoint');
    p1.position = startRpm;
    p1.value = isFadeIn ? 0 : 1;
    // 0.7 approximates equal-power S-curve (was 0.45)
    p1.curveShape = isFadeIn ? -0.7 : 0.7;

    var p2 = project.create('AutomationPoint');
    p2.position = endRpm;
    p2.value = isFadeIn ? 1 : 0;
    
    fade.relationships.startPoint.add(p1);
    fade.relationships.endPoint.add(p2);
    
    if (isFadeIn) {
        snd.relationships.fadeInCurve.add(fade);
    } else {
        snd.relationships.fadeOutCurve.add(fade);
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
        if (toRemove[j] && (toRemove[j].isOfType('SingleSound') || toRemove[j].isOfType('MultiSound'))) {
            mods.remove(toRemove[j]);
        }
    }
}

function buildSmoothBand(evt, wavPaths) {
    if (!evt || wavPaths.length === 0) return;

    // Ensure rpms parameter exists (FMOD 1.08 compatible)
    var param = null;
    var params = evt.parameters.items || evt.parameters;
    if (params && params.length > 0) {
        for (var i = 0; i < params.length; i++) {
            var pName = params[i].name.toLowerCase();
            if (pName === 'rpms' || pName === 'rpm') { 
                param = params[i]; 
                break; 
            }
        }
    }

    if (!param) {
        // Manually create parameter if missing (addGameParameter is missing in some 1.08 builds)
        param = project.create('GameParameter');
        param.name = 'rpms';
        param.maximum = 10000;
        param.seekSpeed = 100000; // Adding smoothing to eliminate jerks
        evt.relationships.parameters.add(param);
    } else {
        // Apply smoothing to existing template parameter
        param.seekSpeed = 100000;
    }

    var onWavs = [];
    var offWavs = [];
    for (var i = 0; i < wavPaths.length; i++) {
        if (wavPaths[i].isOn) onWavs.push(wavPaths[i]); else offWavs.push(wavPaths[i]);
    }

    function processTrack(keyword1, keyword2, wavList) {
        if (wavList.length === 0) return;
        var defaultName = carName + ' ' + keyword1;
        var track = findBestTrack(evt, keyword1, keyword2, defaultName);
        clearTrack(track);  // remove old recordings before placing new ones
        wavList.sort(function(a, b) { return a.rpm - b.rpm; });

        for (var i = 0; i < wavList.length; i++) {
            var currentRpm = wavList[i].rpm;
            var prevRpm    = (i > 0) ? wavList[i-1].rpm : 0;
            var nextRpm    = (i < wavList.length - 1) ? wavList[i+1].rpm : 10000;
            
            // Widen overlap by 30% so adjacent clips always have content from both sides
            var inBuffer   = (currentRpm - prevRpm) * 0.3;
            var outBuffer  = (nextRpm - currentRpm) * 0.3;

            var startPos   = (i === 0) ? 0 : Math.max(0, prevRpm - inBuffer);
            var endPos     = (i === wavList.length - 1) ? 10000 : Math.min(10000, nextRpm + outBuffer);
            var len        = endPos - startPos;

            var audioObj = project.importAudioFile(wavList[i].path);
            if (!audioObj) continue;

            var snd = project.create('SingleSound');
            snd.audioFile = audioObj;
            snd.start = startPos;
            snd.length = len;
            snd.looping = true;
            bindSound(track, param, snd);

            // Autopitch modulator: FMOD 1.08 uses AutopitchModulator with root=RPM
            var ap = project.create('AutopitchModulator');
            ap.root = currentRpm;
            snd.relationships.modulators.add(ap);

            // Fade in: from prevRpm to currentRpm (with overlap buffer)
            if (i > 0) {
                addFadeCurve(snd, true, Math.max(0, prevRpm - inBuffer), currentRpm);
            }
            // Fade out: from currentRpm to nextRpm (with overlap buffer)
            if (i < wavList.length - 1) {
                addFadeCurve(snd, false, currentRpm, Math.min(10000, nextRpm + outBuffer));
            }
        }
    }

    processTrack('load', 'on', onWavs);
    processTrack('coast', 'off', offWavs);
}

");
    }

    // -- FromScratch JS --------------------------------------------------------

    private void GenerateFromScratch(StringBuilder sb)
    {
        sb.AppendLine(@"
// -- Folder & Bank ---------------------------------------------------------
var rootFolder = project.workspace.masterEventFolder;
var carsFolder = getOrCreateFolder(rootFolder, 'cars');
var targetFolder = getOrCreateFolder(carsFolder, carName);

var bank = findByName(project.workspace.masterBankFolder, 'Bank', carName);
if (!bank) {
    bank = project.create('Bank');
    bank.name = carName;
}

// -- Get or create engine events (idempotent) ------------------------------
function getOrCreateEvent(folder, name, bank) {
    var existing = findByName(folder, 'Event', name);
    if (existing) return existing;
    var evt = project.create('Event');
    evt.name = name;
    evt.folder = folder;
    evt.relationships.banks.add(bank);
    return evt;
}

var eventExt     = getOrCreateEvent(targetFolder, 'engine_ext', bank);
var eventInt     = getOrCreateEvent(targetFolder, 'engine_int', bank);
var eventLimiter = getOrCreateEvent(targetFolder, 'engine_limiter', bank);

// -- Apply recordings ------------------------------------------------------
buildSmoothBand(eventExt, extWavPaths);
buildSmoothBand(eventInt, intWavPaths);

if (limiterPath !== '') {
    var limAudio = project.importAudioFile(limiterPath);
    if (limAudio) {
        var limTrack = findBestTrack(eventLimiter, 'limiter', 'limiter', 'limiter');
        var limSnd = project.create('SingleSound');
        limSnd.audioFile = limAudio;
        limSnd.start = 0;
        limSnd.length = 10000;
        limSnd.looping = false;
        bindSound(limTrack, null, limSnd);
    }
}
");
    }

    // -- UpdateTemplate JS -----------------------------------------------------

    private void GenerateUpdateTemplate(StringBuilder sb)
    {
        sb.AppendLine(@"
// -- Locate car folder -----------------------------------------------------
var carsFolder = findFolderByName('cars');
var carFolder = findByName(carsFolder, 'EventFolder', carName);

if (!carFolder) {
    // If not in 'cars', fallback to master search
    carFolder = project.workspace.masterEventFolder;
}

// -- Locate events within that car's folder --------------------------------
var eventExt     = findByName(carFolder, 'Event', 'engine_ext');
var eventInt     = findByName(carFolder, 'Event', 'engine_int');
var eventLimiter = findByName(carFolder, 'Event', 'engine_limiter');

if (!eventExt || !eventInt) {
    // Final fallback: try global names if they aren't in the carFolder
    var master = project.workspace.masterEventFolder;
    if (!eventExt) eventExt = findByName(master, 'Event', 'engine_ext');
    if (!eventInt) eventInt = findByName(master, 'Event', 'engine_int');
}

buildSmoothBand(eventExt, extWavPaths);
buildSmoothBand(eventInt, intWavPaths);

// -- Limiter ---------------------------------------------------------------
if (limiterPath !== '') {
    if (!eventLimiter) {
        eventLimiter = findByName(carFolder, 'Event', 'engine_limiter');
    }
    if (eventLimiter) {
        var limAudio = project.importAudioFile(limiterPath);
        if (limAudio) {
            var limTrack = findBestTrack(eventLimiter, 'limiter', 'limiter', 'limiter');
            clearTrack(limTrack);
            var limSnd = project.create('SingleSound');
            limSnd.audioFile = limAudio;
            limSnd.start = 0;
            limSnd.length = 10000;
            limSnd.looping = false;
            bindSound(limTrack, null, limSnd);
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
