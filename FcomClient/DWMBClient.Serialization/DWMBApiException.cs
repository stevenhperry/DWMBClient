using System;

namespace DWMBClient.Serialization
{
	class DWMBApiException : Exception
	{
		public DWMBApiException()
		{
		}

		public DWMBApiException(string message) : base(message)
		{
		}

		public DWMBApiException(string message, Exception inner) : base(message, inner)
		{
		}

	}
}
