using System;
using System.IO;
using System.Reflection;
using System.Text.Json;
using DWMB.Core.Api;

namespace DWMB.Core.Config
{
    /// <summary>
    /// Loads dwmb.config.json from next to the *host's* executing assembly (a plugin
    /// DLL, or the npcap exe) -- not the current working directory. v1's ApiManager
    /// read its server URL via `File.ReadAllText("server_location.txt")` as a field
    /// initializer, a CWD-relative path re-read on every construction; that is dead on
    /// arrival inside a plugin DLL whose working directory is the host application's,
    /// not the DLL's own directory. This is the fix: resolve relative to
    /// Assembly.GetExecutingAssembly().Location, and load once, explicitly.
    ///
    /// Only `Server` is required here. `Token` is optional at the config-file level
    /// because the two plugin adapters and the npcap fallback source it differently:
    /// plugins have no other way to get a token, so they must check
    /// `config.Token` themselves and refuse to start if it's blank; the npcap
    /// fallback deliberately keeps its v1 UI callsign+token entry (per the v2 plan)
    /// rather than persisting a token to disk, and overwrites `Token` on the loaded
    /// config with the user-typed value before constructing DwmbApiClient.
    /// </summary>
    public sealed class DwmbConfig
    {
        public string Server { get; set; } = string.Empty;
        public string Token { get; set; } = string.Empty;

        public static DwmbConfig Load(string? baseDirectory = null)
        {
            string dir = baseDirectory ?? Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location) ?? ".";
            string path = Path.Combine(dir, "dwmb.config.json");

            if (!File.Exists(path))
            {
                throw new DwmbApiException($"dwmb.config.json not found next to the plugin DLL at '{dir}'.");
            }

            string json;
            try
            {
                json = File.ReadAllText(path);
            }
            catch (Exception ex)
            {
                throw new DwmbApiException($"Could not read dwmb.config.json at '{path}'.", ex);
            }

            DwmbConfig? config;
            try
            {
                config = JsonSerializer.Deserialize<DwmbConfig>(json, new JsonSerializerOptions
                {
                    PropertyNameCaseInsensitive = true,
                });
            }
            catch (JsonException ex)
            {
                throw new DwmbApiException($"dwmb.config.json at '{path}' is malformed.", ex);
            }

            if (config == null || string.IsNullOrWhiteSpace(config.Server))
            {
                throw new DwmbApiException($"dwmb.config.json at '{path}' must contain a non-empty 'server' field.");
            }

            return config;
        }
    }
}
