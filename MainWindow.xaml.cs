using DWMB_AIO.DWMB.Diagnostics;
using DWMB_AIO.DWMB.FsdDetection;
using DWMB_AIO.DWMB.FsdObjects;
using DWMB_AIO.DWMB.Serialization;
using SharpPcap;
using System.Diagnostics;
using System.Text;
using System.Text.RegularExpressions;
using System.Windows;
using System.Windows.Media;



namespace DWMB_AIO
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        public MainWindow()
        {
            InitializeComponent();

            CheckNpcapInstalled();

            // Surface forwarding failures raised on the capture thread (issue #8).
            DWMBClient.ForwardStatusChanged += OnForwardStatusChanged;

            UpdateStatus(DWMBClient.IsRegistered, DWMBClient.IsCapturing); //force false on registration since we used dummy values.

        }

        /// <summary>
        /// Warns the user at startup if no capture driver (Npcap) is found, with
        /// instructions to install it, instead of only failing later when they click
        /// Start. Non-blocking beyond the dialog itself — the window still opens either
        /// way, since the user may just want to look around or deregister.
        /// </summary>
        private void CheckNpcapInstalled()
        {
            if (PcapDriverCheck.IsAvailable(out string? errorDetail))
            {
                return;
            }

            new Logger().Log("[STARTUP] Npcap/WinPcap driver not found: " + errorDetail);

            var result = MessageBox.Show(
                PcapDriverCheck.BuildMissingDriverMessage(errorDetail) + "\n\nOpen the Npcap download page now?",
                "DWMB - Npcap Not Found",
                MessageBoxButton.YesNo,
                MessageBoxImage.Warning);

            if (result == MessageBoxResult.Yes)
            {
                try
                {
                    Process.Start(new ProcessStartInfo(PcapDriverCheck.DownloadUrl) { UseShellExecute = true });
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"Unable to open link: {ex.Message}", "DWMB - Error", MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
        }

        private void btnStart_Click(object sender, RoutedEventArgs e)
        {
            string callsign = txtCallsign.Text;
            string regCode = txtRegCode.Text;
            var environment = chkUseDevServer.IsChecked == true
                ? ServerEnvironment.Development
                : ServerEnvironment.Production;

            try
            {
                var result = DWMBClient.MainApp(callsign, regCode, new Logger(), environment);  // returns success + optional error

                if (!result.Success)
                {
                    // Registration (or validation) failed — show a descriptive error and stop further processing.
                    MessageBox.Show(result.Error ?? "Unknown error during startup.", "DWMB - Error", MessageBoxButton.OK, MessageBoxImage.Error);
                    // Do not LockInputs or UpdateStatus beyond showing the error.
                    return;
                }

                // If we reach here, registration succeeded and capture was started.
                LockInputs();
                UpdateStatus(DWMBClient.IsRegistered, DWMBClient.IsCapturing);
            }
            catch (Exception ex)
            {
                // Unexpected exception — report to user and do not proceed.
                MessageBox.Show($"Unexpected error while starting DWMB client: {ex.Message}", "DWMB - Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void btnStop_Click(object sender, RoutedEventArgs e)  //TODO - need to move the device capture command to another function which can be restarted.  Presently once stopped, it will not re-start.
        {

            if (DWMBClient.Stop())
            {
                MessageBox.Show("Successfully Stopped Capture.","DWMB - Caution",MessageBoxButton.OK,MessageBoxImage.Information); //since we have not yet implemented de-registering
            }
            else  // not sure the logic is correct here.  Investigate.
            {
                MessageBox.Show("You are not capturing.  There is nothing to stop.","DWMB - Caution",MessageBoxButton.OK,MessageBoxImage.Warning);
                //but since we are still registered, we do not unlock the inputs.
            }
            UpdateStatus(DWMBClient.IsRegistered, DWMBClient.IsCapturing);

        }

        private void btnDeregister_Click(object sender, RoutedEventArgs e)
        {

            // Best-effort stop of any active capture. Deregistration must proceed whether
            // or not capture is currently running — e.g. after a Pause, capture is already
            // stopped, but the client is still registered and must be removed server-side.
            DWMBClient.Stop();

            if (DWMBClient.Deregister(this.txtRegCode.Text))
            {
                //success
                MessageBox.Show("Successfully deregistered and stopped capturing!");
                UnlockInputs();
            }
            else
            {
                //failure to deregister
                MessageBox.Show("De-registration failed.\nYou need to DM the bot with 'remove'!!", "DWMB - Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }

            UpdateStatus(DWMBClient.IsRegistered, DWMBClient.IsCapturing);
        }

        private void btnTest_Click(object sender, RoutedEventArgs e)
        {
            ApiManager? am;
            string response;


            am = new ApiManager("asdf1234", "N98765"); // dummy values to run a test

            response = string.Format("Connection test response: {0}", am.TestConnection());

            MessageBox.Show(response, "DWMB - Test");
        }

        private void UpdateStatus(bool? statusRegistration, bool? statusCapturing)
        {
            txtStatusRegister.Text = string.Format("Registered: {0}", statusRegistration);
            txtStatusCapture.Text = string.Format("Capturing: {0}", statusCapturing);

            //now set the colors for registration status
            if (statusRegistration == true)
            {
                txtStatusRegister.Foreground = new SolidColorBrush(Colors.Green);
                txtStatusRegister.Background = new SolidColorBrush(Colors.LightGray);
            }
            else
            {
                txtStatusRegister.Foreground = new SolidColorBrush(Colors.Black);
                txtStatusRegister.Background = new SolidColorBrush(Colors.LightYellow);
            }

            //and also for the capture status
            if (statusCapturing == true)
            {
                txtStatusCapture.Foreground = new SolidColorBrush(Colors.Green);
                txtStatusCapture.Background = new SolidColorBrush(Colors.LightGray);
            }
            else
            {
                txtStatusCapture.Foreground = new SolidColorBrush(Colors.Black);
                txtStatusCapture.Background = new SolidColorBrush(Colors.LightYellow);
            }

            // keep the forwarding-health indicator in sync with start/stop transitions
            UpdateForwardStatus();
        }

        /// <summary>
        /// Handles <see cref="DWMBClient.ForwardStatusChanged"/>, which may fire on the
        /// capture thread — marshal to the UI thread before touching controls (issue #8).
        /// </summary>
        private void OnForwardStatusChanged()
        {
            if (Dispatcher.CheckAccess())
            {
                UpdateForwardStatus();
            }
            else
            {
                Dispatcher.BeginInvoke(new Action(UpdateForwardStatus));
            }
        }

        /// <summary>
        /// Renders the message-forwarding health: green "OK" while capturing with no
        /// failures, red "N failed" (with the last error in the tooltip) once any forward
        /// has failed, and a neutral idle state otherwise.
        /// </summary>
        private void UpdateForwardStatus()
        {
            var (failures, lastError, lastErrorUtc) = DWMBClient.GetForwardStatus();

            if (failures > 0)
            {
                txtStatusForward.Text = string.Format("Forwarding: {0} failed", failures);
                txtStatusForward.Foreground = new SolidColorBrush(Colors.White);
                txtStatusForward.Background = new SolidColorBrush(Colors.Firebrick);
                txtStatusForward.ToolTip = string.Format("Last failure {0:u}: {1}", lastErrorUtc, lastError);
            }
            else if (DWMBClient.IsCapturing)
            {
                txtStatusForward.Text = "Forwarding: OK";
                txtStatusForward.Foreground = new SolidColorBrush(Colors.Green);
                txtStatusForward.Background = new SolidColorBrush(Colors.LightGray);
                txtStatusForward.ToolTip = "All captured messages have forwarded successfully.";
            }
            else
            {
                txtStatusForward.Text = "Forwarding: —";
                txtStatusForward.Foreground = new SolidColorBrush(Colors.Black);
                txtStatusForward.Background = new SolidColorBrush(Colors.LightYellow);
                txtStatusForward.ToolTip = "Message-forwarding health (idle).";
            }
        }

        private void LockInputs()
        {
            txtCallsign.IsEnabled = false;
            txtRegCode.IsEnabled = false;
            // Prevent switching prod/dev servers while registered/capturing: the active
            // ApiManager is already bound to whichever server it was constructed against,
            // so flipping this mid-connection wouldn't reconnect anything.
            chkUseDevServer.IsEnabled = false;
        }

        private void UnlockInputs()
        {
            txtCallsign.IsEnabled = true;
            txtRegCode.IsEnabled = true;
            chkUseDevServer.IsEnabled = true;
        }

        private void KofiButton_Click(object sender, RoutedEventArgs e)
        {
            const string url = "https://ko-fi.com/dontwallopmebro";
            try
            {
                Process.Start(new ProcessStartInfo(url) { UseShellExecute = true });
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Unable to open link: {ex.Message}", "DWMB - Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }
    }


    class DWMBClient
    {

        // initialize variables
        static string callsign = "";
        // Deliberately null until the user clicks Start. Constructing an ApiManager
        // eagerly here validated the compiled-in server URL in a field initializer, so
        // a malformed value crashed the app at launch with a cryptic
        // TypeInitializationException (issue #5). It's now only validated when the
        // user actually starts, where the error is caught and shown as a friendly
        // dialog. IsRegistered/Stop already treat a null am as "not registered".
        static ApiManager? am;
        static Logger logger = new(); // Default log file "log.txt"
        static ICaptureDevice? device; // Define at class level to share across Main and Stop functions
        static FsdMessage? lastMessage;

        // Guards the shared static state (am, callsign, lastMessage) that is read on the
        // SharpPcap capture thread and mutated on the WPF UI thread (issue #10). Held only
        // briefly to publish or snapshot references — never across network I/O.
        static readonly object stateLock = new object();

        // Precompiled regexes used to strip the inter-packet garbage from each FSD line
        // and to validate the callsign. Making them static readonly avoids recompiling the
        // same patterns on every packet on the hot capture path (issue #12).
        static readonly Regex DollarCleanRegex = new Regex("^.*\\$", RegexOptions.Multiline | RegexOptions.Compiled);
        static readonly Regex HashCleanRegex = new Regex("^.*#", RegexOptions.Multiline | RegexOptions.Compiled);
        static readonly Regex PercentCleanRegex = new Regex("^.*%", RegexOptions.Multiline | RegexOptions.Compiled);
        static readonly Regex AtCleanRegex = new Regex("^.*@", RegexOptions.Multiline | RegexOptions.Compiled);
        static readonly Regex CallsignFormatRegex = new Regex(@"^(\d|\w|_|-)+$", RegexOptions.Compiled);

        public static bool IsCapturing { get; set; } = false;
        public static bool? IsRegistered => am?.IsRegistered;

        // --- Message-forwarding health tracking (issue #8) ---
        // Forward failures used to be logged only; a burst (e.g. server unreachable) left
        // the UI still showing "Capturing: true" while messages were silently dropped.
        // These aggregate the failures so the UI can surface a visible indicator.
        static int forwardFailureCount;
        static string? lastForwardError;
        static DateTime? lastForwardErrorUtc;

        /// <summary>
        /// Raised whenever forwarding health changes. May be raised on the capture thread,
        /// so subscribers must marshal to the UI thread before touching UI.
        /// </summary>
        public static event Action? ForwardStatusChanged;

        /// <summary>Returns a consistent snapshot of the current forwarding health.</summary>
        public static (int Failures, string? LastError, DateTime? LastErrorUtc) GetForwardStatus()
        {
            lock (stateLock)
            {
                return (forwardFailureCount, lastForwardError, lastForwardErrorUtc);
            }
        }

        /// <summary>Clears the failure tally (called when a fresh capture session starts).</summary>
        private static void ResetForwardStatus()
        {
            lock (stateLock)
            {
                forwardFailureCount = 0;
                lastForwardError = null;
                lastForwardErrorUtc = null;
            }
            ForwardStatusChanged?.Invoke();
        }

        /// <summary>Records a forward failure and notifies subscribers.</summary>
        private static void RecordForwardFailure(string error)
        {
            lock (stateLock)
            {
                forwardFailureCount++;
                lastForwardError = error;
                lastForwardErrorUtc = DateTime.UtcNow;
            }
            ForwardStatusChanged?.Invoke();
        }


        /// <summary>
        /// Starts the client: validates input, registers, and begins capture.
        /// Returns a tuple indicating overall success and an error message when applicable.
        /// </summary>
        public static (bool Success, string? Error) MainApp(string strCallsignInput, string strRegCode, Logger logger, ServerEnvironment environment = ServerEnvironment.Production)
        {
            // check for valid inputs

            try
            {
                // ensure the class-level logger is set so other methods can write to the same log
                DWMBClient.logger = logger;
                logger.Log("Starting DWMBClient MainApp");

                bool isInputValid = false;

                if (CallsignFormatRegex.IsMatch(strCallsignInput)) // if valid callsign format (alphanumeric, underscores, or hyphens)
                {
                    isInputValid = true; // set but not used.  We avoid the else statement below.

                    // The registration code is intentionally logged in plaintext. It is a
                    // disposable, per-session token (regenerated each session, old ones
                    // invalidated server-side) and is already shown to the user in Discord,
                    // so recording it here for troubleshooting is acceptable (issue #13).
                    logger.Log(String.Format("Client was started with the following arguments: {0} {1}", strCallsignInput, strRegCode));

                    // Build the ApiManager (reads/validates config) before taking the lock,
                    // then publish callsign + am together so the capture thread never sees a
                    // mismatched (am, callsign) pair (issue #10).
                    ApiManager newAm = new ApiManager(strRegCode, strCallsignInput, environment);
                    lock (stateLock)
                    {
                        callsign = strCallsignInput;
                        am = newAm;
                    }

                    newAm.Register(strRegCode, strCallsignInput);


                    if (!newAm.IsRegistered)  //if the registration is not successful
                    {
                        logger.Log("[DWMB_API_ERROR] - Client failed to register with the server.");
                        // Return error instead of showing a MessageBox here so the caller can decide how to present the error.
                        return (false, "Failed to register with the server. Please check your callsign and registration code.");
                    }
                }
                else  //if callsign is invalid
                {
                    isInputValid = false; // set but not used.  Return in this else function exits anyway.
                    logger.Log("[DWMB_API_ERROR] - Callsign contained impermissible characters");
                    return (false, "Callsign contains impermissible characters. Please use only letters, numbers, underscores, or hyphens.");
                }


                // if input is valid, proceed to start packet capture
                BeginCapture();
                am.IsCapturing = true;

                //TODO: start heartbeat timer here and stop it in the Stop() function.  Also, consider whether we need to send a final heartbeat on shutdown to let the server know we're gone.
                                

                // Success
                return (true, null);
            }
            catch (OperationCanceledException oce)
            {
                // User cancelled device selection — not an error, just an abort.
                logger.Log("[INFO] Start aborted: " + oce.Message);
                IsCapturing = false;
                return (false, oce.Message);
            }
            catch (DWMBApiException dae)
            {
                // Configuration / API problems (e.g. malformed compiled-in server
                // URL, no capture device) carry an actionable message — surface it
                // directly instead of as "Unexpected error".
                logger.Log("[CONFIG-ERROR] " + dae.Message);
                IsCapturing = false;
                return (false, dae.Message);
            }
            catch (DllNotFoundException dnfe)
            {
                // Same failure the startup Npcap check watches for, just hit here
                // instead (e.g. the check was dismissed, or the driver was removed
                // mid-session). Give the same install instructions rather than a raw
                // "Unable to load DLL 'wpcap'" message.
                logger.Log("[CONFIG-ERROR] Npcap/WinPcap driver not found: " + dnfe.Message);
                IsCapturing = false;
                return (false, PcapDriverCheck.BuildMissingDriverMessage(dnfe.Message));
            }
            catch (Exception ex)
            {
                logger.Log("[CRASH] - An unexpected error occurred: " + ex.Message);
                IsCapturing = false;
                return (false, $"Unexpected error: {ex.Message}");
            }
        }

        public static void OnIncomingFsdPacket(object sender, PacketCapture e)
        {
            // This runs on the SharpPcap capture thread. Any exception that escapes
            // this method is unhandled on a background thread and terminates the
            // process (issue #6). A single malformed/unexpected packet must never
            // take the client down, so the entire body is guarded: log and continue.
            try
            {
                ProcessIncomingFsdPacket(e);
            }
            catch (Exception ex)
            {
                logger.Log("[CAPTURE-ERROR] Failed to process a captured packet (ignored): " + ex);
            }
        }

        private static void ProcessIncomingFsdPacket(PacketCapture e)
        {
            DateTime timestamp = DateTime.UtcNow;

            var rawPacket = e.GetPacket();
            var packet = PacketDotNet.Packet.ParsePacket(rawPacket.LinkLayerType, rawPacket.Data);
            var tcpPacket = packet.Extract<PacketDotNet.TcpPacket>();
            if (tcpPacket == null || tcpPacket.PayloadData == null || tcpPacket.PayloadData.Length == 0)
            {
                // No TCP payload to parse — nothing to do. (Previously the code fell
                // through and processed e.Data.ToString(), which is a type name, not
                // packet data.)
                return;
            }

            string pktString = Encoding.UTF8.GetString(tcpPacket.PayloadData);

            // Take a consistent snapshot of the shared state the UI thread can reassign
            // (issue #10), so this whole packet is processed against one (am, callsign)
            // pair even if the user pauses/restarts mid-processing.
            ApiManager? currentAm;
            string currentCallsign;
            lock (stateLock)
            {
                currentAm = am;
                currentCallsign = callsign;
            }

            // Not started (or already torn down) — nothing to forward to.
            if (currentAm == null)
            {
                return;
            }

            // Split the packet into individual lines
            string[] inputs = pktString.Split(new string[] { "\n" }, StringSplitOptions.None);
            foreach (string line in inputs)
            {
                // Strip out the garbage that appears in between FSD packets
                string input = DollarCleanRegex.Replace(line, "$");
                input = HashCleanRegex.Replace(input, "#");
                input = PercentCleanRegex.Replace(input, "%");
                input = AtCleanRegex.Replace(input, "@");

                // Create a FsdPacket object from the cleaned input
                FsdPacket currPacket = new FsdPacket(timestamp, input);

                // Only do something if it is a PM
                if (input.StartsWith("#TM"))
                {
                    FsdMessage input_pm = new FsdMessage(timestamp, input);

                    if (IsForwardMessage(input_pm, currentCallsign))
                    {
                        // Check to see if same as last message.  If so, ignore it.
                        bool isDuplicate = false;
                        lock (stateLock)
                        {
                            if (lastMessage != null &&
                                string.Equals(lastMessage.Sender, input_pm.Sender, StringComparison.OrdinalIgnoreCase) &&
                                string.Equals(lastMessage.Recipient, input_pm.Recipient, StringComparison.OrdinalIgnoreCase) &&
                                string.Equals(lastMessage.Message, input_pm.Message, StringComparison.OrdinalIgnoreCase) &&
                                (input_pm.Timestamp - lastMessage.Timestamp).TotalSeconds < 2) // within 2 seconds
                            {
                                isDuplicate = true;
                            }
                        }
                        if (isDuplicate)
                        {
                            logger.Log("Duplicate message detected within 2 seconds.  Ignoring.");
                            continue; // skip processing this duplicate message
                        }

                        string loggingString = String.Format("{0} > {1} ({2}):\"{3}\" ",
                                                        input_pm.Sender,
                                                        input_pm.Recipient,
                                                        input_pm.Timestamp.ToUniversalTime(),
                                                        input_pm.Message);

                        try
                        {
                            // Forward outside the lock — never hold it across network I/O.
                            currentAm.ForwardMessage(input_pm);
                            lock (stateLock)
                            {
                                lastMessage = input_pm;
                            }
                        }
                        catch (Exception ex)
                        {
                            // logger IS static on DWMBClient, so record the failure here
                            // instead of dropping it silently. The message was not
                            // delivered to the server; note the affected message and
                            // surface it in the UI via the forwarding-health indicator.
                            logger.Log("[FORWARD-ERROR] Failed to forward message (" + loggingString + "): " + ex.Message);
                            RecordForwardFailure(ex.Message);
                        }

                    }
                }
            }
        }


        /// <summary>
        /// Helper method for determining if a FsdMessage should be forwarded.
        /// </summary>
        /// <param name="msg">The FsdMessage in question</param>
        /// <param name="callsign">The user's callsign to match against (passed in so the
        /// capture thread uses a consistent snapshot rather than reading the static field).</param>
        /// <returns>True if it should be forwarded, False otherwise</returns>
        private static bool IsForwardMessage(FsdMessage msg, string callsign)
        {
            // Under-the-hood ones to SERVER/FP/DATA...
            bool isServerMessage =
                string.Equals(msg.Sender, "server", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(msg.Recipient, "server", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(msg.Recipient, "fp", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(msg.Recipient, "data", StringComparison.OrdinalIgnoreCase)
                ;

            // on-frequency and private messages addressed to the user...

            // On-frequency messages address the user as "{callsign} ..." or "{callsign},...".
            // We require the character right after the callsign to be a space or comma so we
            // don't partial-match (e.g. UAL1 vs UAL123). Done with string ops instead of a
            // per-packet regex compile (issue #12); equivalent to ^{callsign}( |,).*.
            string message = msg.Message ?? string.Empty;
            bool startsWithCallsign =
                message.Length > callsign.Length &&
                message.StartsWith(callsign, StringComparison.OrdinalIgnoreCase) &&
                (message[callsign.Length] == ' ' || message[callsign.Length] == ',');

            bool isAddressedToUser = startsWithCallsign ||
                                    string.Equals(msg.Recipient, callsign, StringComparison.OrdinalIgnoreCase);

            // self-addressed messages:
            bool isSelfMessage = string.Equals(msg.Sender, callsign, StringComparison.OrdinalIgnoreCase);

            return !isServerMessage && isAddressedToUser && !isSelfMessage;
        }

        public static bool Stop()
        {
            if (am != null)
            {
                if (am.IsCapturing)  //if already capturing
                {
                    try
                    {
                        if (device != null)
                        {
                            // Unsubscribe the handler and close the device so a later Start
                            // re-initializes cleanly instead of double-subscribing / leaking
                            // the device for the process lifetime (issue #11).
                            device.OnPacketArrival -= new SharpPcap.PacketArrivalEventHandler(OnIncomingFsdPacket);
                            device.StopCapture();
                            device.Close();
                            device = null;
                        }

                        am.IsCapturing = false;
                        IsCapturing = false;

                        // Pausing capture should also stop heartbeats, otherwise the server
                        // keeps treating this (non-capturing) client as online (issue #9).
                        // Heartbeats resume when the user starts again (re-registration).
                        am.StopHeartbeat();

                        logger.Log("FSD packet capture stopped.");
                        return true;
                    }
                    catch (Exception ex)
                    {
                        logger.Log("Error in the Stop function: " + ex);
                        return false;
                    }
                }
                else // not capturing
                {
                    logger.Log("Client was not capturing.  Nothing to stop.");
                    //MessageBox.Show("You are not registered.  There is nothing to stop.","DWMB: Nothing to Stop");
                    return false;
                }
            }
            else
            {
                //MessageBox.Show("You are not registered.  There is nothing to stop.", "DWMB: Nothing to Stop");
                return false;
            }
        }

        public static bool Deregister(string strToken)
        {
            // am is null before the first Start (issue #5 change), so null-guard here.
            if (am != null && am.IsRegistered)
            {
                try
                {
                    return am.Deregister(strToken);
                }
                catch (Exception ex)
                {
                    logger.Log("[CRASH] - An unexpected error occurred: " + ex.Message);
                    MessageBox.Show("MAYDAY MAYDAY!  Something went wrong when deregistering!!!!", "DWMB - Error", MessageBoxButton.OK, MessageBoxImage.Error);
                    return false;
                }
            }
            else
            {
                logger.Log("Client is not registered.  Cannot deregister.");
                return false;
            }


        }

        public static void BeginCapture()
        {

            ConnectionManager cm = new ConnectionManager();
            List<HardwareDevice> connections = cm.Connections;

            if (connections.Count == 0)
            {
                // No adapter with a local IP was found. Previously this fell into the
                // Console prompt loop and spun the UI at 100% CPU (issue #4). Fail with
                // an actionable message instead.
                logger.Log("[CAPTURE] No suitable network adapter found.");
                throw new DWMBApiException(
                    "No suitable network adapter was found. Make sure Npcap (or WinPcap) is installed " +
                    "and you have an active network connection, then try Start again.");
            }
            else if (connections.Count == 1)
            {
                // Exactly one candidate — use it without prompting.
                device = connections[0].Device;
            }
            else
            {
                // Multiple candidates — ask the user via a WPF dialog rather than a
                // Console prompt, which does not work in a windowed app and froze the
                // UI thread (issue #4).
                var dialog = new DeviceSelectionWindow(connections)
                {
                    Owner = Application.Current?.MainWindow
                };

                bool? result = dialog.ShowDialog();
                if (result != true || dialog.SelectedDevice == null)
                {
                    throw new OperationCanceledException(
                        "Adapter selection was cancelled. The client did not start capturing.");
                }

                device = dialog.SelectedDevice;
            }

            device.OnPacketArrival += new SharpPcap.PacketArrivalEventHandler(DWMBClient.OnIncomingFsdPacket);

            // open device for capturing
            int readTimeOutMilliseconds = 2000;
            //Timeout of 2000 was set for VATSIM pre-Velocity project (2021).  Now with an update rate of 5hz, we need to be more responsive.  This could explain the gibberish we're seeing before/after real messages in the FSD packets.

            device.Open(DeviceModes.None, readTimeOutMilliseconds);


            device.Filter = "tcp port 6809";

            logger.Log("Starting FSD packet capture on device: " + device.Description);


            try
            {
                // Clear any forwarding failures from a previous session so the health
                // indicator starts fresh for this capture (issue #8).
                ResetForwardStatus();

                // start non-blocking capture
                device.StartCapture();
                IsCapturing = true;
                logger.Log("FSD packet capture started (background).");
            }
            catch (Exception ex)
            {
                // ensure consistent state if StartCapture fails
                IsCapturing = false;
                logger.Log("[CRASH] - Failed to start capture: " + ex.Message);
                throw;
            }
        }
    }
}

