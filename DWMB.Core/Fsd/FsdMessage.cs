using System;

namespace DWMB.Core.Fsd
{
    /// <summary>Ported from v1's DWMB.FsdObjects/FsdMessages.cs, made public.</summary>
    public class FsdMessage : FsdPacket
    {
        public string? Message { get; private set; }

        public FsdMessage(DateTime timestamp, string packetString) : base(timestamp, packetString)
        {
            string[] contents = packetString.Split(':');

            // format #TM<sender>:<recipient>:<message>
            for (int i = 0; i < contents.Length; i++)
            {
                switch (i)
                {
                    case 0:
                        Sender = contents[i].Substring(3); // remove #TM prefix
                        break;
                    case 1:
                        // If this is a frequency message, it's addressed to "@xxyyy"
                        // (1xx.yyy MHz). Keep the "@" so the server knows it's a
                        // frequency message.
                        Recipient = contents[i];
                        break;
                    case 2:
                        Message = contents[i];
                        break;
                    default:
                        // additional colons are part of the message text
                        Message += ":" + contents[i];
                        break;
                }
            }
        }
    }
}
