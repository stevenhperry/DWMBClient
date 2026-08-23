namespace DWMB_AIO.DWMB.FsdDetection
{
    /// <summary>
    /// Detects whether a libpcap-compatible capture driver (Npcap, or the deprecated
    /// WinPcap) is installed and loadable. SharpPcap has no dedicated "is Npcap
    /// installed" API, so this relies on the same thing SharpPcap itself depends on:
    /// without a driver, the native wpcap.dll can't be found, and enumerating capture
    /// devices throws a DllNotFoundException (see dotpcap/sharppcap issues #447, #477,
    /// #489 for the same failure other SharpPcap users hit).
    /// </summary>
    static class PcapDriverCheck
    {
        public const string DownloadUrl = "https://npcap.com/#download";

        /// <summary>
        /// Attempts to enumerate capture devices to see whether a driver responds.
        /// Returns true if one is present and loadable; false (with the causing
        /// exception's message in <paramref name="errorDetail"/>) otherwise.
        /// </summary>
        public static bool IsAvailable(out string? errorDetail)
        {
            try
            {
                _ = SharpPcap.CaptureDeviceList.Instance;
                errorDetail = null;
                return true;
            }
            catch (Exception ex)
            {
                errorDetail = ex.Message;
                return false;
            }
        }

        /// <summary>
        /// User-facing explanation and install steps for a missing/unloadable driver.
        /// </summary>
        public static string BuildMissingDriverMessage(string? errorDetail)
        {
            string detail = string.IsNullOrWhiteSpace(errorDetail) ? "" : $"\n\nDetails: {errorDetail}";
            return
                "DWMB could not find a packet capture driver (Npcap) on this computer.\n\n" +
                "DWMB needs Npcap to read VATSIM network traffic — without it, packet capture cannot start.\n\n" +
                "To fix this:\n" +
                $"1. Download Npcap from {DownloadUrl}\n" +
                "2. Run the installer. Enabling \"Install Npcap in WinPcap API-compatible Mode\" is the safest option.\n" +
                "3. Restart DWMB after installing." +
                detail;
        }
    }
}
