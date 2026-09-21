using DWMB_AIO.DWMB.Audio;
using DWMB_AIO.DWMB.Diagnostics;
using DWMB_AIO.DWMB.FsdDetection;
using DWMB_AIO.DWMB.FsdObjects;
using DWMB_AIO.DWMB.Notifications;
using DWMB_AIO.DWMB.Serialization;
using SharpPcap;
using System.Diagnostics;
using System.Text;
using System.Text.RegularExpressions;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Threading;



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

            // Keep the Silence button and taskbar flash in sync with whether the alarm is
            // actually sounding — the flash is tied entirely to the alarm's own start/stop,
            // not raised independently, so it only happens when the (opt-in) alarm sound
            // does, and stops the moment the alarm is silenced.
            DWMBClient.AlarmStateChanged += OnAlarmStateChanged;
            DWMBClient.AlarmSoundEnabled = chkAlarmSound.IsChecked == true; // off by default
            DWMBClient.RepeatDiscordPingEnabled = chkRepeatDiscordPing.IsChecked == true; // off by default

            // Stash the XAML tooltips so SyncAlarmUi can swap in the "register first"
            // explanation while the alarm group is locked, and put these back once it isn't.
            alarmSoundToolTip = chkAlarmSound.ToolTip;
            repeatDiscordPingToolTip = chkRepeatDiscordPing.ToolTip;
            silenceAlarmToolTip = btnSilenceAlarm.ToolTip;

            // Also runs SyncAlarmUi, so the alarm group starts out locked: at launch nothing
            // is registered and nothing is being forwarded.
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

            // Every registration/capture transition comes through here, so this is the one
            // place the alarm group's lock needs driving from (see AlarmControlsAvailable).
            SyncAlarmUi();
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

        // Silence button colors for the "Disarmed"/"Set" states (the "Sounding" state
        // alternates between AlarmSoundingBrush and Brushes.Transparent — see
        // AlarmFlashTimer_Tick). Frozen so they're cheap to reuse on every UI update.
        private static readonly SolidColorBrush AlarmDisarmedBrush = FrozenBrush(0xFF, 0xC1, 0x07); // cautionary amber/yellow
        private static readonly SolidColorBrush AlarmSetBrush = FrozenBrush(0x6B, 0x8E, 0x5A); // muted green
        private static readonly SolidColorBrush AlarmSoundingBrush = FrozenBrush(0xE5, 0x39, 0x35); // alert red
        private static readonly SolidColorBrush AlarmUnavailableBrush = FrozenBrush(0xBD, 0xBD, 0xBD); // neutral grey

        // How far the alarm checkboxes are faded while the group is locked. They can't use
        // IsEnabled for this (see SyncAlarmUi), so the greying is done by hand.
        private const double AlarmLockedOpacity = 0.55;

        // The XAML tooltips, captured at construction so SyncAlarmUi can swap the
        // "register first" explanation in and out without hard-coding copies of them here.
        private object? alarmSoundToolTip;
        private object? repeatDiscordPingToolTip;
        private object? silenceAlarmToolTip;

        // Set while we're programmatically correcting a checkbox, so its Checked/Unchecked
        // handler can tell that write apart from a toggle the user made.
        private bool suppressAlarmCheckboxEvents;

        /// <summary>
        /// Whether the alarm controls are usable. The alarm exists to flag messages DWMB is
        /// forwarding, so it means nothing until the client is both registered and
        /// capturing: pausing puts it back out of reach just as surely as never having
        /// started, since a paused client forwards nothing for the alarm to fire on.
        /// </summary>
        private static bool AlarmControlsAvailable =>
            DWMBClient.IsRegistered == true && DWMBClient.IsCapturing;

        private static SolidColorBrush FrozenBrush(byte r, byte g, byte b)
        {
            var brush = new SolidColorBrush(Color.FromRgb(r, g, b));
            brush.Freeze();
            return brush;
        }

        // Drives the Silence button's red/transparent blink while the alarm is sounding.
        // Only running while sounding — started/stopped in SyncAlarmUi, never left ticking
        // in the other two states.
        private DispatcherTimer? alarmFlashTimer;
        private bool alarmFlashRedPhase;

        /// <summary>
        /// Toggles whether new messages trigger the local alarm sound. Off by default.
        /// Turning it off also silences an alarm that's already sounding, rather than just
        /// suppressing future ones.
        /// </summary>
        private void chkAlarmSound_CheckedChanged(object sender, RoutedEventArgs e)
        {
            if (suppressAlarmCheckboxEvents)
            {
                return; // our own corrective write, not the user's toggle
            }

            if (!AlarmControlsAvailable)
            {
                // Normally unreachable: AlarmControl_Preview* swallow the activation before
                // the box ever toggles. Something that bypasses input events (UI automation,
                // a programmatic set) can still land here, so put the box back rather than
                // arm an alarm whose Silence button is locked out of stopping it.
                RevertAlarmCheckbox(chkAlarmSound, DWMBClient.AlarmSoundEnabled);
                ShowAlarmUnavailableMessage();
                return;
            }

            bool enabled = chkAlarmSound.IsChecked == true;
            DWMBClient.AlarmSoundEnabled = enabled;

            if (!enabled)
            {
                DWMBClient.SilenceAlarm();
            }

            // SilenceAlarm() above only raises AlarmStateChanged (and so re-syncs the UI)
            // when it actually stops a sounding alarm. Flipping the checkbox while nothing
            // is sounding — e.g. arming/disarming ahead of time — needs its own sync so the
            // button still switches between "Disarmed" and "Set".
            SyncAlarmUi();
        }

        /// <summary>
        /// Toggles whether the message that triggered the alarm keeps being re-forwarded to
        /// the server (and so re-pinged on Discord) every 60 seconds until the alarm is
        /// silenced. Off by default, and only selectable while the alarm sound is armed —
        /// see <see cref="SyncAlarmUi"/>.
        /// </summary>
        private void chkRepeatDiscordPing_CheckedChanged(object sender, RoutedEventArgs e)
        {
            if (suppressAlarmCheckboxEvents)
            {
                return; // our own corrective write, not the user's toggle
            }

            if (!AlarmControlsAvailable)
            {
                // See chkAlarmSound_CheckedChanged — same belt-and-braces undo.
                RevertAlarmCheckbox(chkRepeatDiscordPing, DWMBClient.RepeatDiscordPingEnabled);
                ShowAlarmUnavailableMessage();
                return;
            }

            bool enabled = chkRepeatDiscordPing.IsChecked == true;
            DWMBClient.RepeatDiscordPingEnabled = enabled;

            if (!enabled)
            {
                // Turning it off stops a loop already running rather than only suppressing
                // future ones — the same contract as unchecking chkAlarmSound silencing an
                // alarm that's already sounding. Turning it back on does not retroactively
                // arm one; it applies to the next message that triggers the alarm.
                DWMBClient.StopRepeatForward();
            }
        }

        private void btnSilenceAlarm_Click(object sender, RoutedEventArgs e)
        {
            if (!AlarmControlsAvailable)
            {
                // As in the checkbox handlers: the Preview* gate normally gets here first,
                // this covers whatever it can't see.
                ShowAlarmUnavailableMessage();
                return;
            }

            DWMBClient.SilenceAlarm();
        }

        /// <summary>
        /// Restores a checkbox to <paramref name="value"/> without its Checked/Unchecked
        /// handler treating the correction as a fresh user toggle (and recursing).
        /// </summary>
        private void RevertAlarmCheckbox(CheckBox box, bool value)
        {
            suppressAlarmCheckboxEvents = true;
            try
            {
                box.IsChecked = value;
            }
            finally
            {
                suppressAlarmCheckboxEvents = false;
            }
        }

        /// <summary>
        /// Blocks mouse activation of an alarm control while the group is locked, and says
        /// why instead of silently doing nothing. Handling the event in the tunnelling
        /// Preview pass — before CheckBox/Button act on it — means the toggle or click never
        /// happens at all, so there is no state left to undo afterwards. This is also why
        /// the controls aren't simply IsEnabled=false: a disabled WPF element raises no
        /// input events, leaving nothing to hang the explanation off.
        /// </summary>
        private void AlarmControl_PreviewMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            if (AlarmControlsAvailable)
            {
                return;
            }

            e.Handled = true;
            ShowAlarmUnavailableMessage();
        }

        /// <summary>
        /// Keyboard counterpart to <see cref="AlarmControl_PreviewMouseLeftButtonDown"/>:
        /// Space toggles a focused CheckBox and Space/Enter press a focused Button, so the
        /// lock has to cover those as well as the mouse.
        /// </summary>
        private void AlarmControl_PreviewKeyDown(object sender, KeyEventArgs e)
        {
            if (AlarmControlsAvailable || (e.Key != Key.Space && e.Key != Key.Enter))
            {
                return;
            }

            e.Handled = true;
            ShowAlarmUnavailableMessage();
        }

        private void ShowAlarmUnavailableMessage()
        {
            MessageBox.Show(
                this,
                BuildAlarmUnavailableMessage(),
                "DWMB - Alarm Unavailable",
                MessageBoxButton.OK,
                MessageBoxImage.Information);
        }

        /// <summary>
        /// Explains the lock, tailored to which half of "registered and forwarding" is
        /// missing — telling someone who is registered but paused to go and register would
        /// just send them looking for a button that's already been pressed. Doubles as the
        /// tooltip on the locked controls (see <see cref="SyncAlarmUi"/>).
        /// </summary>
        private static string BuildAlarmUnavailableMessage()
        {
            return DWMBClient.IsRegistered == true
                ? "The alarm controls are unavailable because message forwarding is paused.\n\n"
                  + "Click Start to resume forwarding and they will become available again."
                : "You must register before you can use the alarm controls.\n\n"
                  + "Enter your callsign and registration code, then click Start. The alarm only "
                  + "sounds for messages DWMB is actively forwarding, so there is nothing for it "
                  + "to do until you are registered.";
        }

        /// <summary>
        /// Handles <see cref="DWMBClient.AlarmStateChanged"/>, which may fire on the capture
        /// thread — marshal to the UI thread before touching controls (same pattern as
        /// <see cref="OnForwardStatusChanged"/>, issue #8).
        /// </summary>
        private void OnAlarmStateChanged()
        {
            if (Dispatcher.CheckAccess())
            {
                SyncAlarmUi();
            }
            else
            {
                Dispatcher.BeginInvoke(new Action(SyncAlarmUi));
            }
        }

        /// <summary>
        /// Reflects the alarm's state — unavailable / disarmed / armed-but-quiet / sounding
        /// — on the Silence button's text and color, greys out and locks the whole alarm
        /// group while it's unavailable, and drives the taskbar flash (only raised while
        /// sounding, never independently per message, so it tracks the alarm exactly).
        /// The button stays enabled in all four states — including when a click would be a
        /// no-op (SilenceAlarm() no-ops if nothing is sounding) — because WPF's default
        /// disabled-button style would otherwise paint over these custom colors, and the
        /// button's color is itself the point in the Unavailable/Disarmed/Set states.
        /// </summary>
        private void SyncAlarmUi()
        {
            bool available = AlarmControlsAvailable;

            // Never leave an alarm sounding with no way to stop it. The Silence button is
            // locked out below, so an alarm still going as the client leaves the forwarding
            // state (Pause, Deregister, or a capture that died on its own) has to be
            // silenced here — that also stops the repeat-ping loop, which hangs off
            // AlarmPlayer.StateChanged. Silencing re-enters this method via
            // AlarmStateChanged; that's harmless, since the nested pass sees IsAlarmSounding
            // false and settles on exactly the state this one is about to paint.
            if (!available && DWMBClient.IsAlarmSounding)
            {
                DWMBClient.SilenceAlarm();
            }

            bool enabled = DWMBClient.AlarmSoundEnabled;
            bool sounding = DWMBClient.IsAlarmSounding;

            // Greyed by hand rather than with IsEnabled, because a disabled WPF control
            // raises no mouse or key events — there'd be no interaction left for
            // AlarmControl_Preview* to explain the lock from. The checkboxes keep whatever
            // the user last chose while locked; only their reachability changes.
            chkAlarmSound.Opacity = available ? 1.0 : AlarmLockedOpacity;
            chkRepeatDiscordPing.Opacity = available ? 1.0 : AlarmLockedOpacity;

            // Repeating is defined as "until the alarm is silenced", so it only means
            // anything while the alarm is armed. Greyed out rather than unchecked when the
            // alarm is disarmed, so the user's choice survives arming/disarming. While the
            // group is locked it stays IsEnabled for the reason just above — the Preview
            // gate, not IsEnabled, is what makes it inert.
            chkRepeatDiscordPing.IsEnabled = !available || enabled;

            // Hovering a greyed control should say why it's greyed, not describe something
            // it won't currently do.
            object? lockedToolTip = available ? null : BuildAlarmUnavailableMessage();
            chkAlarmSound.ToolTip = lockedToolTip ?? alarmSoundToolTip;
            chkRepeatDiscordPing.ToolTip = lockedToolTip ?? repeatDiscordPingToolTip;
            btnSilenceAlarm.ToolTip = lockedToolTip ?? silenceAlarmToolTip;

            if (!available)
            {
                StopAlarmButtonFlash();
                btnSilenceAlarm.Content = "Alarm - Unavailable";
                btnSilenceAlarm.Background = AlarmUnavailableBrush;
                btnSilenceAlarm.Foreground = Brushes.Black;
            }
            else if (!enabled)
            {
                StopAlarmButtonFlash();
                btnSilenceAlarm.Content = "Alarm - Disarmed";
                btnSilenceAlarm.Background = AlarmDisarmedBrush;
                btnSilenceAlarm.Foreground = Brushes.Black;
            }
            else if (!sounding)
            {
                StopAlarmButtonFlash();
                btnSilenceAlarm.Content = "Alarm - Set";
                btnSilenceAlarm.Background = AlarmSetBrush;
                btnSilenceAlarm.Foreground = Brushes.White;
            }
            else
            {
                btnSilenceAlarm.Content = "Silence Alarm";
                StartAlarmButtonFlash();
            }

            if (sounding)
            {
                // No point flashing the taskbar if the user is already looking at the window.
                if (!IsActive)
                {
                    TaskbarFlasher.Start(this);
                }
            }
            else
            {
                TaskbarFlasher.Stop(this);
            }
        }

        /// <summary>Starts the Silence button's red/transparent 1 Hz blink. Safe to call repeatedly.</summary>
        private void StartAlarmButtonFlash()
        {
            if (alarmFlashTimer != null)
            {
                return; // already flashing — don't reset the phase
            }

            alarmFlashRedPhase = true;
            btnSilenceAlarm.Background = AlarmSoundingBrush;
            btnSilenceAlarm.Foreground = Brushes.White;

            alarmFlashTimer = new DispatcherTimer
            {
                Interval = TimeSpan.FromMilliseconds(500), // half-period of a 1 Hz alternation
            };
            alarmFlashTimer.Tick += AlarmFlashTimer_Tick;
            alarmFlashTimer.Start();
        }

        private void AlarmFlashTimer_Tick(object? sender, EventArgs e)
        {
            alarmFlashRedPhase = !alarmFlashRedPhase;
            btnSilenceAlarm.Background = alarmFlashRedPhase ? AlarmSoundingBrush : Brushes.Transparent;
            btnSilenceAlarm.Foreground = alarmFlashRedPhase ? Brushes.White : Brushes.Black;
        }

        /// <summary>Stops the blink, if running. Safe to call when it's not.</summary>
        private void StopAlarmButtonFlash()
        {
            if (alarmFlashTimer == null)
            {
                return;
            }

            alarmFlashTimer.Stop();
            alarmFlashTimer.Tick -= AlarmFlashTimer_Tick;
            alarmFlashTimer = null;
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
        static Logger logger = new(); // Default log file: %LOCALAPPDATA%\DontWallopMeBro\log.txt
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

        // --- Local alarm sound (in addition to the Discord notification) ---
        // Off by default; toggled from the GUI (chkAlarmSound). A single AlarmPlayer
        // instance for the process lifetime, same pattern as the static `logger`.
        static readonly AlarmPlayer alarmPlayer = new();
        public static bool AlarmSoundEnabled { get; set; } = false;
        public static bool IsAlarmSounding => alarmPlayer.IsSounding;

        /// <summary>Raised whenever the alarm starts/stops sounding. May fire off the UI thread.</summary>
        public static event Action? AlarmStateChanged
        {
            add => alarmPlayer.StateChanged += value;
            remove => alarmPlayer.StateChanged -= value;
        }

        /// <summary>Immediately stops the alarm sound, if it's sounding. Safe to call from the GUI at any time.</summary>
        public static void SilenceAlarm() => alarmPlayer.Silence();

        // --- Repeat Discord ping (chkRepeatDiscordPing) ---
        // Off by default, and only ever armed when the alarm sound is. While the alarm is
        // sounding, the message that started it is re-forwarded to the server every
        // REPEAT_INTERVAL_MS, so Discord keeps pinging until the user acknowledges by
        // silencing the alarm — the Discord-side equivalent of the escalation the local
        // alarm already does. Only the triggering message repeats: messages arriving
        // mid-alarm are forwarded once as usual, and neither replace the repeat target nor
        // reset its clock.
        public static bool RepeatDiscordPingEnabled { get; set; } = false;

        // Guarded by stateLock, like the other statics shared between the capture thread,
        // the UI thread and (now) the repeat timer's thread. Kept deliberately separate
        // from `lastMessage`, which exists only for the 2-second duplicate window and is
        // assigned only on a successful forward.
        static FsdMessage? repeatMessage;
        static System.Threading.Timer? repeatTimer;

        // Bumped on every start and stop. Timer.Dispose() does not wait for a callback
        // that is already running or already queued, so a stopped loop can still get one
        // more tick; each tick compares the generation it was scheduled with against this
        // one and bails if it has been superseded.
        static int repeatGeneration;

        const int REPEAT_INTERVAL_MS = 60_000;

        static DWMBClient()
        {
            // The repeat loop's lifetime is exactly the alarm's. Hooking StateChanged
            // rather than SilenceAlarm() covers all three ways the alarm can end: the
            // Silence button, unchecking chkAlarmSound, and playback stopping on its own
            // (an audio device error). That last case would otherwise leave the loop
            // pinging Discord forever with no UI affordance left to stop it.
            alarmPlayer.StateChanged += () =>
            {
                if (!alarmPlayer.IsSounding)
                {
                    StopRepeatForward();
                }
            };
        }

        /// <summary>
        /// Arms the repeat loop for <paramref name="msg"/> — the message that just started
        /// the alarm. Called on the capture thread.
        /// </summary>
        private static void StartRepeatForward(FsdMessage msg)
        {
            // Idempotent: any previous loop is cancelled first, so a new alarm supersedes
            // an older repeat target rather than stacking timers.
            StopRepeatForward();

            lock (stateLock)
            {
                repeatMessage = msg;
                int generation = ++repeatGeneration;

                // dueTime only, period Infinite: each tick re-arms itself once its forward
                // has returned, so two ticks can never overlap if a POST hangs
                // (ForwardMessage is synchronous, with no timeout override).
                repeatTimer = new System.Threading.Timer(
                    RepeatForwardTick, generation, REPEAT_INTERVAL_MS, System.Threading.Timeout.Infinite);
            }

            // Logged outside the lock — stateLock is never held across file or network I/O.
            logger.Log(String.Format(
                "[REPEAT] Repeat Discord ping armed for \"{0} > {1}\"; re-forwarding every {2}s until the alarm is silenced.",
                msg.Sender, msg.Recipient, REPEAT_INTERVAL_MS / 1000));

            // The alarm can have been silenced between Trigger() and here: the initial
            // forward sits in between, and ForwardMessage is a synchronous POST, so that
            // window is seconds wide rather than microseconds. A Silence() inside it ran
            // its StateChanged handler before this method armed anything, so that handler
            // missed us and we'd be left repeating with no alarm left to stop us.
            // Re-checking after arming closes the window from the other side: either we
            // see the alarm already gone and stand down here, or we armed first and that
            // handler stops us. The log then reads armed-then-stopped, which is accurate.
            if (!alarmPlayer.IsSounding)
            {
                StopRepeatForward();
            }
        }

        /// <summary>
        /// One tick of the repeat loop: re-forwards the retained triggering message, then
        /// re-arms. Runs on a thread-pool thread owned by <see cref="repeatTimer"/>.
        /// </summary>
        private static void RepeatForwardTick(object? state)
        {
            // An exception escaping a System.Threading.Timer callback is unhandled on a
            // background thread and terminates the process — the same hazard as the capture
            // callback (issue #6) — so the whole body is guarded.
            try
            {
                int generation = state is int g ? g : -1;

                ApiManager? currentAm;
                FsdMessage? msg;
                lock (stateLock)
                {
                    if (generation != repeatGeneration)
                    {
                        // Superseded or stopped while this callback was queued.
                        return;
                    }

                    // `am` is read live each tick rather than captured when the loop was
                    // armed, so a Pause/Start cycle (which builds a fresh ApiManager) is
                    // picked up without the user having to re-trigger the alarm.
                    currentAm = am;
                    msg = repeatMessage;
                }

                if (currentAm == null || msg == null)
                {
                    StopRepeatForward();
                    return;
                }

                try
                {
                    // Forward outside the lock — never hold it across network I/O.
                    currentAm.ForwardMessage(msg);
                    logger.Log(String.Format("[REPEAT] Re-forwarded unacknowledged message {0} > {1}: \"{2}\"",
                                             msg.Sender, msg.Recipient, msg.Message));
                }
                catch (Exception ex)
                {
                    // Surface repeat failures in the forwarding-health indicator too, so a
                    // repeat loop silently failing against the server is visible (issue #8).
                    logger.Log("[REPEAT-ERROR] Failed to re-forward the unacknowledged message: " + ex.Message);
                    RecordForwardFailure(ex.Message);
                }

                lock (stateLock)
                {
                    // Only re-arm if this generation is still the live one — the alarm may
                    // have been silenced while the forward above was in flight.
                    if (generation == repeatGeneration)
                    {
                        repeatTimer?.Change(REPEAT_INTERVAL_MS, System.Threading.Timeout.Infinite);
                    }
                }
            }
            catch (Exception ex)
            {
                try { logger.Log("[REPEAT-ERROR] Repeat tick failed (ignored): " + ex); } catch { }
            }
        }

        /// <summary>
        /// Stops the repeat loop and forgets the retained message. Safe to call from any
        /// thread, and when no loop is running.
        /// </summary>
        public static void StopRepeatForward()
        {
            System.Threading.Timer? toDispose;

            lock (stateLock)
            {
                // Invalidate any callback that is already running or queued before the
                // timer goes away — Dispose() on its own does not wait for one.
                repeatGeneration++;
                toDispose = repeatTimer;
                repeatTimer = null;
                repeatMessage = null;
            }

            if (toDispose == null)
            {
                return; // nothing was running
            }

            try { toDispose.Dispose(); } catch { }
            logger.Log("[REPEAT] Repeat Discord ping stopped.");
        }

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
                    // Log which server this build/session is actually configured to hit.
                    // Not a secret (already recoverable from the compiled assembly) and
                    // essential for diagnosing a build that shipped with the ServerConfig
                    // placeholder URL instead of the real one (issue: silent placeholder builds).
                    logger.Log($"[INFO] Server configured ({environment}): {newAm.ServerAddress}");
                    lock (stateLock)
                    {
                        callsign = strCallsignInput;
                        am = newAm;
                    }

                    newAm.Register(strRegCode, strCallsignInput);


                    if (!newAm.IsRegistered)  //if the registration is not successful
                    {
                        logger.Log("[DWMB_API_ERROR] - Client failed to register with the server. " + (newAm.LastRegistrationError ?? "(no additional detail)"));
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

                        // Local alarm (which also drives the taskbar flash via
                        // AlarmStateChanged, see MainWindow.SyncAlarmUi) is independent of
                        // server forwarding (issue: notify even if the network call below
                        // fails or is slow) and must never take the capture thread down.
                        // Trigger() reports whether this call actually started the alarm
                        // (it no-ops while one is already sounding). That is what picks out
                        // the single message which keeps re-pinging Discord below.
                        bool alarmStarted = false;
                        if (AlarmSoundEnabled)
                        {
                            try
                            {
                                alarmStarted = alarmPlayer.Trigger();
                            }
                            catch (Exception ex)
                            {
                                logger.Log("[ALARM-ERROR] Failed to play alarm sound: " + ex.Message);
                            }
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

                        // This message started the alarm, so it's the one that keeps
                        // pinging Discord until the user acknowledges. Armed whether or not
                        // the forward above succeeded — if it failed, the repeat doubles as
                        // a retry. alarmStarted can only be true when AlarmSoundEnabled is,
                        // so "repeat requires the alarm sound" holds structurally, not just
                        // because the GUI greys the checkbox out.
                        if (alarmStarted && RepeatDiscordPingEnabled)
                        {
                            StartRepeatForward(input_pm);
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
            // Deregistering is the user's intent to disconnect, so the repeat loop stops
            // here whatever the outcome below — including when deregistration fails or the
            // client wasn't registered. Pause (Stop()) deliberately leaves it running: the
            // registration is still live there, same as the alarm sound itself.
            StopRepeatForward();

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

                string message =
                    "No suitable network adapter was found. Make sure Npcap (or WinPcap) is installed " +
                    "and you have an active network connection, then try Start again.";

                // Npcap installed with "Restrict driver's access to Administrators only"
                // hides every adapter from a non-elevated process instead of erroring, so
                // this looks identical to "no driver"/"no network" unless we call it out.
                if (!PcapDriverCheck.IsRunningElevated())
                {
                    message +=
                        "\n\nIf Npcap was installed with \"Restrict Npcap driver's access to " +
                        "Administrators only,\" try running DWMB as Administrator.";
                }

                throw new DWMBApiException(message);
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

