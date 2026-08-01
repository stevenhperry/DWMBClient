using System.Windows;
using System.Windows.Input;
using DWMB_AIO.DWMB.FsdDetection;
using SharpPcap;

namespace DWMB_AIO
{
    /// <summary>
    /// Modal dialog that lets the user pick which capture device (network adapter)
    /// to sniff when more than one candidate is present. Replaces the old
    /// Console-based prompt that froze the GUI in a WPF app (issue #4).
    /// </summary>
    public partial class DeviceSelectionWindow : Window
    {
        /// <summary>
        /// The device the user selected, or null if the dialog was cancelled.
        /// </summary>
        public ICaptureDevice? SelectedDevice { get; private set; }

        /// <summary>
        /// Wraps a <see cref="HardwareDevice"/> with a human-readable display string
        /// for the list box.
        /// </summary>
        private sealed class DeviceItem
        {
            public HardwareDevice Hardware { get; }
            public string Display { get; }

            public DeviceItem(HardwareDevice hardware)
            {
                Hardware = hardware;
                string ips = hardware.IpAddresses.Count > 0
                    ? string.Join(", ", hardware.IpAddresses)
                    : "no IP";
                string name = string.IsNullOrWhiteSpace(hardware.FriendlyName)
                    ? hardware.Description
                    : hardware.FriendlyName;
                Display = $"{name}  —  {hardware.Description}  ({ips})";
            }
        }

        // internal (not public) because HardwareDevice is an internal type; the dialog
        // is only ever constructed from within this assembly (DWMBClient.BeginCapture).
        internal DeviceSelectionWindow(IReadOnlyList<HardwareDevice> devices)
        {
            InitializeComponent();

            foreach (HardwareDevice hd in devices)
            {
                lstDevices.Items.Add(new DeviceItem(hd));
            }

            if (lstDevices.Items.Count > 0)
            {
                lstDevices.SelectedIndex = 0;
            }
        }

        private void Confirm()
        {
            if (lstDevices.SelectedItem is DeviceItem item)
            {
                SelectedDevice = item.Hardware.Device;
                DialogResult = true;
                Close();
            }
            else
            {
                MessageBox.Show(this, "Please select an adapter first.", "DWMB",
                    MessageBoxButton.OK, MessageBoxImage.Information);
            }
        }

        private void btnOk_Click(object sender, RoutedEventArgs e) => Confirm();

        private void lstDevices_MouseDoubleClick(object sender, MouseButtonEventArgs e)
        {
            if (lstDevices.SelectedItem is DeviceItem)
            {
                Confirm();
            }
        }

        private void btnCancel_Click(object sender, RoutedEventArgs e)
        {
            SelectedDevice = null;
            DialogResult = false;
            Close();
        }
    }
}
