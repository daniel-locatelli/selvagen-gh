using System;
using System.IO;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Selvagen.Core.Api
{
    /// <summary>
    /// Configuration settings for the Selvagen environment.
    /// Loaded from selvagen.config.json in the user's AppData folder, with compile-time defaults.
    /// </summary>
    public static class SelvagenConfig
    {
        private const string DefaultSupabaseUrl = "https://aqzfsrebvjkegvfexcut.supabase.co";
        private const string DefaultSupabaseAnonKey = "eyJhbGciOiJIUzI1NiIsInR5cCI6IkpXVCJ9.eyJpc3MiOiJzdXBhYmFzZSIsInJlZiI6ImFxemZzcmVidmprZWd2ZmV4Y3V0Iiwicm9sZSI6ImFub24iLCJpYXQiOjE3NzA5ODY2MjcsImV4cCI6MjA4NjU2MjYyN30.UO63GIMmVBtR9Nv7-a7-XNuYGeO3-p8q5M24nr2AWbk";
        private const string ConfigFileName = "selvagen.config.json";

        private static readonly Lazy<ConfigFile> _config = new Lazy<ConfigFile>(LoadConfig);

        public static string SupabaseUrl => _config.Value.SupabaseUrl;
        public static string SupabaseAnonKey => _config.Value.SupabaseAnonKey;

        /// <summary>
        /// Directory where the config file is stored.
        /// %APPDATA%/Selvagen on Windows, ~/.config/Selvagen on macOS/Linux.
        /// </summary>
        public static string ConfigDirectory
        {
            get
            {
                var appData = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
                return Path.Combine(appData, "Selvagen");
            }
        }

        public static string ConfigFilePath => Path.Combine(ConfigDirectory, ConfigFileName);

        private static ConfigFile LoadConfig()
        {
            try
            {
                var path = ConfigFilePath;
                if (File.Exists(path))
                {
                    var json = File.ReadAllText(path);
                    var cfg = JsonSerializer.Deserialize<ConfigFile>(json);
                    if (cfg != null
                        && !string.IsNullOrWhiteSpace(cfg.SupabaseUrl)
                        && !string.IsNullOrWhiteSpace(cfg.SupabaseAnonKey))
                    {
                        return cfg;
                    }
                }
            }
            catch
            {
                // Fall through to defaults
            }

            return new ConfigFile
            {
                SupabaseUrl = DefaultSupabaseUrl,
                SupabaseAnonKey = DefaultSupabaseAnonKey,
            };
        }

        private class ConfigFile
        {
            [JsonPropertyName("supabase_url")]
            public string SupabaseUrl { get; set; } = "";

            [JsonPropertyName("supabase_anon_key")]
            public string SupabaseAnonKey { get; set; } = "";
        }
    }
}
