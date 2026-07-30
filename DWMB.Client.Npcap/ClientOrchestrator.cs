using System;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using DWMB.Core;
using DWMB.Core.Api;
using DWMB.Core.Config;
using DWMB.Core.Fsd;
using DWMB_AIO.DWMB.Diagnostics;
using DWMB_AIO.DWMB.FsdDetection;
using SharpPcap;

namespace DWMB_AIO
{
    /// <summary>
    /// Instance-based replacement for v1's static DWMBClient god-class
    /// (MainWindow.xaml.cs:170-464, static callsign/am/device/lastMessage/IsCapturing
    /// fields). Owns session state and returns (bool Success, string? Error) results
    /// instead of calling MessageBox.Show directly, so MainWindow.xaml.cs stays
    /// UI-only -- v1's Deregister() calling MessageBox.Show from inside what's
    /// nominally a non-UI orchestrator was exactly the coupling this fixes.
    /// </summary>
    public sealed class ClientOrchestrator
    {
        private const string ClientName = "DWMB.Client.Npcap";

        private readonly Logger _logger;
        private readonly FsdCaptureSource _captureSource;

        private IDwmbApiClient? _api;
        private string? _callsign;
        private RelayMessage? _lastMessage;

        public bool IsCapturing => _captureSource.IsCapturing;

        public bool? IsRegistered => _api?.IsRegistered;

        public ClientOrchestrator(Logger logger)
        {
            _logger = logger;
            _captureSource = new FsdCaptureSource(logger);
            _captureSource.MessageCaptured += OnMessageCaptured;
        }

        public async Task<(bool Success, string? Error)> StartAsync(string callsignInput, string token)
        {
            _logger.Log("Starting DWMB client");

            var callsignFormat = new Regex(@"^(\d|\w|_|-)+$", RegexOptions.Compiled);
            if (!callsignFormat.IsMatch(callsignInput))
            {
                _logger.Log("[DWMB_API_ERROR] - Callsign contained impermissible characters");
                return (false, "Callsign contains impermissible characters. Please use only letters, numbers, underscores, or hyphens.");
            }
            _callsign = callsignInput;
            _logger.Log($"Client was started with the following arguments: {_callsign} {token}");

            DwmbConfig config;
            try
            {
                config = DwmbConfig.Load();
            }
            catch (DwmbApiException ex)
            {
                return (false, ex.Message);
            }
            // npcap keeps the v1 UI callsign+token entry rather than persisting a
            // token to disk (unlike the plugin adapters) -- see DwmbConfig's doc comment.
            config.Token = token;

            _api = new DwmbApiClient(config, CaptureMethod.Npcap, ClientName, AppInfo.DisplayVersion);

            var (registerSuccess, registerError) = await _api.RegisterAsync(_callsign).ConfigureAwait(true);
            if (!registerSuccess)
            {
                _logger.Log("[DWMB_API_ERROR] - Client failed to register with the server.");
                return (false, registerError ?? "Failed to register with the server. Please check your callsign and registration code.");
            }

            try
            {
                // Fixes v1's "capture can't be restarted once stopped" TODO
                // (MainWindow.xaml.cs:57): Stop→Start re-enumerates devices and gets a
                // fresh ICaptureDevice each time rather than reusing one that was
                // already opened and stopped, and FsdCaptureSource.Start() is itself
                // now safe to call more than once on the same device besides.
                ICaptureDevice device = SelectDevice();
                _captureSource.Start(device);
            }
            catch (Exception ex)
            {
                _logger.Log("[CRASH] - An unexpected error occurred: " + ex.Message);
                return (false, $"Unexpected error: {ex.Message}");
            }

            return (true, null);
        }

        public bool Stop() => _captureSource.Stop();

        public async Task<(bool Success, string? Error)> DeregisterAsync()
        {
            if (_api == null || !_api.IsRegistered)
            {
                _logger.Log("Client is not registered.  Cannot deregister.");
                return (false, null);
            }

            try
            {
                bool ok = await _api.DeregisterAsync().ConfigureAwait(true);
                return (ok, ok ? null : "Deregistration request failed. Please try again, or contact an admin for help.");
            }
            catch (Exception ex)
            {
                _logger.Log("[CRASH] - An unexpected error occurred while deregistering: " + ex.Message);
                return (false, "Something went wrong while deregistering.");
            }
        }

        /// <summary>Independent of session state -- loads config fresh and opens a
        /// throwaway client, matching v1's btnTest_Click dummy-instance pattern.</summary>
        public async Task<bool> TestConnectionAsync()
        {
            try
            {
                DwmbConfig config = DwmbConfig.Load();
                using var testClient = new DwmbApiClient(config, CaptureMethod.Npcap, ClientName, AppInfo.DisplayVersion);
                return await testClient.TestConnectionAsync().ConfigureAwait(true);
            }
            catch
            {
                return false;
            }
        }

        private void OnMessageCaptured(object? sender, FsdMessage msg)
        {
            // Captured into locals rather than read from the fields repeatedly below --
            // the compiler's nullable flow analysis resets a field's narrowed state
            // after any intervening method call, so re-reading _callsign/_api after the
            // MessageFilter calls below would otherwise still look possibly-null.
            string? callsign = _callsign;
            IDwmbApiClient? api = _api;
            if (callsign == null || api == null)
            {
                return;
            }

            string messageText = msg.Message ?? string.Empty;
            if (!MessageFilter.IsForwardable(msg.Sender, msg.Recipient, messageText, callsign))
            {
                return;
            }

            var relay = new RelayMessage(msg.Timestamp, msg.Sender, msg.Recipient, messageText);
            if (MessageFilter.IsDuplicate(_lastMessage, relay))
            {
                _logger.Log("Duplicate message detected within 2 seconds.  Ignoring.");
                return;
            }
            _lastMessage = relay;

            _ = api.ForwardAsync(relay);
        }

        private ICaptureDevice SelectDevice()
        {
            var cm = new ConnectionManager();
            var connections = cm.Connections;

            if (connections.Count == 1)
            {
                return connections[0].Device;
            }

            var dialog = new DeviceSelectionWindow(connections);
            bool? result = dialog.ShowDialog();
            if (result != true || dialog.SelectedDevice == null)
            {
                throw new InvalidOperationException("No capture device was selected.");
            }
            return dialog.SelectedDevice.Device;
        }
    }
}
