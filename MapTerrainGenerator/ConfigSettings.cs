using System;
using System.IO;
using System.Text.Json;

namespace MapTerrainGeneratorWPF
{
    public class ConfigSettings
    {
        public string OutputFolder { get; set; } = AppDomain.CurrentDomain.BaseDirectory;
        public string GameDataPath { get; set; } = ""; 
        public int DefaultTargetMode { get; set; } = 0;
        public int DefaultNoiseType { get; set; } = 0;
        public string DefaultTexture { get; set; } = "common/caulk"; 
        public List<string> FavoriteTextures { get; set; } = new List<string>();

        private static readonly string ConfigPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "config.json");

        public static ConfigSettings Load()
        {
            if (!File.Exists(ConfigPath)) return new ConfigSettings();
            try { return JsonSerializer.Deserialize<ConfigSettings>(File.ReadAllText(ConfigPath)) ?? new ConfigSettings(); }
            catch { return new ConfigSettings(); }
        }

        public void Save() => File.WriteAllText(ConfigPath, JsonSerializer.Serialize(this, new JsonSerializerOptions { WriteIndented = true }));
    }
}