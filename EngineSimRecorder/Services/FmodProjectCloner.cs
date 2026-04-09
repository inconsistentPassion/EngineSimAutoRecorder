using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;

namespace EngineSimRecorder.Services
{
    /// <summary>
    /// Handles deep-cloning of FMOD 1.08 project folders at the file level.
    /// This is necessary because the FMOD 1.08 JS API lacks a clone/duplicate method.
    /// </summary>
    public class FmodProjectCloner
    {
        private readonly string _projectPath;
        private readonly string _metadataPath;

        public FmodProjectCloner(string projectPath)
        {
            _projectPath = projectPath;
            _metadataPath = Path.Combine(projectPath, "Metadata");
        }

        public string CloneFolder(string sourceName, string targetName)
        {
            if (!Directory.Exists(_metadataPath))
                throw new DirectoryNotFoundException("FMOD Metadata folder not found.");

            // 1. Locate the source folder XML
            string eventFolderDir = Path.Combine(_metadataPath, "EventFolder");
            string? sourceFile = Directory.GetFiles(eventFolderDir, "*.xml")
                .FirstOrDefault(f => File.ReadAllText(f).Contains($"<property name=\"name\"><value>{sourceName}</value></property>"));

            if (sourceFile == null)
                throw new Exception($"Template folder '{sourceName}' not found in FMOD project.");

            string sourceGuid = Path.GetFileNameWithoutExtension(sourceFile);
            
            // 2. Discover all nested objects (Events, Tracks, etc.) belonging to this folder tree
            var objectsToClone = new List<string> { sourceGuid };
            var guidMap = new Dictionary<string, string>();
            
            // Collect GUIDs recursively
            CollectFolderChildren(sourceGuid, objectsToClone);

            // 3. Create new GUIDs for every object
            foreach (var oldGuid in objectsToClone)
            {
                guidMap[oldGuid] = "{" + Guid.NewGuid().ToString().ToLower() + "}";
            }

            // 4. Perform the file-level copy and GUID replacement
            foreach (var oldGuid in objectsToClone)
            {
                CloneObjectFile(oldGuid, guidMap, targetName, sourceName);
            }

            // 5. Update Parent relationship so the folder actually shows up
            UpdateParentRelationship(sourceGuid, guidMap[sourceGuid]);

            return guidMap[sourceGuid];
        }

        private void UpdateParentRelationship(string sourceGuid, string newGuid)
        {
            // Find which file contains the parent relationship to the source folder
            foreach (var dir in Directory.GetDirectories(_metadataPath))
            {
                foreach (var file in Directory.GetFiles(dir, "*.xml"))
                {
                    string content = File.ReadAllText(file);
                    if (content.Contains($"<destination>{sourceGuid}</destination>"))
                    {
                        // Found the parent! (likely a Folder or Workspace)
                        // Add the new GUID to the same relationship list
                        string marker = $"<destination>{sourceGuid}</destination>";
                        string replacement = marker + "\n            " + $"<destination>{newGuid}</destination>";
                        
                        // We only want to update the relationship list, not just any text
                        // but in simple FMOD XML, this is usually safe.
                        content = content.Replace(marker, replacement);
                        File.WriteAllText(file, content);
                        return;
                    }
                }
            }
        }


        private void CollectFolderChildren(string folderGuid, List<string> collected)
        {
            // Find all events and subfolders that have this folder as their parent
            // In FMOD 1.08, relationships are in XML entries like <relationship name="folder"><destination>{rootGuid}</destination></relationship>
            
            string[] subTypes = { "Event", "EventFolder" };
            foreach (var type in subTypes)
            {
                string dir = Path.Combine(_metadataPath, type);
                if (!Directory.Exists(dir)) continue;

                foreach (var file in Directory.GetFiles(dir, "*.xml"))
                {
                    string content = File.ReadAllText(file);
                    if (content.Contains($"<destination>{folderGuid}</destination>"))
                    {
                        string childGuid = Path.GetFileNameWithoutExtension(file);
                        if (!collected.Contains(childGuid))
                        {
                            collected.Add(childGuid);
                            if (type == "EventFolder") CollectFolderChildren(childGuid, collected);
                            else CollectEventChildren(childGuid, collected);
                        }
                    }
                }
            }
        }

        private void CollectEventChildren(string eventGuid, List<string> collected)
        {
            // Events have many sub-objects (GroupTracks, Parameters, Modulators, Automations)
            // They are usually stored in their own directories with a folder-based relationship
            
            string[] metadataTypes = Directory.GetDirectories(_metadataPath);
            foreach (var dir in metadataTypes)
            {
                string typeName = Path.GetFileName(dir);
                if (typeName == "Event" || typeName == "EventFolder") continue;

                foreach (var file in Directory.GetFiles(dir, "*.xml"))
                {
                    string content = File.ReadAllText(file);
                    // Match any destination that is the event itself
                    if (content.Contains($"<destination>{eventGuid}</destination>"))
                    {
                        string guid = Path.GetFileNameWithoutExtension(file);
                        if (!collected.Contains(guid))
                        {
                            collected.Add(guid);
                            // Recurse to find children of tracks, etc.
                            CollectEventChildren(guid, collected);
                        }
                    }
                }
            }
        }

        private void CloneObjectFile(string oldGuid, Dictionary<string, string> guidMap, string targetName, string sourceName)
        {
            // Find which folder this GUID lives in
            string? sourceFile = null;
            string? typeDir = null;
            
            foreach (var dir in Directory.GetDirectories(_metadataPath))
            {
                string candidate = Path.Combine(dir, oldGuid + ".xml");
                if (File.Exists(candidate))
                {
                    sourceFile = candidate;
                    typeDir = dir;
                    break;
                }
            }

            if (sourceFile == null) return;

            string content = File.ReadAllText(sourceFile);
            string newGuid = guidMap[oldGuid];

            // Replace the name if it's the root folder
            if (content.Contains($"<property name=\"name\"><value>{sourceName}</value></property>"))
            {
                content = content.Replace($"<value>{sourceName}</value>", $"<value>{targetName}</value>");
            }

            // Replace all mapped GUID references within the file content
            foreach (var mapping in guidMap)
            {
                content = content.Replace(mapping.Key, mapping.Value);
            }
            
            // Note: There might be GUIDs in the file that we haven't mapped yet 
            // (e.g. references to Master Bus). We only want to replace GUIDs that are PART of the clone.

            string destFile = Path.Combine(typeDir!, newGuid.Trim('{', '}') + ".xml");
            File.WriteAllText(destFile, content);
        }
    }
}
