using System;

namespace DWMB.Core
{
    /// <summary>
    /// Which adapter captured a session's messages. Recorded on the server as pure
    /// provenance (session_stats.capture_method) -- the server never branches on it.
    /// </summary>
    public enum CaptureMethod
    {
        VPilotPlugin,
        XPilotPlugin,
        Npcap,
    }

    public static class CaptureMethodExtensions
    {
        public static string ToWireValue(this CaptureMethod method) => method switch
        {
            CaptureMethod.VPilotPlugin => "vpilot_plugin",
            CaptureMethod.XPilotPlugin => "xpilot_plugin",
            CaptureMethod.Npcap => "npcap",
            _ => throw new ArgumentOutOfRangeException(nameof(method), method, null),
        };
    }
}
