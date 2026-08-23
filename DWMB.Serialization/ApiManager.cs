using DWMB_AIO.DWMB.Serialization.ApiObjects;
using RestSharp;
using System.Diagnostics.CodeAnalysis;
using System.Threading;

namespace DWMB_AIO.DWMB.Serialization
{
    class ApiManager
    {
        //private readonly string CLIENT_VERSION = "DWMBClient/0.11.01";

        // Using AppInfo to get version info
        private readonly string CLIENT_VERSION = $"DWMBClient/{DWMB_AIO.AppInfo.DisplayVersion}";

        private readonly string SERVER_ADDRESS;
        private readonly string MESSAGE_FORWARDING_ENDPOINT = "/api/v1/messaging";
        private readonly string REGISTRATION_ENDPOINT = "/api/v1/register";
        private readonly string DEREGISTRATION_ENDPOINT = "/api/v1/deregister";
        private readonly string TEST_ENDPOINT = "/api/v1/test";
        private readonly string HEARTBEAT_ENDPOINT = "/api/v1/heartbeat";
        public required string Token { get; set; }
        public required string Callsign { get; set; }

        public long DiscordId { get; set; }

        public string? DiscordName { get; set; }
        private RestSharp.RestClient client;
        public bool IsRegistered { get; set; } = false;
        public bool IsCapturing { get; set; } = false;


        [SetsRequiredMembers]
        public ApiManager(string token, string callsign, ServerEnvironment environment = ServerEnvironment.Production)
        {
            this.Token = token;
            this.Callsign = callsign;
            this.IsRegistered = false;
            this.SERVER_ADDRESS = LoadServerAddress(environment);

            // Set up RestSharp client for use by other functions later
            var options = new RestClientOptions(SERVER_ADDRESS)
            {
                UserAgent = CLIENT_VERSION
            };

            client = new RestClient(options);

        }

        /// <summary>
        /// Validates the compiled-in DWMB server base URL for the requested
        /// <paramref name="environment"/> (<see cref="ServerConfig.ServerUrl"/> for
        /// Production, <see cref="ServerConfig.ServerUrlDev"/> for Development). Throws
        /// <see cref="DWMBApiException"/> with an actionable message when it is empty or
        /// not a well-formed absolute http(s) URL, so the caller can surface a friendly
        /// dialog instead of a cryptic crash (issue #5). A bad value here means the build
        /// itself is misconfigured, since the URL is compiled in rather than read from a
        /// file at runtime.
        /// </summary>
        private static string LoadServerAddress(ServerEnvironment environment)
        {
            string constantName = environment == ServerEnvironment.Development
                ? nameof(ServerConfig.ServerUrlDev)
                : nameof(ServerConfig.ServerUrl);
            string raw = (environment == ServerEnvironment.Development
                ? ServerConfig.ServerUrlDev
                : ServerConfig.ServerUrl).Trim();

            if (string.IsNullOrWhiteSpace(raw))
            {
                throw new DWMBApiException(
                    $"The DWMB server URL is not configured (ServerConfig.{constantName} is empty). " +
                    "This build was not compiled correctly.");
            }

            if (!Uri.TryCreate(raw, UriKind.Absolute, out Uri? uri) ||
                (uri.Scheme != Uri.UriSchemeHttp && uri.Scheme != Uri.UriSchemeHttps))
            {
                throw new DWMBApiException(
                    $"The configured DWMB server URL (ServerConfig.{constantName}) is not a valid " +
                    $"absolute http(s) URL (found: '{raw}'). This build was not compiled correctly.");
            }

            // Require HTTPS for real servers so forwarded private/on-frequency message
            // content is not sent in cleartext and cannot be tampered with on-path
            // (issue #7). Plain http is tolerated only for loopback/dev use.
            if (uri.Scheme == Uri.UriSchemeHttp && !uri.IsLoopback)
            {
                throw new DWMBApiException(
                    $"The configured DWMB server URL (ServerConfig.{constantName}) uses an insecure " +
                    $"'http://' URL ('{raw}'). Forwarded messages would travel in cleartext. Use " +
                    "'https://' (plain http is only permitted for localhost).");
            }

            return raw;
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
            //client.AddDefaultHeader("User-Agent", CLIENT_VERSION);  //getting duplicates in header.  Removing this one, relying on the one set in the RestClientOptions in the constructor.
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
                    // Start sending periodic heartbeats
                    StartHeartbeatTimer();
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
                // Stop sending periodic heartbeats when deregistered
                StopHeartbeatTimer();
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
            // User-Agent is already set once via RestClientOptions in the constructor.
            // Calling AddDefaultHeader here accumulated a duplicate header on every call.
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

        public void SendHeartbeat()
        {
            RestRequest heartbeatRequest = new RestRequest(HEARTBEAT_ENDPOINT, Method.Get);
            heartbeatRequest.AddParameter("token", this.Token);
            heartbeatRequest.OnBeforeDeserialization = resp => { resp.ContentType = "application/json"; };
            var response = client.Execute(heartbeatRequest);
            if (response.IsSuccessful)
            {
                Console.WriteLine(" -- Heartbeat successful.");
            }
            else
            {
                Console.WriteLine(" -- Heartbeat failed.");
            }
        }
        // Timer used to periodically send heartbeats while registered
        private Timer? heartbeatTimer;
        private readonly int HEARTBEAT_INTERVAL_MS = 55_000; //55 seconds, slightly less than 1 minute to ensure it is received.  Server is configured to allow up to 3 minutes between heartbeats before considering client disconnected.

        private void StartHeartbeatTimer()
        {
            // Ensure any existing timer is stopped first
            StopHeartbeatTimer();

            // Schedule periodic heartbeats. First heartbeat will occur after HEARTBEAT_INTERVAL_MS.
            heartbeatTimer = new Timer(_ =>
            {
                try
                {
                    SendHeartbeat();
                }
                catch (Exception ex)
                {
                    Console.WriteLine($" -- Heartbeat exception: {ex.Message}");  // except we do not use console logging in the final product, so this is just for debugging purposes.  In production, we might want to log this to a file or other logging system.
                    //TODO: add logging of heartbeat exceptions to a file or other logging system, since console logging is not used in the final product.
                }
            }, null, HEARTBEAT_INTERVAL_MS, HEARTBEAT_INTERVAL_MS);
        }

        /// <summary>
        /// Stops periodic heartbeats without deregistering. Used when capture is paused
        /// so the server stops treating this client as online (issue #9). Heartbeats
        /// resume automatically the next time the client registers.
        /// </summary>
        public void StopHeartbeat() => StopHeartbeatTimer();

        private void StopHeartbeatTimer()
        {
            if (heartbeatTimer != null)
            {
                try
                {
                    heartbeatTimer.Dispose();
                }
                catch { }
                heartbeatTimer = null;
            }
        }
    }
}
