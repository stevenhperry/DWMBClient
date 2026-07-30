using DWMB_AIO.DWMB.Diagnostics;
using System.Diagnostics;
using System.Windows;
using System.Windows.Media;

namespace DWMB_AIO
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml. UI glue only -- all session/orchestration
    /// logic lives in ClientOrchestrator, and packet capture in FsdCaptureSource. v1
    /// mixed all three into this file's static DWMBClient class; see those two files'
    /// doc comments for what moved where and why.
    /// </summary>
    public partial class MainWindow : Window
    {
        private readonly ClientOrchestrator _orchestrator;

        public MainWindow()
        {
            InitializeComponent();
            _orchestrator = new ClientOrchestrator(new Logger());
            UpdateStatus(_orchestrator.IsRegistered, _orchestrator.IsCapturing);
        }

        private async void btnStart_Click(object sender, RoutedEventArgs e)
        {
            string callsign = txtCallsign.Text;
            string regCode = txtRegCode.Text;

            btnStart.IsEnabled = false;
            try
            {
                var (success, error) = await _orchestrator.StartAsync(callsign, regCode);

                if (!success)
                {
                    MessageBox.Show(error ?? "Unknown error during startup.", "DWMB - Error", MessageBoxButton.OK, MessageBoxImage.Error);
                    return;
                }

                LockInputs();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Unexpected error while starting DWMB client: {ex.Message}", "DWMB - Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
            finally
            {
                btnStart.IsEnabled = true;
                UpdateStatus(_orchestrator.IsRegistered, _orchestrator.IsCapturing);
            }
        }

        private void btnStop_Click(object sender, RoutedEventArgs e)
        {
            if (_orchestrator.Stop())
            {
                MessageBox.Show("Successfully Stopped Capture.", "DWMB - Caution", MessageBoxButton.OK, MessageBoxImage.Information);
            }
            else
            {
                MessageBox.Show("You are not capturing.  There is nothing to stop.", "DWMB - Caution", MessageBoxButton.OK, MessageBoxImage.Warning);
                // still registered, so inputs stay locked
            }
            UpdateStatus(_orchestrator.IsRegistered, _orchestrator.IsCapturing);
        }

        private async void btnDeregister_Click(object sender, RoutedEventArgs e)
        {
            _orchestrator.Stop();

            var (success, error) = await _orchestrator.DeregisterAsync();
            if (success)
            {
                MessageBox.Show("Successfully deregistered and stopped capturing!");
                UnlockInputs();
            }
            else
            {
                MessageBox.Show(
                    error ?? "Capture stopped.  However, de-registration failed.",
                    "DWMB - Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }

            UpdateStatus(_orchestrator.IsRegistered, _orchestrator.IsCapturing);
        }

        private async void btnTest_Click(object sender, RoutedEventArgs e)
        {
            bool ok = await _orchestrator.TestConnectionAsync();
            MessageBox.Show(string.Format("Connection test response: {0}", ok), "DWMB - Test");
        }

        private void UpdateStatus(bool? statusRegistration, bool? statusCapturing)
        {
            txtStatusRegister.Text = string.Format("Registered: {0}", statusRegistration);
            txtStatusCapture.Text = string.Format("Capturing: {0}", statusCapturing);

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
}
