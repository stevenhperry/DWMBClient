# CLAUDE.md

Guidance for working in this repository.

## What this project is

**DWMB ("Don't Wallop Me Bro") AIO Client** is a Windows desktop application for
the [VATSIM](https://vatsim.net) flight-simulation network. It passively captures
FSD-protocol network traffic, detects private messages and on-frequency messages
addressed to the user, and forwards them to the DWMB server, which relays them to
the user on Discord. The goal is to notify a pilot or controller that someone is
trying to reach them, so they don't get "walloped" for going unresponsive.

- **Platform:** Windows only (`net9.0-windows`, WPF, x64).
- **Language/runtime:** C# / .NET 9.
- **UI:** WPF (`MainWindow.xaml`).
- **Packet capture:** Requires [Npcap](https://npcap.com/) (or WinPcap) installed on
  the host for SharpPcap to enumerate devices and capture traffic.

## Build & run

This is a Windows-only WPF project and cannot be built or run on Linux/macOS
(including the environment CLAUDE may be running in). Build on Windows with the
.NET 9 SDK and Visual Studio 2022 (17.14+):

```
dotnet restore DWMB-AIO.sln
dotnet build DWMB-AIO.sln -c Release
dotnet run --project DWMB.csproj
```

There is no test project.

### Runtime requirement: `server_location.txt`

`ApiManager` reads the DWMB server base URL from a `server_location.txt` file at
startup (`File.ReadAllText("server_location.txt")`). The file is copied to the
output directory on build (`CopyToOutputDirectory=Always`) but is **git-ignored and
not committed** — it must exist next to the executable at runtime or the client
throws on construction. It should contain a single line, the server base URL
(e.g. `https://example.com`).

## Architecture

The whole app lives in one project (`DWMB.csproj`, root namespace `DWMB_AIO`),
organized into folders that map to sub-namespaces:

- **Root / UI** — `App.xaml`, `MainWindow.xaml(.cs)`, `AppInfo.cs`.
  `MainWindow.xaml.cs` also contains the `DWMBClient` orchestrator class, which is
  the entry point for client logic (start/stop/deregister, packet handling).
- **`DWMB.FsdDetection`** — network device discovery.
  - `ConnectionManager` enumerates SharpPcap capture devices and keeps only those
    with a local IP address.
  - `HardwareDevice` wraps a capture device and parses its name/description/MAC/IPs.
- **`DWMB.FsdObjects`** — FSD packet/message model.
  - `FsdPacket` is the base packet (timestamp, sender, recipient, raw string).
  - `FsdMessage : FsdPacket` parses `#TM` text-message packets into
    sender/recipient/message.
  - `AbstractFsdPacket` and `PrivateMessage` are unused scaffolding (not wired in).
- **`DWMB.Serialization`** — server API.
  - `ApiManager` is the active REST client (RestSharp): register, deregister,
    forward message, heartbeat, test. Endpoints under `/api/v1/*`.
  - `ApiObjects/` — DTOs (`ForwardedMessage`/`Message`, `ServerRegistrationResponse`,
    `MessageForwardRequest`).
  - `MessageForwarder` is a legacy/superseded forwarder, not used by the app.
  - `DWMBApiException` — API error type.
- **`DWMB.Diagnostics`** — `Logger`, a minimal file logger (`log.txt` by default).

### Runtime flow

1. User enters callsign + registration code and clicks **Start**.
2. `DWMBClient.MainApp` validates the callsign, then `ApiManager.Register` hits
   `/api/v1/register`. A successful registration starts a heartbeat timer
   (`~55s`, endpoint `/api/v1/heartbeat`).
3. `DWMBClient.BeginCapture` picks a capture device (via `ConnectionManager`),
   applies the BPF filter `tcp port 6809`, and starts non-blocking capture.
4. `OnIncomingFsdPacket` extracts the TCP payload, splits it into FSD lines, and
   builds an `FsdMessage` for each `#TM` packet.
5. `IsForwardMessage` decides whether to forward: skip server/FP/DATA traffic and
   self-sent messages; forward direct messages to the user and on-frequency
   messages that start with the user's callsign. Duplicate messages within 2
   seconds are dropped.
6. `ApiManager.ForwardMessage` POSTs the message to `/api/v1/messaging`.
7. **Pause** stops capture; **Deregister** stops capture and calls
   `/api/v1/deregister`, then unlocks the inputs.

## Conventions & gotchas

- **Windows-only APIs.** SharpPcap/PacketDotNet packet capture and WPF are
  Windows-bound; don't assume cross-platform behavior.
- **FSD protocol:** VATSIM FSD runs on TCP port 6809. `#TM<sender>:<recipient>:<message>`
  is a text message; frequency messages address `@xxyyy` (frequency `1xx.yyy`).
- **`DWMBClient` uses static state** (`callsign`, `am`, `device`, `IsCapturing`).
  It is constructed with dummy `ApiManager` values on startup, then replaced with
  real credentials on Start. Be careful editing this shared/static lifecycle.
- **Version numbers** are set in `DWMB.csproj` (`<Version>`, `<AssemblyVersion>`,
  `<FileVersion>`, `<InformationalVersion>`) and surfaced at runtime via
  `AppInfo.DisplayVersion`, which the UI and `ApiManager` user-agent read.
- **Known TODOs in the code:** capture can't be restarted once stopped; device
  selection still uses `Console`-based prompting instead of a WPF dialog; a few
  error paths lack logging. Search for `TODO` before assuming a rough edge is a bug.

## Git

- Do not commit `server_location.txt`, `log.txt`, or build output (all git-ignored).
- Commit or push only when explicitly asked.
