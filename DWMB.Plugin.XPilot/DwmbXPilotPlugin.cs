using System;
using DWMB.Core;
using DWMB.Core.Api;
using DWMB.Core.Config;
using Vatsim.Xpilot.PluginSdk;

namespace DWMB.Plugin.XPilot
{
    /// <summary>
    /// xPilot plugin entry point. xPilot scans its Plugins folder for public,
    /// non-abstract IPlugin implementations with a public parameterless constructor,
    /// instantiates each one found, then calls Initialize(IBroker) once. Thin shim,
    /// identical shape to DWMB.Plugin.VPilot -- all forwarding/filtering logic lives
    /// in the shared DWMB.Core.
    /// </summary>
    public sealed class DwmbXPilotPlugin : IPlugin
    {
        private const string ClientName = "DWMB.Plugin.XPilot";
        private const string ClientVersion = "2.0.0";

        private IDwmbApiClient? _api;
        private string? _myCallsign;

        public string Name => "DWMB";

        public void Initialize(IBroker broker)
        {
            DwmbConfig config;
            try
            {
                config = DwmbConfig.Load();
            }
            catch (DwmbApiException)
            {
                // No dwmb.config.json next to this DLL yet -- nothing to do until the
                // user completes onboarding and drops their token in. Fail quietly
                // rather than crash xPilot's plugin loader.
                return;
            }

            // DwmbConfig.Load() only requires `server` -- a plugin has no other source
            // for the token (unlike the npcap fallback, which keeps a UI text entry),
            // so it must check for one itself.
            if (string.IsNullOrWhiteSpace(config.Token))
            {
                return;
            }

            // Captured by the closures below as a local rather than read from the _api
            // field each time, so the compiler's nullable flow analysis (which narrows
            // locals far more reliably than mutable fields) knows it's never null here.
            var api = new DwmbApiClient(config, CaptureMethod.XPilotPlugin, ClientName, ClientVersion);
            _api = api;

            broker.NetworkConnected += (sender, e) =>
            {
                _myCallsign = e.Callsign;
                _ = api.RegisterAsync(_myCallsign);
            };

            broker.NetworkDisconnected += (sender, e) =>
            {
                _ = api.DeregisterAsync();
            };

            broker.PrivateMessageReceived += (sender, e) =>
            {
                string? callsign = _myCallsign;
                if (callsign == null)
                {
                    return;
                }
                if (!MessageFilter.IsForwardable(e.From, callsign, e.Message, callsign))
                {
                    return;
                }
                var msg = new RelayMessage(DateTimeOffset.UtcNow, e.From, callsign, e.Message);
                _ = api.ForwardAsync(msg);
            };

            broker.RadioMessageReceived += (sender, e) =>
            {
                string? callsign = _myCallsign;
                if (callsign == null)
                {
                    return;
                }
                // On-frequency messages have no "recipient" on the wire -- npcap sees
                // the frequency itself (e.g. "@22800") in that slot, and filters purely
                // on whether the message text is prefixed with the user's callsign.
                // Mirror that here so IsForwardable behaves identically across adapters.
                string freqTag = MessageFilter.FreqTag(e.Frequencies);
                if (!MessageFilter.IsForwardable(e.From, freqTag, e.Message, callsign))
                {
                    return;
                }
                var msg = new RelayMessage(DateTimeOffset.UtcNow, e.From, freqTag, e.Message);
                _ = api.ForwardAsync(msg);
            };
        }
    }
}
