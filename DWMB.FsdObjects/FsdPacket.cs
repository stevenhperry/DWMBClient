using System.Text;

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

        /// <summary>
        /// Constructs an FsdPacket from a timestamp and a raw byte array representing the packet.
        /// Removes the TCP header and converts the remaining bytes to a string.
        /// Raises an event if the packet is a private message.
        /// </summary>
        /// <param name="timestamp">The timestamp of the packet.</param>
        /// <param name="packetString">The raw byte array of the packet.</param>
        public FsdPacket(DateTime timestamp, byte[] packetString)
        {
            int TCP_HEADER_SIZE = 54;
            this.Timestamp = timestamp;

            // Remove the TCP header from the packet
            byte[] noHeader = new byte[packetString.Length - TCP_HEADER_SIZE];
            Buffer.BlockCopy(packetString, TCP_HEADER_SIZE, noHeader, 0, noHeader.Length);

            // Convert the remaining bytes to a UTF-8 string and trim any trailing newline
            this.PacketString = Encoding.UTF8.GetString(noHeader).TrimEnd('\n');

            // TODO: Parse multiple FsdPackets from a single TCP packet

            // Raise event if this packet is a private message
            if (this.IsPrivateMessage() && OnMessageArrival != null)
            {
                OnMessageArrival();
            }
        }

        /// <summary>
        /// Determines if the packet is a private message by checking its prefix.
        /// </summary>
        /// <returns>True if the packet is a private message; otherwise, false.</returns>
        public bool IsPrivateMessage()
        {
            if (this.PacketString == null || this.PacketString.Length < 4)
                return false;
            else if (this.PacketString.Substring(0, 3).Equals("#TM"))
                return true;
            else return false;
        }

        /// <summary>
        /// Delegate for message arrival event.
        /// </summary>
        public delegate void msgEventRaiser();

        /// <summary>
        /// Event triggered when a private message arrives.
        /// </summary>
        public event msgEventRaiser? OnMessageArrival;
    }
}
