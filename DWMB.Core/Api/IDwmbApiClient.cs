using System.Threading.Tasks;

namespace DWMB.Core.Api
{
    /// <summary>Ports v1's ApiManager (DWMB.Serialization/ApiManager.cs). Token, capture
    /// method, and client identity are bound at construction (from DwmbConfig + the
    /// host adapter), so call sites only ever pass what varies per call.</summary>
    public interface IDwmbApiClient
    {
        bool IsRegistered { get; }

        string? Callsign { get; }

        /// <summary>Registers with the server. `callsign` may be null for plugin
        /// adapters that haven't yet received NetworkConnected.</summary>
        Task<(bool Success, string? Error)> RegisterAsync(string? callsign);

        Task ForwardAsync(RelayMessage message);

        /// <summary>Fire-and-forget; failures are logged server-side via missed_heartbeats, not thrown here.</summary>
        Task HeartbeatAsync();

        Task<bool> DeregisterAsync();

        /// <summary>Hits the server's public GET /status (no dedicated /test endpoint in v2).</summary>
        Task<bool> TestConnectionAsync();
    }
}
