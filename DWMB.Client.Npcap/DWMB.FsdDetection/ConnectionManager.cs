using SharpPcap;
using System.Net;

namespace DWMB_AIO.DWMB.FsdDetection
{
    /// <summary>
    /// Manages network connections by detecting and storing relevant hardware devices.
    /// Filters out devices that do not have associated IP addresses.
    /// </summary>
    class ConnectionManager
    {
        /// <summary>
        /// List of detected hardware devices (network connections) that are relevant (i.e., have IP addresses).
        /// </summary>
        public List<HardwareDevice> Connections { get; set; }

        /// <summary>
        /// Initializes a new instance of the <see cref="ConnectionManager"/> class
        /// and populates the <see cref="Connections"/> list with relevant devices.
        /// </summary>
        public ConnectionManager()
        {
            Connections = new List<HardwareDevice>();
            Filter();
        }

        /// <summary>
        /// Helper method for filtering out irrelevant connections.
        /// Only devices with at least one associated IP address are added to the Connections list.
        /// </summary>
        private void Filter()
        {
            // Get the list of all capture devices on the system
            CaptureDeviceList devices = CaptureDeviceList.Instance;

            // Get all local IP addresses for filtering
            IPAddress[] localIps = Dns.GetHostAddresses(Dns.GetHostName());

            int candidate = -1;
            int i = 0;

            // Iterate through each capture device
            foreach (ICaptureDevice device in devices)
            {
                if (device != null)
                {
                    string deviceDescription = device?.ToString() ?? string.Empty;
                    HardwareDevice h = new HardwareDevice(device);
                    h.Device = device;

                    // Only add devices that have at least one IP address
                    if (h.IpAddresses.Count > 0)
                    {
                        Connections.Add(new HardwareDevice(device));
                        candidate = i;
                    }
                    i++;
                }
            }
        }
    }
}
