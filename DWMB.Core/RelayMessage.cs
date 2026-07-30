using System;

namespace DWMB.Core
{
    /// <summary>A message ready to forward to the server, regardless of which adapter captured it.</summary>
    public sealed record RelayMessage(DateTimeOffset Timestamp, string Sender, string Receiver, string Message);
}
