// FMOD Studio 1.08.12 Compatible Script
// Minimal automation: import WAVs and create events in root

studio.menu.addMenuItem({
    name: "Scripts\\Import Recordings to Events (FMOD 1.08)",
    execute: function () {

        var projectPath = studio.project.filePath;
        var projectDir = projectPath.substring(0, projectPath.lastIndexOf("/") + 1);
        var recordingsDir = projectDir + "Recordings";

        if (!studio.system.getFile(recordingsDir).exists()) {
            alert("Error: Recordings/ folder not found at: " + recordingsDir);
            return;
        }

        // Walk directory for WAV files
        function walk(dir) {
            var out = [];
            var list;
            try {
                list = studio.system.getFile(dir).list();
            } catch (e) {
                return out;
            }
            for (var i = 0; i < list.length; i++) {
                var full = dir + "/" + list[i];
                var f = studio.system.getFile(full);
                if (f.isDirectory()) {
                    out = out.concat(walk(full));
                } else {
                    out.push(full);
                }
            }
            return out;
        }

        var allFiles = walk(recordingsDir);
        var wavFiles = [];
        for (var i = 0; i < allFiles.length; i++) {
            var fileLower = allFiles[i].toLowerCase();
            if (fileLower.substring(fileLower.length - 4) === ".wav") {
                wavFiles.push(allFiles[i]);
            }
        }

        if (wavFiles.length === 0) {
            alert("No .wav files found.");
            return;
        }

        var imported = 0;
        var failed = 0;

        for (var i = 0; i < wavFiles.length; i++) {
            var filePath = wavFiles[i];
            var fileName = filePath.substring(filePath.lastIndexOf("/") + 1);
            var eventName = fileName.substring(0, fileName.lastIndexOf("."));

            // Import audio
            var audioFile = null;
            try {
                audioFile = studio.project.importAudioFile(filePath);
            } catch (e) {
                failed++;
                continue;
            }
            if (!audioFile) {
                failed++;
                continue;
            }

            // Create event (FMOD 1.08: no folders, no tracks)
            var evt = studio.project.createEvent(eventName);

            // Assign audio to event's default sound slot
            evt.sound = audioFile;

            imported++;
        }

        studio.project.save();

        alert(
            "FMOD 1.08 Import Complete\n" +
            "Events created: " + imported + "\n" +
            "Failed imports: " + failed
        );
    }
});
