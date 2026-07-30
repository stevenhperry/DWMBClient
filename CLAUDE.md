# CLAUDE.md

Guidance for working in this repository.

## What this project is

**DWMB ("Don't Wallop Me Bro")** captures VATSIM messages addressed to a pilot or
controller and forwards them to the DWMB server, which relays them on Discord (or
Pushover). **This is v2.0, a greenfield rewrite** (see the sibling `DWMBServer-Private`
repo's `docs/DWMB-v2-plan.md` for the full plan). v1 was a single Windows WPF app that
sniffed FSD network traffic via Npcap; v2 adds two native plugin adapters that need no
packet-capture driver at all, keeping the npcap app as a fallback for clients without a
plugin SDK.

- **Three adapters, one shared core.** `DWMB.Plugin.VPilot` and `DWMB.Plugin.XPilot`
  subscribe to already-parsed message events from their host pilot client (no raw
  packet capture); `DWMB.Client.Npcap` is the original SharpPcap-based sniffer, kept as
  a fallback. All three are thin shims over `DWMB.Core`, which owns every piece of logic
  that doesn't depend on how messages were captured: the server API client, the
  forward/dedupe filter, and config loading.
- **Registration is now web-based.** The DWMB server's Discord OAuth login page issues a
  token; plugins store it in `dwmb.config.json` next to their DLL (auto-detecting
  callsign from their host's connect event), while the npcap fallback keeps v1's UI
  callsign+token entry.

## Solution layout

```
DWMB.Core/              netstandard2.0 -- shared by all three adapters below
DWMB.Core.Tests/         xUnit tests for DWMB.Core -- the only project testable off Windows
DWMB.Plugin.VPilot/       net48 (matches vPilot's own .NET Framework-era host)
DWMB.Plugin.XPilot/        net10.0 (matches xPilot's plugin SDK's target)
DWMB.Client.Npcap/          net9.0-windows, WPF -- the npcap fallback app
```

## Build & run

Windows-only for anything except `DWMB.Core`/`DWMB.Core.Tests` (SharpPcap/PacketDotNet
and WPF are Windows-bound; the plugin projects need their host's SDK DLL, which isn't
distributed via NuGet -- see each plugin's `lib/README.md`). Build on Windows with the
.NET 9 SDK and Visual Studio 2022 (17.14+):

```
dotnet restore DWMB-AIO.sln
dotnet build DWMB-AIO.sln -c Release
dotnet run --project DWMB.Client.Npcap/DWMB.Client.Npcap.csproj
```

`DWMB.Core.Tests` (xUnit) is the one project runnable anywhere:

```
dotnet test DWMB.Core.Tests/DWMB.Core.Tests.csproj
```

### Runtime requirement: `dwmb.config.json`

Every adapter reads server URL (and, for plugins, the token) from a `dwmb.config.json`
file next to its own executable/DLL, via `DWMB.Core.Config.DwmbConfig.Load()`. This file
is git-ignored and not committed — it must exist at runtime or the adapter throws (or,
for plugins, quietly refuses to activate; see below) a `DwmbApiException`.

- **Plugins** (`DWMB.Plugin.VPilot`, `DWMB.Plugin.XPilot`): `{ "server": "...", "token": "..." }`.
  `DwmbConfig.Load()` only requires `server` to be present — plugins additionally check
  `config.Token` themselves in `Initialize()` and no-op if it's blank, since a token is
  the only thing they have no other source for.
- **`DWMB.Client.Npcap`**: `{ "server": "..." }` (no `token` — the npcap client
  deliberately keeps v1's UI callsign+token text entry rather than persisting a token to
  disk; `ClientOrchestrator.StartAsync` overwrites the loaded config's `Token` with the
  UI value before constructing `DwmbApiClient`).

`DwmbConfig.Load()` resolves the file relative to the *host's* executing assembly
location (`Assembly.GetExecutingAssembly().Location`, resolved from within `DWMB.Core`
itself — which works because `DWMB.Core.dll` is always copied into the same output
directory as whichever project references it, via normal MSBuild `ProjectReference`
copying), not the current working directory. v1's `ApiManager` read
`server_location.txt` via `File.ReadAllText("server_location.txt")` as a field
initializer — a CWD-relative path re-read on every construction — which would have been
dead on arrival inside a plugin DLL whose working directory is its host application's,
not the DLL's own directory. That bug is what this fixes.

## Architecture

### `DWMB.Core` (shared)

- **`CaptureMethod`** — `VPilotPlugin` | `XPilotPlugin` | `Npcap` enum, reported to the
  server as pure provenance (never branched on server-side) for evidence-based
  deprecation of capture methods.
- **`RelayMessage`** — the one message shape every adapter converts into before calling
  `DwmbApiClient.ForwardAsync`.
- **`MessageFilter`** — `IsForwardable` (ported from v1's `IsForwardMessage`,
  `MainWindow.xaml.cs:320-342`; callsign is now an explicit parameter instead of a
  static field read, since three adapter instances can run concurrently instead of one
  static orchestrator) and `IsDuplicate` (v1's 2-second dedupe check,
  `MainWindow.xaml.cs:282-291`). `FreqTag` maps a plugin-reported frequency to the wire's
  `@xxyyy` convention — Hz is confirmed by xPilot's own SDK docs but not independently
  verified against vPilot, nor against a real running session for either host; its xUnit
  test stays skip-marked until that live verification happens (see `MessageFilter.cs`'s
  doc comment for detail).
- **`Config.DwmbConfig`** — see "Runtime requirement" above.
- **`Api.DwmbApiClient`** (`IDwmbApiClient`) — ports v1's `ApiManager`: register,
  forward, heartbeat (self-contained ~55s timer, matching v1's cadence), deregister,
  and `TestConnectionAsync()` (hits the server's public `GET /status` — v2 has no
  dedicated `/test` endpoint). Fixes two v1 issues while porting: `TestConnection()` no
  longer re-adds a duplicate `User-Agent` header (v1's own `Register()` comment already
  flagged this), and success is determined by HTTP status rather than parsing response
  body text, matching the v2 server's JSON responses instead of v1's bare `"ok"` string.
- **`Fsd.FsdPacket`/`FsdMessage`** — v1's FSD wire-text parser, used only by
  `DWMB.Client.Npcap` (plugins receive already-parsed events from their host and never
  see raw `"#TM..."` text). Made public; dropped the unused `(DateTime, byte[])`
  constructor, `IsPrivateMessage()`, and `OnMessageArrival` event that v1 never used.

### Plugin adapters (`DWMB.Plugin.VPilot`, `DWMB.Plugin.XPilot`)

Both are a single class implementing their host's `IPlugin` interface
(`RossCarlson.Vatsim.Vpilot.Plugins.IPlugin` / `Vatsim.Xpilot.PluginSdk.IPlugin` — nearly
identical shape: `Name` + `Initialize(IBroker broker)`). `Initialize` loads config,
constructs a `DwmbApiClient`, and wires four broker events: `NetworkConnected` (register,
auto-detecting callsign), `NetworkDisconnected` (deregister), `PrivateMessageReceived`
and `RadioMessageReceived` (filter via `MessageFilter.IsForwardable` and forward). All
API calls are fire-and-forget (`_ = api.SomeAsync(...)`) since plugin event handlers are
synchronous `void` methods.

Neither SDK is distributed via NuGet — each plugin project has a `lib/` folder
(gitignored except for a README) where you drop the third-party SDK DLL before building;
see `DWMB.Plugin.VPilot/lib/README.md` / `DWMB.Plugin.XPilot/lib/README.md`.

### `DWMB.Client.Npcap` (fallback)

Decomposed from v1's static `DWMBClient` god-class (`MainWindow.xaml.cs:170-464`, which
mixed static session state, WPF `MessageBox.Show` calls, and packet-capture logic all in
one class) into three pieces:

- **`FsdCaptureSource`** — owns the SharpPcap device: open/BPF-filter (`tcp port 6809`)/
  `OnPacketArrival`, TCP-payload extraction, FSD-line cleanup regexes, and constructing
  an `FsdMessage` per `#TM` line. Raises `MessageCaptured`. This is the capture-only half
  of v1's `OnIncomingFsdPacket` (`MainWindow.xaml.cs:245-312`).
- **`ClientOrchestrator`** — instance class (not static): owns callsign/API-client/dedupe
  state, `StartAsync`/`Stop`/`DeregisterAsync` return `(bool Success, string? Error)`
  tuples with no UI calls inside. Subscribes to `FsdCaptureSource.MessageCaptured`,
  applies `MessageFilter.IsForwardable`/`IsDuplicate`, then calls `ForwardAsync`. This is
  the portable half of `OnIncomingFsdPacket` plus everything else v1's `DWMBClient` did.
- **`MainWindow.xaml.cs`** — button click handlers only, translating `ClientOrchestrator`
  results into `MessageBox.Show(...)` calls. v1's `Deregister()` called `MessageBox.Show`
  directly from inside the orchestrator; that coupling is what this split fixes.

Two v1 TODOs fixed in the process: **capture restart** (`FsdCaptureSource.Start` is now
safe to call more than once — `-=` before `+=` on `OnPacketArrival` is a no-op if never
subscribed — and `ClientOrchestrator.StartAsync` re-enumerates devices fresh on every
Start click rather than reusing a possibly-stale device handle) and **device selection**
(`DeviceSelectionWindow`, a proper WPF dialog, replaces v1's `Console.ReadLine()` prompt,
which could never have worked in a windowed app with no attached console).

## Conventions & gotchas

- **`DWMB.Core` targets `netstandard2.0`** so vPilot's .NET-Framework-era host,
  xPilot's `net10.0` plugin SDK, and the npcap fallback's `net9.0-windows` can all
  reference the same assembly.
- **FSD protocol:** VATSIM FSD runs on TCP port 6809 (npcap fallback only — plugins
  never see the wire protocol). `#TM<sender>:<recipient>:<message>` is a text message;
  frequency messages address `@xxyyy` (frequency `1xx.yyy` MHz).
- **No more static state.** `ClientOrchestrator` is instantiated once per `MainWindow`
  and owns its session state as instance fields — don't reintroduce v1's static
  `DWMBClient` pattern when touching this code.
- **Version numbers** are set per-project now (each of the five `.csproj` files has its
  own `<Version>` etc.), not just the one root `DWMB.csproj` v1 had. `AppInfo.DisplayVersion`
  (npcap-only) still reads assembly attributes the same way.
- **Dead code already removed, don't reintroduce:** `MessageForwarder.cs`,
  `AbstractFsdPacket.cs`, `PrivateMessage.cs`, `ApiObjects/MessageForwardRequest.cs`
  (superseded by `DWMB.Core.Api.ApiObjects`), and the unused `FsdPacket` members listed
  above.

## Git

- Do not commit `dwmb.config.json`, `log.txt`, third-party plugin SDK DLLs under either
  `lib/` folder, or build output (all git-ignored).
- Commit or push only when explicitly asked.
