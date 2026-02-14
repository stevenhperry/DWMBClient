using System.Text.Json.Serialization;

namespace DWMB_AIO.DWMB.Serialization.ApiObjects
{
    /// <summary>
    /// Represents a request to forward messages, including authentication and message details.
    /// </summary>
    class MessageForwardRequest
    {
        /// <summary>
        /// Gets or sets the authentication token for the request.
        /// </summary>
        [JsonPropertyName("token")]
        public string Token { get; set; }

        /// <summary>
        /// Gets or sets the Discord user ID associated with the request.
        /// </summary>
        [JsonPropertyName("discord_id")]
        public long DiscordId { get; set; }

        /// <summary>
        /// Gets or sets the list of messages to be forwarded.
        /// </summary>
        [JsonPropertyName("messages")]
        public List<TextMessage> Messages { get; set; }

        /// <summary>
        /// Initializes a new instance of the <see cref="MessageForwardRequest"/> class with the specified token and Discord ID.
        /// </summary>
        /// <param name="token">The authentication token.</param>
        /// <param name="discordID">The Discord user ID.</param>
        public MessageForwardRequest(string token, long discordID)
        {
            this.Token = token;
            this.DiscordId = discordID;
        }

        /// <summary>
        /// Appends a message to the Messages list.
        /// (Method not yet implemented.)
        /// </summary>
        public void AppendMessage()
        {

        }

    }

    /// <summary>
    /// Represents a single text message to be forwarded.
    /// </summary>
    class TextMessage
    {
        /// <summary>
        /// Gets or sets the timestamp of the message.
        /// </summary>
        [JsonPropertyName("timestamp")]
        required public string Timestamp { get; set; }

        /// <summary>
        /// Gets or sets the sender of the message.
        /// </summary>
        [JsonPropertyName("sender")]
        required public string Sender { get; set; }

        /// <summary>
        /// Gets or sets the recipient of the message.
        /// </summary>
        [JsonPropertyName("recipient")]
        required public string Recipient { get; set; }

        /// <summary>
        /// Gets or sets the message content.
        /// </summary>
        [JsonPropertyName("message")]
        required public string Message { get; set; }

    }
}
