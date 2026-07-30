using System;

namespace DWMB.Core.Fsd
{
    /// <summary>
    /// Base FSD packet: timestamp, sender, recipient, raw packet string. Ported from
    /// v1's DWMB.FsdObjects/FsdPacket.cs, made public; dropped the unused
    /// (DateTime, byte[]) constructor overload, IsPrivateMessage(), and the
    /// OnMessageArrival event -- none were referenced anywhere in v1. Used only by the
    /// npcap fallback adapter: plugin adapters (vPilot/xPilot) receive already-parsed
    /// events from their host and never construct these from raw FSD text.
    /// </summary>
    public class FsdPacket
    {
        public DateTime Timestamp { get; set; } = DateTime.UtcNow;
        public string PacketString { get; set; } = string.Empty;
        public string Sender { get; set; } = string.Empty;
        public string Recipient { get; set; } = string.Empty;

        public FsdPacket(DateTime timestamp, string packetString)
        {
            PacketString = packetString;
            Timestamp = timestamp;
        }
    }
}
