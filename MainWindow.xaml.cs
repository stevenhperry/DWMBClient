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

            UpdateStatus(DWMBClient.IsRegistered, DWMBClient.IsCapturing); //force false on registration since we used dummy values.

        }

        private void btnStart_Click(object sender, RoutedEventArgs e)
        {
            string callsign = txtCallsign.Text;
            string regCode = txtRegCode.Text;

            try
            {
                var result = DWMBClient.MainApp(callsign, regCode, new Logger());  // returns success + optional error

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

            if (DWMBClient.Stop())
            {
                if (DWMBClient.Deregister(this.txtRegCode.Text))
                {
                    //success
                    MessageBox.Show("Successfully deregistered and stopped capturing!");
                    UnlockInputs();
                }
                else
                {
                    //failure to deregister
                    MessageBox.Show("Capture stopped.  However, de-registration failed.\nYou need to DM the bot with 'remove'!!", "DWMB - Error", MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
            else //Still capturing and did not deregister
            {
                MessageBox.Show("Error when deregistering.\nYou need to DM the bot with 'remove'!!", "DWMB - Error", MessageBoxButton.OK, MessageBoxImage.Error);
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
        }

        private void LockInputs()
        {
            txtCallsign.IsEnabled = false;
            txtRegCode.IsEnabled = false;
        }

        private void UnlockInputs()
        {
            txtCallsign.IsEnabled = true;
            txtRegCode.IsEnabled = true;
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
        // eagerly here read server_location.txt in a field initializer, so a missing
        // or malformed config file crashed the app at launch with a cryptic
        // TypeInitializationException (issue #5). The file is now only read when the
        // user actually starts, where the error is caught and shown as a friendly
        // dialog. IsRegistered/Stop already treat a null am as "not registered".
        static ApiManager? am;
        static Logger logger = new(); // Default log file "log.txt"
        static ICaptureDevice? device; // Define at class level to share across Main and Stop functions
        static FsdMessage? lastMessage;
        public static bool IsCapturing { get; set; } = false;
        public static bool? IsRegistered => am?.IsRegistered;


        /// <summary>
        /// Starts the client: validates input, registers, and begins capture.
        /// Returns a tuple indicating overall success and an error message when applicable.
        /// </summary>
        public static (bool Success, string? Error) MainApp(string strCallsignInput, string strRegCode, Logger logger)
        {
            // check for valid inputs

            try
            {
                // ensure the class-level logger is set so other methods can write to the same log
                DWMBClient.logger = logger;
                logger.Log("Starting DWMBClient MainApp");

                bool isInputValid = false;
                Regex callsignFormat = new Regex(@"^(\d|\w|_|-)+$");  //String must be alphanumeric, underscores, or hyphens

                if (callsignFormat.IsMatch(strCallsignInput)) // if valid callsign format
                {
                    isInputValid = true; // set but not used.  We avoid the else statement below.
                    callsign = strCallsignInput;

                    logger.Log(String.Format("Client was started with the following arguments: {0} {1}", callsign, strRegCode));


                    am = new ApiManager(strRegCode, callsign);
                    am.Register(strRegCode, callsign);


                    if (!am.IsRegistered)  //if the registration is not successful
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
                // Configuration / API problems (e.g. missing or malformed
                // server_location.txt, no capture device) carry an actionable
                // message — surface it directly instead of as "Unexpected error".
                logger.Log("[CONFIG-ERROR] " + dae.Message);
                IsCapturing = false;
                return (false, dae.Message);
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

            // Split the packet into individual lines
            string[] inputs = pktString.Split(new string[] { "\n" }, StringSplitOptions.None);
            foreach (string line in inputs)
            {
                // Strip out the garbage that appears in between FSD packets
                string input = Regex.Replace(line, "^.*\\$", "$", RegexOptions.Multiline);
                input = Regex.Replace(input, "^.*#", "#", RegexOptions.Multiline);
                input = Regex.Replace(input, "^.*%", "%", RegexOptions.Multiline);
                input = Regex.Replace(input, "^.*@", "@", RegexOptions.Multiline);

                // Create a FsdPacket object from the cleaned input
                FsdPacket currPacket = new FsdPacket(timestamp, input);

                // Only do something if it is a PM
                if (input.StartsWith("#TM"))
                {
                    FsdMessage input_pm = new FsdMessage(timestamp, input);

                    if (IsForwardMessage(input_pm))
                    {
                        //Check to see if same as last message.  If so, ignore it.
                        if (lastMessage != null)
                        {
                            if (string.Equals(lastMessage.Sender, input_pm.Sender, StringComparison.OrdinalIgnoreCase) &&
                                string.Equals(lastMessage.Recipient, input_pm.Recipient, StringComparison.OrdinalIgnoreCase) &&
                                string.Equals(lastMessage.Message, input_pm.Message, StringComparison.OrdinalIgnoreCase) &&
                                (input_pm.Timestamp - lastMessage.Timestamp).TotalSeconds < 2) // within 2 seconds
                            {
                                logger.Log("Duplicate message detected within 2 seconds.  Ignoring.");
                                continue; // skip processing this duplicate message
                            }
                        }
                        string loggingString = String.Format("{0} > {1} ({2}):\"{3}\" ",
                                                        input_pm.Sender,
                                                        input_pm.Recipient,
                                                        input_pm.Timestamp.ToUniversalTime(),
                                                        input_pm.Message);

                        try
                        {
                            am?.ForwardMessage(input_pm);
                            lastMessage = input_pm;
                        }
                        catch (Exception ex)
                        {
                            // logger IS static on DWMBClient, so record the failure here
                            // instead of dropping it silently. The message was not
                            // delivered to the server; note the affected message.
                            logger.Log("[FORWARD-ERROR] Failed to forward message (" + loggingString + "): " + ex.Message);
                        }

                    }
                }
            }
        }


        /// <summary>
        /// Helper method for determining if a FsdMessage should be forwarded.
        /// </summary>
        /// <param name="msg">The FsdMessage in question</param>
        /// <returns>True if it should be forwarded, False otherwise</returns>
        private static bool IsForwardMessage(FsdMessage msg)
        {
            // Under-the-hood ones to SERVER/FP/DATA...
            bool isServerMessage =
                string.Equals(msg.Sender, "server", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(msg.Recipient, "server", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(msg.Recipient, "fp", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(msg.Recipient, "data", StringComparison.OrdinalIgnoreCase)
                ;

            // on-frequency and private messages addressed to the user...

            // (NOTE: using string.StartsWith() results in partial matches (e.g. UAL1/UAL123), so use regex instead)
            // Regex: ^{callsign}( |,).*
            Regex frequencyMessagePattern = new Regex("^" + callsign + @"( |,).*", RegexOptions.IgnoreCase);
            bool isAddressedToUser = frequencyMessagePattern.IsMatch(msg.Message) ||
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
                        device.StopCapture();
                        logger.Log("FSD packet capture stopped.");
                        IsCapturing = false;
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
            if (am.IsRegistered)
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

