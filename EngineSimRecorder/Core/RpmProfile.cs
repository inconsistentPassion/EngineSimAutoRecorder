using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;

namespace EngineSimRecorder.Core
{
    public sealed class RpmProfile
    {
        public string Name { get; set; } = "Untitled";
        public string CarName { get; set; } = "";
        public string Prefix { get; set; } = "";
        public string OutputDir { get; set; } = "recordings";
        public List<int> TargetRpms { get; set; } = new();
        public int SampleRate { get; set; } = 44100;
        public int Channels { get; set; } = 2;

        private static string ProfilesDir =>
            Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "profiles");

        public static string[] GetProfileNames()
        {
            try
            {
                if (!Directory.Exists(ProfilesDir)) return Array.Empty<string>();
                return Directory.GetFiles(ProfilesDir, "*.json")
                    .Select(f => Path.GetFileNameWithoutExtension(f))
                    .OrderBy(n => n)
                    .ToArray();
            }
            catch { return Array.Empty<string>(); }
        }

        public static RpmProfile? Load(string name)
        {
            try
            {
                string path = Path.Combine(ProfilesDir, name + ".json");
                if (!File.Exists(path)) return null;
                string json = File.ReadAllText(path);
                return JsonSerializer.Deserialize<RpmProfile>(json);
            }
            catch { return null; }
        }

        public void Save()
        {
            try
            {
                Directory.CreateDirectory(ProfilesDir);
                string path = Path.Combine(ProfilesDir, Name + ".json");
                var options = new JsonSerializerOptions { WriteIndented = true };
                string json = JsonSerializer.Serialize(this, options);
                File.WriteAllText(path, json);
            }
            catch { }
        }

        public static bool Delete(string name)
        {
            try
            {
                string path = Path.Combine(ProfilesDir, name + ".json");
                if (File.Exists(path)) { File.Delete(path); return true; }
            }
            catch { }
            return false;
        }
    }
}
