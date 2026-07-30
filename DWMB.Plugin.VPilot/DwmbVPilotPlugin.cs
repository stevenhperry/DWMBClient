using System;
using DWMB.Core;
using DWMB.Core.Api;
using DWMB.Core.Config;
using RossCarlson.Vatsim.Vpilot.Plugins;

namespace DWMB.Plugin.VPilot
{
    /// <summary>
    /// vPilot plugin entry point. vPilot discovers this via reflection (a public,
    /// non-abstract IPlugin implementation with a public parameterless constructor),
    /// instantiates it, then calls Initialize(IBroker) once. Thin shim: all
    /// forwarding/filtering logic lives in DWMB.Core, shared with DWMB.Plugin.XPilot
    /// and DWMB.Client.Npcap.
    /// </summary>
    public sealed class DwmbVPilotPlugin : IPlugin
    {
        private const string ClientName = "DWMB.Plugin.VPilot";
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
                // rather than crash vPilot's plugin loader.
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
            var api = new DwmbApiClient(config, CaptureMethod.VPilotPlugin, ClientName, ClientVersion);
            _api = api;

            broker.NetworkConnected += (sender, e) =>
            {
                // Callsign is auto-detected here -- the user only ever supplies a token.
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
