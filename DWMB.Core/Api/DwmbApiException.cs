using System;

namespace DWMB.Core.Api
{
    /// <summary>Errors from DWMB API operations.</summary>
    public class DwmbApiException : Exception
    {
        public DwmbApiException() { }

        public DwmbApiException(string message) : base(message) { }

        public DwmbApiException(string message, Exception inner) : base(message, inner) { }
    }
}
