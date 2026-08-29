# CLAUDE.md

Guidance for working in this repository.

## What this project is

**DWMB ("Don't Wallop Me Bro") Client** is a Windows desktop application for
the [VATSIM](https://vatsim.net) flight-simulation network. It passively captures
FSD-protocol network traffic, detects private messages and on-frequency messages
addressed to the user, and forwards them to the DWMB server, which relays them to
the user on Discord. The goal is to notify a pilot or controller that someone is
trying to reach them, so they don't get "walloped" for going unresponsive.

- **Platform:** Windows only (`net9.0-windows`, WPF, x64).
- **Language/runtime:** C# / .NET 9.
- **UI:** WPF (`MainWindow.xaml`).
- **Packet capture:** Requires a libpcap driver installed on the host for SharpPcap
  to enumerate devices and capture traffic. [Npcap](https://npcap.com/) is
  recommended (WinPcap is deprecated). The code uses SharpPcap's driver-agnostic
  capture API (`CaptureDeviceList.Instance`, `ICaptureDevice`, `DeviceModes`), with
  no WinPcap-specific calls, so it works with either driver. Originally developed
  against WinPcap; Npcap compatibility is verified by code review but not
  exhaustively tested on hardware.

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

### MSI installer

`Installer/DWMB.Installer.wixproj` is a separate WiX Toolset v7 SDK-style project
(not part of `DWMB-AIO.sln`) that `dotnet publish`-es `DWMB.csproj` self-contained
(`win-x64`) and packages it as `DWMB-AIO-Client-Setup.msi`. Build it with
`dotnet build Installer\DWMB.Installer.wixproj -c Release`; see
`Installer/README.md` for details. `.github/workflows/installer.yml` builds it in
CI (`windows-latest`) and uploads the `.msi` as a workflow artifact. The install is
per-user (`Package/@Scope="perUser"` in `Package.wxs`, installing under
`%LOCALAPPDATA%\Programs\DontWallopMeBro` via `StandardDirectory
Id="PerUserProgramFilesFolder"`) rather than per-machine, so running the installer
itself needs no UAC elevation — separate from Npcap possibly needing an elevated
*app* process at runtime (see Admin-restricted Npcap below). Per-user scope means
every auto-harvested component from the `<Files>` glob in `Package.wxs` (one per
published file) needs an HKCU-registry KeyPath rather than a file, which bulk
harvesting can't generate — an open WiX gap
([wixtoolset/issues#8633](https://github.com/wixtoolset/issues/issues/8633)), not
an authoring mistake — so the wixproj sets `<SuppressIces>ICE38;ICE64</SuppressIces>`
rather than hand-authoring a registry KeyPath for 100+ files. The server URL
is compiled into `DWMB.exe` (see below), so installs work with no manual setup step.

### Server URL: compiled-in, not a loose file

`ApiManager` reads the DWMB server base URL from a compiled-in constant in
`DWMB.Serialization/ServerConfig.cs` — `ServerConfig.ServerUrl` (production) or
`ServerConfig.ServerUrlDev` (development) — depending on the `ServerEnvironment`
passed to its constructor, when the first real `ApiManager` is constructed (on
Start), via `LoadServerAddress()`. This used to be a `server_location.txt` file
shipped next to the executable; it's now a build-time constant so there's no
plaintext config file sitting in the install folder. `LoadServerAddress()` trims
the value and validates it is a well-formed absolute URL; it must be `https://`
(plain `http://` is rejected unless the host is loopback/localhost). An
empty/invalid value raises a `DWMBApiException` with an actionable message,
surfaced to the user as a dialog on Start rather than crashing at launch — that
would mean the build itself is misconfigured, since there's no runtime file to
go missing anymore.

The main window has a "Use development server" checkbox (`chkUseDevServer`,
default unchecked = production), read in `btnStart_Click` and threaded through
`DWMBClient.MainApp(..., ServerEnvironment environment)` into the `ApiManager`
constructor. `LockInputs()`/`UnlockInputs()` disable/enable it alongside the
callsign and registration-code fields, so it can't be changed while
registered/capturing — the active `ApiManager` is already bound to whichever
server it was constructed against.

Note this only keeps the real URL out of the public git history — it is **not**
a security boundary against someone who has the installed app. A .NET string
constant sits in the compiled assembly in plaintext (`strings DWMB.exe` or any
decompiler recovers it trivially). If a change needs to actually restrict who
can use the API, that has to be enforced server-side (validate the
registration code, rate-limit), not by hiding the endpoint.

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
  - `DWMBApiException` — API error type.
- **`DWMB.Audio`** — local alarm sound (`AlarmPlayer`, `AlarmWaveProvider`), see below.
- **`DWMB.Diagnostics`** — `Logger`, a minimal file logger. Defaults to
  `%LOCALAPPDATA%\DontWallopMeBro\log.txt` — the exe installs to Program Files,
  which a standard user can't write to, so the log can't live next to it or in
  the process's working directory. (The MSI install is now per-user, under
  `%LOCALAPPDATA%\Programs\...` — see MSI installer above — so that folder
  happens to be writable too, but logs stay in their own `%LOCALAPPDATA%`
  location regardless, kept separate from the app's own program files.)

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
6. If not a duplicate, and the alarm-sound checkbox is on, `DWMBClient` triggers
   the local alarm (`AlarmPlayer.Trigger`) — independent of and before the network
   call below, so it fires even if forwarding is slow or fails.
7. `ApiManager.ForwardMessage` POSTs the message to `/api/v1/messaging`.
8. **Pause** stops capture; **Deregister** stops capture and calls
   `/api/v1/deregister`, then unlocks the inputs. The alarm, if sounding, is left
   alone by both — it's a local "you have an unacknowledged message" indicator
   that the user silences on their own via the GUI, not tied to the connection
   lifecycle.

## Conventions & gotchas

- **Windows-only APIs.** SharpPcap/PacketDotNet packet capture and WPF are
  Windows-bound; don't assume cross-platform behavior.
- **FSD protocol:** VATSIM FSD runs on TCP port 6809. `#TM<sender>:<recipient>:<message>`
  is a text message; frequency messages address `@xxyyy` (frequency `1xx.yyy`).
- **`DWMBClient` uses static state** (`callsign`, `am`, `device`, `IsCapturing`).
  `am` is `null` until the user clicks Start (constructing it early validated the
  compiled-in server URL in a field initializer and crashed the app at launch), then
  it is set to a real `ApiManager` on Start. The shared statics that are touched by
  both the capture thread and the UI thread (`am`, `callsign`, `lastMessage`) are
  guarded by `stateLock`; the capture thread snapshots them under the lock and never
  holds it across network I/O. Be careful editing this shared/static lifecycle.
- **Version numbers** are set in `DWMB.csproj` (`<Version>`, `<AssemblyVersion>`,
  `<FileVersion>`, `<InformationalVersion>`) and surfaced at runtime via
  `AppInfo.DisplayVersion`, which the UI and `ApiManager` user-agent read. For a real
  release, though, the committed values in `DWMB.csproj` don't matter: pushing a `v*`
  tag makes `.github/workflows/installer.yml` derive all four from the tag
  (`vMAJOR.MINOR.PATCH`) and patch them in before building, so the tag is the single
  source of truth — hand-bumping `DWMB.csproj` before tagging has no effect on the
  release build, and a mismatched tag/csproj version (e.g. tagging `v1.2.1` while
  `DWMB.csproj` still said `1.2.0.0`) is exactly the bug this exists to prevent. See
  `Installer/README.md`'s Versioning section for details.
- **Capture lifecycle:** Stop/Pause unsubscribes the packet handler, closes the
  device, nulls it, and stops the heartbeat, so a later Start re-initializes cleanly
  (capture is restartable). Device selection uses a WPF dialog
  (`DeviceSelectionWindow`) when more than one adapter is present, and fails with a
  user-facing error when none is found.
- **Npcap check:** `MainWindow`'s constructor calls `CheckNpcapInstalled()`, which
  uses `DWMB.FsdDetection.PcapDriverCheck.IsAvailable()` to probe for a working
  capture driver (via `SharpPcap.CaptureDeviceList.Instance` — SharpPcap has no
  dedicated "is Npcap installed" API, so this is the same call enumeration itself
  needs, and it throws `DllNotFoundException` when the native `wpcap.dll` isn't
  found). If it fails, a dialog explains why and offers to open the Npcap download
  page (`PcapDriverCheck.DownloadUrl`); the window still opens either way. The same
  `PcapDriverCheck.BuildMissingDriverMessage()` text is reused in `MainApp`'s
  `DllNotFoundException` catch block, so hitting the same failure at Start time
  (driver removed mid-session, or the startup dialog was dismissed) gives the same
  actionable message instead of a raw "Unable to load DLL 'wpcap'" error.
- **Admin-restricted Npcap:** Npcap installed with "Restrict driver's access to
  Administrators only" makes the driver load fine but hides every adapter from a
  non-elevated process — `PcapDriverCheck.IsAvailable()` still returns true, and
  `BeginCapture` just sees zero devices, indistinguishable from "no driver" or "no
  network" without extra context. `BeginCapture`'s empty-device-list error checks
  `PcapDriverCheck.IsRunningElevated()` (`WindowsPrincipal.IsInRole(Administrator)`)
  and appends a "try running DWMB as Administrator" hint when the process isn't
  elevated.

- **Alarm sound (`DWMB.Audio`):** off by default, toggled via `chkAlarmSound` on the
  main window (`DWMBClient.AlarmSoundEnabled`). `AlarmWaveProvider` synthesizes a
  siren tone in code (no bundled audio asset) as raw 16-bit PCM (`IWaveProvider`,
  not the float-based `ISampleProvider`, to depend only on NAudio's long-stable core
  API); volume ramps from quiet to full over ~20s and then holds, so it starts
  unobtrusive and gets harder to ignore the longer it's unacknowledged.
  `AlarmPlayer` wraps a NAudio `WaveOutEvent`, which — unlike the older `WaveOut` —
  drives playback from its own background thread rather than needing a Win32
  message pump/STA thread, so `Trigger()` is safe to call directly from the
  SharpPcap capture thread and `Silence()` from the UI thread with no dispatcher
  marshalling (contrast with `ForwardStatusChanged`/`AlarmStateChanged`
  themselves, which *do* need marshalling before touching WPF controls — see
  `OnForwardStatusChanged`/`OnAlarmStateChanged` in `MainWindow`). A second
  `Trigger()` while already sounding is a no-op (doesn't restart the ramp or
  stack players); unchecking the GUI checkbox also silences an alarm already in
  progress, not just future ones. This is in addition to, not a replacement for,
  the Discord notification — Discord can be muted or backgrounded.

Search for `TODO` before assuming a rough edge is a bug.

## Git

- `DWMB.Serialization/ServerConfig.cs` is committed with **placeholder**
  `ServerUrl`/`ServerUrlDev` values (`https://example.com`), not the real
  production/development URLs — this repo is public, and the real (production)
  URL was briefly committed (as `server_location.txt`, since removed) and then
  removed from history (reset + force-push) once that was noticed. Do not
  commit real server URLs here; `.github/workflows/installer.yml` patches them
  in from the `DWMB_SERVER_URL` and `DWMB_SERVER_URL_DEV` repository secrets
  for tagged (`v*`) builds. For a local release build, edit `ServerConfig.cs`
  locally/out of band, uncommitted — a `dotnet build`/`dotnet publish` run
  without that edit compiles in the committed placeholder, since only the
  CI workflow's tag-triggered step ever patches it. A non-tag CI run
  (`workflow_dispatch`, a push to `master`, a PR) uploads the MSI as
  `DWMB-AIO-Client-Setup-TESTBUILD-placeholder-urls` rather than
  `DWMB-AIO-Client-Setup`, with the run summary stating whether real URLs
  were injected, so a placeholder build can't be mistaken for the real
  release artifact. The client also logs the resolved server URL (and, on a
  failed registration, the HTTP status/response) to `log.txt` at Start, so a
  placeholder-URL build is diagnosable after the fact.
- Do not commit `log.txt` or build output (all git-ignored).
- Commit or push only when explicitly asked.
