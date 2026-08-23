namespace DWMB_AIO.DWMB.Diagnostics
{
    /// <summary>
    /// Provides simple file-based logging functionality.
    /// Appends timestamped log messages to a specified file.
    /// </summary>
    class Logger
    {
        /// <summary>
        /// Default log file: log.txt under the current user's local (non-roaming) app
        /// data folder. The exe installs to Program Files, which a standard (non-admin)
        /// user can't write to, so a relative "log.txt" resolved against the working
        /// directory failed there; %LOCALAPPDATA% is always writable by the current user
        /// without elevation and is the conventional home for per-user logs/cache that
        /// shouldn't roam between machines.
        /// </summary>
        private static readonly string DefaultLogPath = System.IO.Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "DontWallopMeBro", "log.txt");

        /// <summary>
        /// The filename where log messages will be written.
        /// Defaults to <see cref="DefaultLogPath"/> if not specified.
        /// </summary>
        private string Filename = DefaultLogPath;

        /// <summary>The full path currently being logged to, for surfacing in error dialogs.</summary>
        public string FilePath => Filename;

        /// <summary>
        /// Initializes a new instance of the Logger class with the default log file.
        /// </summary>
        public Logger()
        { }

        /// <summary>
        /// Initializes a new instance of the Logger class with a specified log file.
        /// </summary>
        /// <param name="filename">The name of the file to which log messages will be written.</param>
        public Logger(string filename)
        {
            this.Filename = filename;
        }

        /// <summary>
        /// Appends a timestamped message to the log file.
        /// </summary>
        /// <param name="msg">The message to log.</param>
        public void Log(string msg)
        {
            DateTime timestamp = DateTime.UtcNow;
            string logMessage = string.Format("{0}: {1}", timestamp.ToString("u"), msg);

            string? dir = System.IO.Path.GetDirectoryName(Filename);
            if (!string.IsNullOrEmpty(dir))
            {
                System.IO.Directory.CreateDirectory(dir);
            }

            System.IO.File.AppendAllLines(Filename, new string[] { logMessage });
        }
    }
}
