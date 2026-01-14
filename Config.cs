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
        public int ClutchAxis { get; set; } = 2; // Default to Z-axis
        public int BrakeAxis { get; set; } = 1; // Default to Y-axis
        public int ThrottleAxis { get; set; } = 0; // Default to X-axis
        public int TraceSeconds { get; set; } = 10;
        public Dictionary<string, string> Colors { get; set; } = new Dictionary<string, string>
        {
            { "clutch", "#FF6B6B" },
            { "brake", "#4ECDC4" },
            { "throttle", "#95E1D3" }
        };

        private static string ConfigPath => Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
            ".pedal_telemetry_config.json"
        );

        public static Config Load()
        {
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
