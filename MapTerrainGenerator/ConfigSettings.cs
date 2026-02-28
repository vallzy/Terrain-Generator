using System;
using System.IO;
using System.Text.Json;

namespace MapTerrainGeneratorWPF
{
    public class ConfigSettings
    {
        public string OutputFolder { get; set; } = AppDomain.CurrentDomain.BaseDirectory;
        public int DefaultTargetMode { get; set; } = 0; // 0: Hint, 1: Manual
        public int DefaultNoiseType { get; set; } = 0; // 0: Perlin, 1: Simplex, 2: Random

        private static readonly string ConfigPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "config.json");

        public static ConfigSettings Load()
        {
            if (!File.Exists(ConfigPath)) return new ConfigSettings();
            try
            {
                string json = File.ReadAllText(ConfigPath);
                return JsonSerializer.Deserialize<ConfigSettings>(json) ?? new ConfigSettings();
            }
            catch { return new ConfigSettings(); }
        }

        public void Save()
        {
            string json = JsonSerializer.Serialize(this, new JsonSerializerOptions { WriteIndented = true });
            File.WriteAllText(ConfigPath, json);
        }
    }
}