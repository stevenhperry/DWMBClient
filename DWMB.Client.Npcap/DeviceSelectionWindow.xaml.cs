using System.Collections.Generic;
using System.Linq;
using System.Windows;
using DWMB_AIO.DWMB.FsdDetection;

namespace DWMB_AIO
{
    /// <summary>
    /// Replaces v1's Console.ReadLine()-based device picker
    /// (MainWindow.xaml.cs:412-433's TODO) with a proper WPF dialog.
    /// </summary>
    public partial class DeviceSelectionWindow : Window
    {
        private sealed class DeviceItem
        {
            public required HardwareDevice Device { get; init; }
            public required string DisplayLabel { get; init; }
        }

        public HardwareDevice? SelectedDevice { get; private set; }

        public DeviceSelectionWindow(IReadOnlyList<HardwareDevice> devices)
        {
            InitializeComponent();

            lstDevices.ItemsSource = devices
                .Select(d => new DeviceItem
                {
                    Device = d,
                    DisplayLabel = $"{d.FriendlyName} - {d.Description} - {string.Join(", ", d.IpAddresses)}",
                })
                .ToList();

            if (lstDevices.Items.Count > 0)
            {
                lstDevices.SelectedIndex = 0;
            }
        }

        private void btnOk_Click(object sender, RoutedEventArgs e)
        {
            if (lstDevices.SelectedItem is DeviceItem item)
            {
                SelectedDevice = item.Device;
                DialogResult = true;
            }
        }

        private void btnCancel_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = false;
        }
    }
}
