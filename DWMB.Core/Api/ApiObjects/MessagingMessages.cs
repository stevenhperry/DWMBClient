using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace DWMB.Core.Api.ApiObjects
{
    public sealed class MessagingRequest
    {
        [JsonPropertyName("token")]
        public string Token { get; set; } = string.Empty;

        [JsonPropertyName("messages")]
        public List<RelayMessageDto> Messages { get; set; } = new();
    }

    public sealed class RelayMessageDto
    {
        [JsonPropertyName("timestamp")]
        public long Timestamp { get; set; }

        [JsonPropertyName("sender")]
        public string Sender { get; set; } = string.Empty;

        [JsonPropertyName("receiver")]
        public string Receiver { get; set; } = string.Empty;

        [JsonPropertyName("message")]
        public string Message { get; set; } = string.Empty;
    }
}
