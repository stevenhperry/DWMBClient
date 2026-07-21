namespace DWMB_AIO.DWMB.FsdObjects
{
    /// <summary>
    /// Represents a packet in the FSD protocol, including timestamp, sender, recipient, and packet data.
    /// </summary>
    class FsdPacket
    {
        /// <summary>
        /// The UTC timestamp when the packet was created or received.
        /// </summary>
        public DateTime Timestamp { get; set; } = DateTime.UtcNow;

        /// <summary>
        /// The string representation of the packet's contents.
        /// </summary>
        public string PacketString { get; set; } = string.Empty;

        /// <summary>
        /// The sender of the packet.
        /// </summary>
        public string Sender { get; set; } = string.Empty;

        /// <summary>
        /// The intended recipient of the packet.
        /// </summary>
        public string Recipient { get; set; } = string.Empty;

        /// <summary>
        /// Constructs an FsdPacket from a timestamp and a packet string.
        /// </summary>
        /// <param name="timestamp">The timestamp of the packet.</param>
        /// <param name="packetString">The string contents of the packet.</param>
        public FsdPacket(DateTime timestamp, string packetString)
        {
            this.PacketString = packetString;
            this.Timestamp = timestamp;
        }
    }
}
