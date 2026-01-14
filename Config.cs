using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;

namespace PedalTelemetry
{
    public class Config
    {
        public int? ClutchHid { get; set; }
        public int? BrakeHid { get; set; }
        public int? ThrottleHid { get; set; }
        public int ClutchAxis { get; set; } = 5; // Default to Z Rotation (RotationZ)
        public int BrakeAxis { get; set; } = 4; // Default to Y Rotation (RotationY)
        public int ThrottleAxis { get; set; } = 3; // Default to X Rotation (RotationX)
        public int TraceSeconds { get; set; } = 8;
        public Dictionary<string, string> Colors { get; set; } = new Dictionary<string, string>
        {
            { "clutch", "#0085F1" },
            { "brake", "#D80404" },
            { "throttle", "#09B61A" }
        };

        private static string ConfigDirectory => Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments),
            "Pedal Input Telemetry Tool"
        );

        private static string ConfigPath => Path.Combine(
            ConfigDirectory,
            "config.json"
        );

        private static void EnsureConfigDirectoryExists()
        {
            if (!Directory.Exists(ConfigDirectory))
            {
                Directory.CreateDirectory(ConfigDirectory);
            }
        }

        public static Config Load()
        {
            EnsureConfigDirectoryExists();
            
            if (File.Exists(ConfigPath))
            {
                try
                {
                    var json = File.ReadAllText(ConfigPath);
                    var config = JsonSerializer.Deserialize<Config>(json);
                    return config ?? new Config();
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"Error loading config: {ex.Message}");
                    return new Config();
                }
            }
            return new Config();
        }

        public void Save()
        {
            try
            {
                EnsureConfigDirectoryExists();
                var options = new JsonSerializerOptions { WriteIndented = true };
                var json = JsonSerializer.Serialize(this, options);
                File.WriteAllText(ConfigPath, json);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error saving config: {ex.Message}");
            }
        }
    }
}
