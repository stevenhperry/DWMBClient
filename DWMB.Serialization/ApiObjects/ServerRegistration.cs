using System.Text.Json.Serialization;

namespace DWMB_AIO.DWMB.Serialization.ApiObjects
{
    /// <summary>
    /// Represents the response received after registering a server.
    /// </summary>
    class ServerRegistrationResponse
    {
        /// <summary>
        /// Gets or sets the authentication token for the registered server.
        /// </summary>
        [JsonPropertyName("token")]
        public required string Token { get; set; }

        /// <summary>
        /// Gets or sets the server's callsign identifier.
        /// </summary>
        [JsonPropertyName("callsign")]
        public required string Callsign { get; set; }

        /// <summary>
        /// Gets or sets the Discord user ID associated with the server registration.
        /// </summary>
        [JsonPropertyName("discord_id")]
        public long DiscordId { get; set; }

        /// <summary>
        /// Gets or sets the Discord username associated with the server registration.
        /// </summary>
        [JsonPropertyName("discord_name")]
        public required string DiscordName { get; set; }
    }
}
