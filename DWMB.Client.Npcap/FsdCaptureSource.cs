using System;
using System.Text;
using System.Text.RegularExpressions;
using DWMB.Core.Fsd;
using DWMB_AIO.DWMB.Diagnostics;
using SharpPcap;

namespace DWMB_AIO
{
    /// <summary>
    /// Owns raw packet capture: opening the SharpPcap device, applying the FSD BPF
    /// filter, and parsing #TM text-message packets out of the TCP payload. This is
    /// the capture-specific half of v1's OnIncomingFsdPacket
    /// (MainWindow.xaml.cs:245-312) -- the portable filter/forward half now lives in
    /// ClientOrchestrator + DWMB.Core, which is what actually makes this npcap
    /// adapter a "fallback" alongside the vPilot/xPilot plugins instead of the only
    /// implementation.
    /// </summary>
    public sealed class FsdCaptureSource
    {
        private const string BpfFilter = "tcp port 6809";
        private const int ReadTimeoutMs = 2000; // Timeout of 2000ms was set pre-Velocity (2021); VATSIM's 5Hz update rate since then is likely why gibberish appears before/after real messages in the FSD stream.

        private readonly Logger _logger;
        private ICaptureDevice? _device;

        public bool IsCapturing { get; private set; }

        public event EventHandler<FsdMessage>? MessageCaptured;

        public FsdCaptureSource(Logger logger)
        {
            _logger = logger;
        }

        public void Start(ICaptureDevice device)
        {
            _device = device;
            // -= before += makes this safe to call more than once on the same device
            // (event unsubscribe is always a no-op if the handler wasn't attached) --
            // part of the fix for v1's "capture can't be restarted once stopped" TODO.
            _device.OnPacketArrival -= OnPacketArrival;
            _device.OnPacketArrival += OnPacketArrival;

            _device.Open(DeviceModes.None, ReadTimeoutMs);
            _device.Filter = BpfFilter;

            _logger.Log("Starting FSD packet capture on device: " + _device.Description);

            try
            {
                _device.StartCapture();
                IsCapturing = true;
                _logger.Log("FSD packet capture started (background).");
            }
            catch (Exception ex)
            {
                IsCapturing = false;
                _logger.Log("[CRASH] - Failed to start capture: " + ex.Message);
                throw;
            }
        }

        public bool Stop()
        {
            if (_device == null || !IsCapturing)
            {
                _logger.Log("Client was not capturing.  Nothing to stop.");
                return false;
            }

            try
            {
                _device.StopCapture();
                _logger.Log("FSD packet capture stopped.");
                IsCapturing = false;
                return true;
            }
            catch (Exception ex)
            {
                _logger.Log("Error while stopping capture: " + ex);
                return false;
            }
        }

        private void OnPacketArrival(object sender, PacketCapture e)
        {
            string pktString = e.Data.ToString() ?? string.Empty;

            var rawPacket = e.GetPacket();
            var packet = PacketDotNet.Packet.ParsePacket(rawPacket.LinkLayerType, rawPacket.Data);
            var tcpPacket = packet.Extract<PacketDotNet.TcpPacket>();
            if (tcpPacket != null)
            {
                pktString = Encoding.UTF8.GetString(tcpPacket.PayloadData);
            }

            DateTime timestamp = DateTime.UtcNow;

            string[] lines = pktString.Split(new[] { "\n" }, StringSplitOptions.None);
            foreach (string line in lines)
            {
                // Strip out the garbage that appears in between FSD packets
                string input = Regex.Replace(line, "^.*\\$", "$", RegexOptions.Multiline);
                input = Regex.Replace(input, "^.*#", "#", RegexOptions.Multiline);
                input = Regex.Replace(input, "^.*%", "%", RegexOptions.Multiline);
                input = Regex.Replace(input, "^.*@", "@", RegexOptions.Multiline);

                if (input.StartsWith("#TM", StringComparison.Ordinal))
                {
                    var message = new FsdMessage(timestamp, input);
                    MessageCaptured?.Invoke(this, message);
                }
            }
        }
    }
}
