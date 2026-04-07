using System;
using System.IO;
using System.Text.Json;

namespace EngineSimRecorder.Core
{
    public sealed class AppSettings
    {
        public int SampleRate { get; set; } = 44100;
        public int Channels { get; set; } = 2; // 1=mono, 2=stereo
        public string LastProfile { get; set; } = "";
        public bool InteriorMode { get; set; } = false;
        public string CarType { get; set; } = "Sedan";
        public bool RecordLimiter { get; set; } = false;
        public bool GeneratePowerLut { get; set; } = false;

        private static string FilePath =>
            Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "settings.json");

        public static AppSettings Load()
        {
            try
            {
                if (File.Exists(FilePath))
                {
                    string json = File.ReadAllText(FilePath);
                    return JsonSerializer.Deserialize<AppSettings>(json) ?? new AppSettings();
                }
            }
            catch { }
            return new AppSettings();
        }

        public void Save()
        {
            try
            {
                var options = new JsonSerializerOptions { WriteIndented = true };
                string json = JsonSerializer.Serialize(this, options);
                File.WriteAllText(FilePath, json);
            }
            catch { }
        }
    }
}
