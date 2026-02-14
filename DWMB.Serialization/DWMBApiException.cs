namespace DWMB_AIO.DWMB.Serialization
{
    /// <summary>
    /// Represents errors that occur during DWMB API operations.
    /// </summary>
    class DWMBApiException : Exception
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="DWMBApiException"/> class.
        /// </summary>
        public DWMBApiException()
        { }

        /// <summary>
        /// Initializes a new instance of the <see cref="DWMBApiException"/> class with a specified error message.
        /// </summary>
        /// <param name="message">The message that describes the error.</param>
        public DWMBApiException(string message)
            : base(message)
        { }

        /// <summary>
        /// Initializes a new instance of the <see cref="DWMBApiException"/> class with a specified error message and a reference to the inner exception that is the cause of this exception.
        /// </summary>
        /// <param name="message">The error message that explains the reason for the exception.</param>
        /// <param name="inner">The exception that is the cause of the current exception.</param>
        public DWMBApiException(string message, Exception inner)
            : base(message, inner)
        { }

    }
}
