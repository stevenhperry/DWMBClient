using System.Text.Json;
using System.Text.Json.Serialization;
using DWMB_AIO.DWMB.Diagnostics;

namespace DWMB_AIO.DWMB.Settings
{
    /// <summary>
    /// Persisted, per-user application preferences. First settings infrastructure in the
    /// repo — deliberately a single flat JSON file rather than app.config/Properties.Settings
    /// so future preferences can just be added as properties here.
    /// </summary>
    class AppSettings
    {
        /// <summary>
        /// Settings file: settings.json under the current user's local (non-roaming) app
        /// data folder, alongside Logger's log.txt (same %LOCALAPPDATA%\DontWallopMeBro
        /// folder, writable without elevation from a Program Files-installed exe).
        /// </summary>
        private static readonly string DefaultSettingsPath = System.IO.Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "DontWallopMeBro", "settings.json");

        [JsonPropertyName("automaticallyCheckForUpdates")]
        public bool AutomaticallyCheckForUpdates { get; set; } = true;

        /// <summary>
        /// Loads settings from disk, falling back to defaults on any failure (missing
        /// file, corrupt JSON, permissions) — a broken settings file must never crash the
        /// app or block startup, just log and use defaults.
        /// </summary>
        public static AppSettings Load()
        {
            try
            {
                if (!System.IO.File.Exists(DefaultSettingsPath))
                {
                    return new AppSettings();
                }

                string json = System.IO.File.ReadAllText(DefaultSettingsPath);
                return JsonSerializer.Deserialize<AppSettings>(json) ?? new AppSettings();
            }
            catch (Exception ex)
            {
                new Logger().Log("[SETTINGS] Failed to load settings.json, using defaults: " + ex.Message);
                return new AppSettings();
            }
        }

        /// <summary>
        /// Saves settings to disk. Failures are logged, not thrown — a failed save must
        /// never surface a dialog to the user.
        /// </summary>
        public void Save()
        {
            try
            {
                string? dir = System.IO.Path.GetDirectoryName(DefaultSettingsPath);
                if (!string.IsNullOrEmpty(dir))
                {
                    System.IO.Directory.CreateDirectory(dir);
                }

                string json = JsonSerializer.Serialize(this, new JsonSerializerOptions { WriteIndented = true });
                System.IO.File.WriteAllText(DefaultSettingsPath, json);
            }
            catch (Exception ex)
            {
                new Logger().Log("[SETTINGS] Failed to save settings.json: " + ex.Message);
            }
        }
    }
}
