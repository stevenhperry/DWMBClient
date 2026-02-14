namespace DWMB_AIO.DWMB.Serialization.ApiObjects
{
    /// <summary>
    /// Represents a collection of forwarded messages along with an authentication token.
    /// </summary>
    public class ForwardedMessage
    {
        /// <summary>
        /// Gets or sets the authentication token associated with the forwarded messages.
        /// </summary>
        public required string token { get; set; }

        /// <summary>
        /// Gets or sets the list of forwarded messages.
        /// </summary>
        public required List<Message> messages { get; set; }
    }

    /// <summary>
    /// Represents a single message with sender, receiver, timestamp, and content.
    /// </summary>
    public class Message
    {
        /// <summary>
        /// Gets or sets the timestamp of when the message was sent.
        /// </summary>
        public required string timestamp { get; set; }

        /// <summary>
        /// Gets or sets the sender of the message.
        /// </summary>
        public required string sender { get; set; }

        /// <summary>
        /// Gets or sets the receiver of the message.
        /// </summary>
        public required string receiver { get; set; }

        /// <summary>
        /// Gets or sets the content of the message.
        /// </summary>
        public required string message { get; set; }
    }
}
