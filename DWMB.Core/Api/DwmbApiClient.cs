using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using DWMB.Core.Api.ApiObjects;
using DWMB.Core.Config;
using RestSharp;

namespace DWMB.Core.Api
{
    /// <summary>
    /// Ports v1's ApiManager (DWMB.Serialization/ApiManager.cs): register, forward,
    /// heartbeat, deregister, plus the self-contained ~55s heartbeat timer (so plugin
    /// shims don't each need to manage one).
    /// </summary>
    public sealed class DwmbApiClient : IDwmbApiClient, IDisposable
    {
        private const string RegisterEndpoint = "/api/v2/register";
        private const string MessagingEndpoint = "/api/v2/messaging";
        private const string HeartbeatEndpoint = "/api/v2/heartbeat";
        private const string DeregisterEndpoint = "/api/v2/deregister";
        private const string StatusEndpoint = "/status";

        private const int HeartbeatIntervalMs = 55_000; // matches server's staleness grace window

        private readonly RestClient _client;
        private readonly string _token;
        private readonly CaptureMethod _captureMethod;
        private readonly string _clientName;
        private readonly string _clientVersion;

        private Timer? _heartbeatTimer;
        private bool _disposed;

        public bool IsRegistered { get; private set; }

        public string? Callsign { get; private set; }

        public DwmbApiClient(DwmbConfig config, CaptureMethod captureMethod, string clientName, string clientVersion)
        {
            _token = config.Token;
            _captureMethod = captureMethod;
            _clientName = clientName;
            _clientVersion = clientVersion;

            var options = new RestClientOptions(config.Server)
            {
                UserAgent = $"{clientName}/{clientVersion}",
            };
            _client = new RestClient(options);
        }

        public async Task<(bool Success, string? Error)> RegisterAsync(string? callsign)
        {
            var request = new RestRequest(RegisterEndpoint, Method.Post);
            request.AddJsonBody(new RegisterRequest
            {
                Token = _token,
                Callsign = callsign ?? string.Empty,
                CaptureMethod = _captureMethod.ToWireValue(),
                ClientName = _clientName,
                ClientVersion = _clientVersion,
            });

            RestResponse<RegisterResponse> response;
            try
            {
                response = await _client.ExecuteAsync<RegisterResponse>(request).ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                IsRegistered = false;
                return (false, $"Registration request failed: {ex.Message}");
            }

            if (!response.IsSuccessful || response.Data == null)
            {
                IsRegistered = false;
                return (false, $"Registration failed: {response.Content}");
            }

            Callsign = response.Data.Callsign;
            IsRegistered = true;
            StartHeartbeatTimer();
            return (true, null);
        }

        public async Task ForwardAsync(RelayMessage message)
        {
            var request = new RestRequest(MessagingEndpoint, Method.Post);
            request.AddJsonBody(new MessagingRequest
            {
                Token = _token,
                Messages = new List<RelayMessageDto>
                {
                    new RelayMessageDto
                    {
                        Timestamp = message.Timestamp.ToUnixTimeMilliseconds(),
                        Sender = message.Sender,
                        Receiver = message.Receiver,
                        Message = message.Message,
                    },
                },
            });

            var response = await _client.ExecuteAsync(request).ConfigureAwait(false);
            if (!response.IsSuccessful)
            {
                throw new DwmbApiException($"Message forwarding failed: {response.Content}");
            }
        }

        public async Task HeartbeatAsync()
        {
            try
            {
                var request = new RestRequest(HeartbeatEndpoint, Method.Get);
                request.AddParameter("token", _token);
                request.AddParameter("capture_method", _captureMethod.ToWireValue());
                await _client.ExecuteAsync(request).ConfigureAwait(false);
            }
            catch
            {
                // best-effort -- a missed heartbeat is handled server-side via missed_heartbeats
            }
        }

        public async Task<bool> DeregisterAsync()
        {
            var request = new RestRequest($"{DeregisterEndpoint}/{_token}", Method.Delete);
            RestResponse response;
            try
            {
                response = await _client.ExecuteAsync(request).ConfigureAwait(false);
            }
            catch
            {
                return false;
            }

            if (!response.IsSuccessful)
            {
                return false;
            }

            IsRegistered = false;
            StopHeartbeatTimer();
            return true;
        }

        public async Task<bool> TestConnectionAsync()
        {
            try
            {
                var request = new RestRequest(StatusEndpoint, Method.Get);
                var response = await _client.ExecuteAsync(request).ConfigureAwait(false);
                return response.IsSuccessful;
            }
            catch
            {
                return false;
            }
        }

        private void StartHeartbeatTimer()
        {
            StopHeartbeatTimer();
            _heartbeatTimer = new Timer(async _ =>
            {
                try
                {
                    await HeartbeatAsync().ConfigureAwait(false);
                }
                catch
                {
                    // HeartbeatAsync already swallows its own errors; this is a last-resort guard
                    // so a Timer callback can never surface an unobserved exception.
                }
            }, null, HeartbeatIntervalMs, HeartbeatIntervalMs);
        }

        private void StopHeartbeatTimer()
        {
            _heartbeatTimer?.Dispose();
            _heartbeatTimer = null;
        }

        public void Dispose()
        {
            if (_disposed)
            {
                return;
            }
            _disposed = true;
            StopHeartbeatTimer();
            _client.Dispose();
        }
    }
}
