using System.Text.Json.Serialization;

namespace DWMB.Core.Api.ApiObjects
{
    public sealed class RegisterRequest
    {
        [JsonPropertyName("token")]
        public string Token { get; set; } = string.Empty;

        [JsonPropertyName("callsign")]
        public string Callsign { get; set; } = string.Empty;

        [JsonPropertyName("capture_method")]
        public string CaptureMethod { get; set; } = string.Empty;

        [JsonPropertyName("client_name")]
        public string ClientName { get; set; } = string.Empty;

        [JsonPropertyName("client_version")]
        public string ClientVersion { get; set; } = string.Empty;
    }

    public sealed class RegisterResponse
    {
        [JsonPropertyName("token")]
        public string Token { get; set; } = string.Empty;

        [JsonPropertyName("discord_id")]
        public long DiscordId { get; set; }

        [JsonPropertyName("discord_name")]
        public string? DiscordName { get; set; }

        [JsonPropertyName("callsign")]
        public string? Callsign { get; set; }

        [JsonPropertyName("notify_channel")]
        public string NotifyChannel { get; set; } = string.Empty;

        [JsonPropertyName("server_min_client_version")]
        public string ServerMinClientVersion { get; set; } = string.Empty;
    }
}
