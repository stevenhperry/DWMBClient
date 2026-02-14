using SharpPcap;
using System.Net;

namespace DWMB_AIO.DWMB.FsdDetection
{
    /// <summary>
    /// Represents a hardware network device detected by SharpPcap.
    /// Extracts and stores device information such as friendly name, description, MAC address, and associated IP addresses.
    /// </summary>
    class HardwareDevice
    {
        /// <summary>
        /// The user-friendly name of the device.
        /// </summary>
        public string FriendlyName { get; }

        /// <summary>
        /// The description of the device.
        /// </summary>
        public string Description { get; }

        /// <summary>
        /// The MAC address of the device.
        /// </summary>
        public string MacAddress { get; }

        /// <summary>
        /// The list of IP addresses associated with the device.
        /// </summary>
        public List<string> IpAddresses { get; }

        /// <summary>
        /// The underlying SharpPcap device instance.
        /// </summary>
        public SharpPcap.ICaptureDevice Device { get; set; }

        /// <summary>
        /// Initializes a new instance of the <see cref="HardwareDevice"/> class,
        /// extracting device details from the provided SharpPcap device.
        /// </summary>
        /// <param name="dev">The SharpPcap device to wrap and extract information from.</param>
        public HardwareDevice(ICaptureDevice dev)
        {
            this.Device = dev;
            string input = dev.ToString();
            IpAddresses = new List<string>();

            // Gather all local IP addresses for comparison
            List<string> detectedAddrs = new List<string>();
            IPAddress[] localIps = Dns.GetHostAddresses(Dns.GetHostName());

            foreach (IPAddress ip in localIps)
            {
                string addr = ip.ToString();

                // Remove any scope ID (e.g., for IPv6 addresses)
                int percentSignPos = addr.IndexOf('%');
                if (percentSignPos > 0)
                {
                    addr = addr.Substring(0, percentSignPos);
                }

                detectedAddrs.Add(addr);
            }

            // Parse the device's string representation line by line
            string[] deviceFields = input.Split(new string[] { "\n" }, StringSplitOptions.RemoveEmptyEntries);
            foreach (string line in deviceFields)
            {
                if (line.StartsWith("FriendlyName"))
                {
                    this.FriendlyName = line.Replace("FriendlyName: ", "");
                }
                else if (line.StartsWith("Description"))
                {
                    this.Description = line.Replace("Description: ", "");
                }
                // Parse IP or MAC address fields
                else if (line.StartsWith("Addr:"))
                {
                    string detectedIp = line.Replace("Addr:      ", "");

                    // If this is a MAC address
                    if (detectedIp.Contains("HW addr: "))
                        MacAddress = detectedIp.Replace("HW addr: ", "");

                    // If this is an IP address that matches a detected local address
                    if (detectedAddrs.Contains(detectedIp))
                        IpAddresses.Add(detectedIp);
                }
            }
        }
    }
}
