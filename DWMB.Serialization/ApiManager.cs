using DWMB_AIO.DWMB.Serialization.ApiObjects;
using RestSharp;
using System.Diagnostics.CodeAnalysis;

namespace DWMB_AIO.DWMB.Serialization
{
    class ApiManager
    {
        //private readonly string CLIENT_VERSION = "DWMBClient/0.11.01";

        // Using AppInfo to get version info
        private readonly string CLIENT_VERSION = $"DWMBClient/{DWMB_AIO.AppInfo.DisplayVersion}";

        private readonly string SERVER_ADDRESS = System.IO.File.ReadAllText("server_location.txt");
        private readonly string MESSAGE_FORWARDING_ENDPOINT = "/api/v1/messaging";
        private readonly string REGISTRATION_ENDPOINT = "/api/v1/register";
        private readonly string DEREGISTRATION_ENDPOINT = "/api/v1/deregister";
        private readonly string TEST_ENDPOINT = "/api/v1/test";
        public required string Token { get; set; }
        public required string Callsign { get; set; }

        public long DiscordId { get; set; }

        public string? DiscordName { get; set; }
        private RestSharp.RestClient client;
        public bool IsRegistered { get; set; } = false;
        public bool IsCapturing { get; set; } = false;

        [SetsRequiredMembers]
        public ApiManager(string token, string callsign)
        {
            this.Token = token;
            this.Callsign = callsign;
            this.IsRegistered = false;

            // Set up RestSharp client for use by other functions later
            var options = new RestClientOptions(SERVER_ADDRESS)
            {
                UserAgent = CLIENT_VERSION
            };

            client = new RestSharp.RestClient(SERVER_ADDRESS);
            client = new RestClient(options);

        }


        /// <summary>
        /// Registration method to register the client with the DWMB server.
        /// </summary>
        /// <param name="token"></param>
        /// <param name="callsign"></param>
        /// <returns></returns>
        public bool Register(string token, string callsign)
        {

            RestRequest registerRequest = new RestRequest(REGISTRATION_ENDPOINT, Method.Get);
            client.AddDefaultHeader("User-Agent", CLIENT_VERSION);
            registerRequest.AddParameter("token", token);
            registerRequest.AddParameter("callsign", callsign);
            registerRequest.OnBeforeDeserialization = resp => { resp.ContentType = "application/json"; };
            var response = client.Execute<ServerRegistrationResponse>(registerRequest);
            if (response.IsSuccessful && response.Data != null)
            {
                this.Token = response.Data.Token;
                this.Callsign = response.Data.Callsign;
                this.DiscordId = response.Data.DiscordId;
                this.DiscordName = response.Data.DiscordName;
                if (this.DiscordId == 0)  // why would it be zero?  Discord user not found?
                {
                    this.IsRegistered = false;
                    return false;  // registration failed
                }
                else
                {
                    this.IsRegistered = true;
                    return true; // registration successful
                }
            }
            else
            {
                return false; // registration failed
            }
        }


        /// <summary>
        /// Deregisters the client from the DWMB server.
        /// </summary>
        public bool Deregister(string token)
        {

            string deregister_uri = String.Format(DEREGISTRATION_ENDPOINT + "/{0}", token);
            RestRequest deregisterRequest = new RestRequest(deregister_uri, Method.Delete);

            RestResponse response = client.Execute(deregisterRequest);
            var content = response.Content;

            if (string.Equals("ok", content, StringComparison.OrdinalIgnoreCase))
            {
                this.IsRegistered = false;
                return true;
            }
            else
            {
                //MessageBox.Show(string.Format("Error un deregistration rest request.\n{0}", response.Content));
                return false;
            }

        }

        /// <summary>
        /// This method tests the connection to the DWMB server by sending a GET request to the test endpoint.
        /// </summary>
        /// <returns>bool indicating success of the connection test.</returns>
        /// /// <exception cref="DWMBApiException"></exception>
        public bool TestConnection()
        {
            RestRequest testRequest = new RestRequest(TEST_ENDPOINT, Method.Get);
            client.AddDefaultHeader("User-Agent", CLIENT_VERSION);
            testRequest.OnBeforeDeserialization = resp => { resp.ContentType = "application/json"; };
            var response = client.Execute(testRequest);
            if (response.IsSuccessful)
            {
                Console.WriteLine(" -- Connection test successful.");
                return true;
            }
            else
            {
                return false;
            }
        }

        /// <summary>
		/// Forwards the given FSD message to the DWMB server.
		/// </summary>
		/// <param name="pm">Message to forward.</param>
        public void ForwardMessage(DWMB_AIO.DWMB.FsdObjects.FsdMessage pm)
        {
            // convert timestamp to Unix time
            DateTimeOffset dateTimeOffset = new DateTimeOffset(pm.Timestamp);
            long unixTimestamp = dateTimeOffset.ToUnixTimeMilliseconds();

            /* Sample JSON (after server rewrite):
			   NOTE: currently, the API does not parse beyond the first message.
			{
				"token": 	"Wjve5p45aTojv6yzRr72FKs9K1py8ze2auFbB8g328o",
				"messages":	[
					{
						"timestamp": "2018-05-01 10:40:00 PM",
						"sender": "XX_SUP",
						"receiver": "DAL1107",
						"message": "IMMA SWING ZE MIGHTY BAN-HAMMER MUAHAHAHAHA"
					}
				]
			}
			 */

            Console.Write(" -- Forwarding to {0}...", SERVER_ADDRESS);

            // Build the API request
            var request = new RestRequest(MESSAGE_FORWARDING_ENDPOINT, Method.Post)
            {
                RequestFormat = RestSharp.DataFormat.Json
            };

            // Build the JSON object
            var messagePayload = new Message
            {
                timestamp = unixTimestamp.ToString(),
                sender = pm.Sender,
                receiver = pm.Recipient,
                message = pm.Message,
            };

            /* The API accepts a list of messages, but currently only processes the first in the list*/
            var messageList = new List<Message>
            {
                messagePayload
            };

            //POST was not being sent as JSON format.  So trying to force Json.
            //request.AddBody(new ForwardedMessage
            request.AddJsonBody(new ForwardedMessage
            {
                token = this.Token,
                messages = messageList
            });

            RestResponse response = client.Execute(request);

            var content = response.Content;

            if (response.IsSuccessful)
            {
            }
            else
            {
                throw new DWMBApiException(String.Format("[Message forwarding error] {0}", response.Content));
            }

        }


    }
}
