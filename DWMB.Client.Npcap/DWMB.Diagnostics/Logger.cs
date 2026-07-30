namespace DWMB_AIO.DWMB.Diagnostics
{
    /// <summary>
    /// Provides simple file-based logging functionality.
    /// Appends timestamped log messages to a specified file.
    /// </summary>
    class Logger
    {
        /// <summary>
        /// The filename where log messages will be written.
        /// Defaults to "log.txt" if not specified.
        /// </summary>
        private string Filename = "log.txt";

        /// <summary>
        /// Initializes a new instance of the Logger class with the default log file ("log.txt").
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
            System.IO.File.AppendAllLines(Filename, new string[] { logMessage });
        }
    }
}
