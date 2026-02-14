using DWMB_AIO.DWMB.FsdObjects;
using RestSharp;

namespace DWMB_AIO.DWMB.Serialization
{
    class MessageForwarder
    {
        private readonly string SERVER_ADDRESS = System.IO.File.ReadAllText("server_location.txt");

        private readonly string MESSAGE_FORWARDING_ENDPOINT = "messaging";


        /// <summary>
        /// API registration token.  Not implemented yet.  Just a date now.
        /// </summary>
        private string TOKEN = "20251001";

        private RestClient client;

        public MessageForwarder()
        {
            client = new RestClient(SERVER_ADDRESS);
        }


        public void UploadMessage(FsdMessage pm)
        {
            // convert timestamp to unix time
            DateTimeOffset dateTimeOffset = new DateTimeOffset(pm.Timestamp);
            long unixTimestamp = dateTimeOffset.ToUnixTimeSeconds();

            string json = string.Format(
                "{{\"privateMessage\":{{\"token\":\"{0}\",\"timestamp\":\"{1}\",\"sender\":\"{2}\",\"receiver\":\"{3}\",\"message\":\"{4}\"}}}}",
                TOKEN, unixTimestamp, pm.Sender, pm.Recipient, pm.Message);

            // to do - have this in a window of the GUI?
            Console.WriteLine("-- Forwarding to " + SERVER_ADDRESS);
            Console.WriteLine("-- " + json);

            var request = new RestRequest(MESSAGE_FORWARDING_ENDPOINT, Method.Post);
            request.AddParameter("application/json", json, ParameterType.RequestBody);

            RestResponse response = client.Execute(request);
            var content = response.Content; // raw content as string

            // TO DO - check response for success/failure


        }
    }
}
