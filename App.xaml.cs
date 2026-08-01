using System.Windows;
using System.Windows.Threading;
using DWMB_AIO.DWMB.Diagnostics;

namespace DWMB_AIO
{
    /// <summary>
    /// Interaction logic for App.xaml
    /// </summary>
    public partial class App : Application
    {
        private readonly Logger logger = new(); // Default log file "log.txt"

        public App()
        {
            // Defense in depth: catch anything that escapes a handler so a stray
            // exception surfaces as a dialog + log entry instead of silently killing
            // the process. See issue #6 (unhandled exception on the pcap thread).
            DispatcherUnhandledException += OnDispatcherUnhandledException;
            AppDomain.CurrentDomain.UnhandledException += OnDomainUnhandledException;
        }

        private void OnDispatcherUnhandledException(object sender, DispatcherUnhandledExceptionEventArgs e)
        {
            logger.Log("[UNHANDLED-UI] " + e.Exception);
            MessageBox.Show(
                "An unexpected error occurred:\n\n" + e.Exception.Message +
                "\n\nThe application will try to continue. See log.txt for details.",
                "DWMB - Unexpected Error", MessageBoxButton.OK, MessageBoxImage.Error);

            // Mark as handled so the UI thread keeps running instead of tearing down.
            e.Handled = true;
        }

        private void OnDomainUnhandledException(object sender, UnhandledExceptionEventArgs e)
        {
            // Exceptions from non-UI threads (e.g. the SharpPcap capture thread) land
            // here. We can't reliably keep running, but we can at least record why.
            if (e.ExceptionObject is Exception ex)
            {
                logger.Log("[UNHANDLED-DOMAIN] IsTerminating=" + e.IsTerminating + " " + ex);
            }
            else
            {
                logger.Log("[UNHANDLED-DOMAIN] IsTerminating=" + e.IsTerminating + " (non-Exception)");
            }
        }
    }

}
