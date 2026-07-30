using System;
using System.Text.RegularExpressions;

namespace DWMB.Core
{
    /// <summary>
    /// Message-forwarding decision logic, shared by all three adapters. Ported from
    /// v1's DWMBClient.IsForwardMessage (MainWindow.xaml.cs:320-342) and its 2-second
    /// dedupe check (MainWindow.xaml.cs:282-291) -- callsign is now an explicit
    /// parameter instead of a static field read, since this must work for three
    /// concurrent adapter instances instead of one static orchestrator.
    /// </summary>
    public static class MessageFilter
    {
        public static bool IsForwardable(string sender, string receiver, string message, string callsign)
        {
            bool isServerMessage =
                string.Equals(sender, "server", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(receiver, "server", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(receiver, "fp", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(receiver, "data", StringComparison.OrdinalIgnoreCase);

            // NOTE: string.StartsWith() would give partial matches (e.g. UAL1 matching
            // inside UAL123), so this uses a regex anchored at the start instead.
            // Regex.Escape guards against a callsign containing regex metacharacters --
            // v1 interpolated the callsign unescaped, harmless there only because its
            // callsign-input validation regex admits no metacharacters.
            var addressedPattern = new Regex("^" + Regex.Escape(callsign) + @"( |,).*", RegexOptions.IgnoreCase);
            bool isAddressedToUser = addressedPattern.IsMatch(message) ||
                                     string.Equals(receiver, callsign, StringComparison.OrdinalIgnoreCase);

            bool isSelfMessage = string.Equals(sender, callsign, StringComparison.OrdinalIgnoreCase);

            return !isServerMessage && isAddressedToUser && !isSelfMessage;
        }

        public static bool IsDuplicate(RelayMessage? last, RelayMessage current, double windowSeconds = 2.0)
        {
            if (last == null)
            {
                return false;
            }

            return string.Equals(last.Sender, current.Sender, StringComparison.OrdinalIgnoreCase) &&
                   string.Equals(last.Receiver, current.Receiver, StringComparison.OrdinalIgnoreCase) &&
                   string.Equals(last.Message, current.Message, StringComparison.OrdinalIgnoreCase) &&
                   (current.Timestamp - last.Timestamp).TotalSeconds < windowSeconds;
        }

        /// <summary>
        /// Maps a plugin-reported radio frequency to the wire's "@xxyyy" convention
        /// (e.g. 122.800 MHz -> "@22800"), the inverse of the server's
        /// `receiver.replace('@','1')[:3] + '.' + receiver[3:]`.
        ///
        /// Hz is confirmed for xPilot: its official plugin SDK source
        /// (xpilot-project/plugin-sdk, Events/RadioMessageReceivedEventArgs.cs) documents
        /// `Frequencies` explicitly as "in Hz (e.g. 123725000 for 123.725 MHz)", which is
        /// what this method assumes. vPilot's own SDK docs only say `Frequencies (int[])`
        /// without stating units -- not independently confirmed for vPilot specifically,
        /// though the two SDKs are otherwise near-identical in shape. Per plan, this stays
        /// unverified against a real *running* session (docs can be stale) -- confirm
        /// against a live RadioMessageReceived event from each host before fully trusting
        /// this in production; see the corresponding skip-marked test in DWMB.Core.Tests.
        /// </summary>
        public static string FreqTag(int[] frequencies)
        {
            if (frequencies == null || frequencies.Length == 0)
            {
                throw new ArgumentException("At least one frequency is required.", nameof(frequencies));
            }

            int khz = frequencies[0] / 1000;                 // 122800000 Hz -> 122800
            string sixDigits = khz.ToString().PadLeft(6, '0'); // "122800"
            string last5 = sixDigits.Substring(1);             // drop the leading "1" MHz digit -> "22800"
            return "@" + last5;
        }
    }
}
