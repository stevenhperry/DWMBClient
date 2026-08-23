# DWMB — Don't Wallop Me Bro
[DontWallopMeBro.com](https://www.dontwallopmebro.com)

A Windows desktop client for the [VATSIM](https://vatsim.net) flight-simulation
network that lets you know when someone is trying to reach you — so you don't get
"walloped" for going unresponsive.

DWMB passively watches your network traffic for FSD private messages and
on-frequency messages addressed to your callsign, and forwards them to the DWMB
service, which pings you on Discord. It never sends anything on the network and
never transmits on your behalf; it only reads and forwards messages meant for you.

## IMPORTANT - DWMB helps you to comply with the [Vatsim CoC](https://vatsim.net/docs/policy/code-of-conduct) ##
You **_must_** remain within easy reach of your PC to respond to any ATC or SUP.  Use
DWMB while doing honey-do items around your home, not while driving to a store.

## How it works

1. You register your callsign with the DWMB Discord bot and receive a
   registration code.
2. You enter your callsign and registration code in the client and click
   **Start**.
3. The client captures FSD traffic (TCP port 6809), picks out messages addressed
   to you, and forwards them to the DWMB server.
4. The server delivers them to you on Discord, then deletes them from the server.
5. While registered, the client sends a heartbeat about once a minute so the
   server knows you're still connected.

## Data Handling & Privacy

1. The source code here is published openly so that interested individuals can
   verify that no additional information is captured, stored, or transmitted.
2. Other than debugging log files which automatically delete on a rolling basis,
   DWMB does not retain personally identifiable information about who uses this
   service or the messages they receive.
3. Basic & anonymized statistics are recorded.  DWMB records the start & stop
   dates & times of each session, the number of messages forwarded, and the
   origin of the message forwarded (e.g. ATC, AutoATC, or SUP).
4. By being a member of our discord server, your discord ID is visible and you
   could be tracked via any VATSIM-related discords you are also a member of.
5. That said, the author has no interest in who uses the service to help them
   comply with the VATSIM CoC.  The author, who is also a VATSIM Supervisor,
   is not aware of any actions taken against any VATSIM members for their use
   of DWMB or its predecessor FCOM.


## Requirements

- **Windows** (the client is built on WPF / `.NET 9` for Windows).
- **[.NET 9 Desktop Runtime](https://dotnet.microsoft.com/download/dotnet/9.0)**
  (unless you use a self-contained build).
- **[Npcap](https://npcap.com/)** — the packet-capture driver (see below).
- A **DWMB registration code**, obtained from the DWMB Discord bot.

### Npcap / WinPcap

DWMB uses [SharpPcap](https://github.com/dotpcap/sharppcap) for packet capture,
which needs a libpcap-compatible driver installed on Windows.

- **Npcap is recommended.** The client uses SharpPcap's standard capture API, which
  works with Npcap. WinPcap is deprecated and unmaintained, and SharpPcap itself
  recommends Npcap over it.
- When installing Npcap, enabling **"Install Npcap in WinPcap API-compatible
  Mode"** is the safest option.
- If you install Npcap with **"Restrict Npcap driver's access to Administrators
  only,"** you must run DWMB **as Administrator** for it to see your network
  adapters.

> **Note:** As of version 1.1, Npcap is the officially supported solution.  No
> further support of WinPCap will be offered.

DWMB checks for a working capture driver at startup; if none is found, it shows
a dialog explaining that Npcap is missing, with a link to the download page and
the steps above. The app still opens either way — you just won't be able to
**Start** until a driver is installed and DWMB is restarted.

If Npcap is installed but restricted to Administrators (see above) and DWMB
isn't running elevated, **Start** will find zero network adapters — the
resulting error mentions trying "Run as Administrator" as the likely fix.


## Setup

1. Install the .NET 9 Desktop Runtime and Npcap (see Requirements).
2. Register with the DWMB Discord bot to get your registration code.
3. The DWMB server's base URL is compiled into `DWMB.exe` — there's nothing
   to configure. If it's ever missing or malformed (e.g. a bad local build),
   the app fails to start with an actionable error instead of a crash.

## Usage

1. Launch DWMB.
2. Enter your **Callsign** (letters, numbers, underscores, and hyphens only) and
   your **Registration Code**. Leave **Use development server** unchecked
   unless you were specifically told to test against the dev server — it can't
   be changed once you're registered/capturing.
3. Click **Start** to register and begin forwarding. If prompted, select your
   network adapter.
4. The status fields show whether you are **Registered** and **Capturing**.
5. Click **Pause** to stop forwarding.
6. Click **Deregister** to stop and disconnect from the bot. If deregistration
   fails, DM the bot with `remove` to be removed manually.

Your callsign here should match the callsign you connect to the network with.

## Building from source

Requires Windows, the .NET 9 SDK, and Visual Studio 2022 (17.14+) or the
`dotnet` CLI.

```
dotnet restore DWMB-AIO.sln
dotnet build DWMB-AIO.sln -c Release
```

The production/development server URLs used by a build from source are the
placeholders committed in `DWMB.Serialization/ServerConfig.cs`; edit that file
locally (uncommitted) to point at real servers for testing.

### Building the MSI installer

A self-contained Windows Installer (`.msi`) can be built from the
[`Installer/`](Installer/) folder using the WiX Toolset:

```
dotnet build Installer\DWMB.Installer.wixproj -c Release
```

See [`Installer/README.md`](Installer/README.md) for details, or trigger the
`Build MSI installer` GitHub Actions workflow to get a built `.msi` without a
local Windows machine.

## Support

If DWMB is useful to you, you can support development on
[Ko‑fi](https://ko-fi.com/dontwallopmebro). There's also a QR code and button in
the app itself.

## Disclaimer

DWMB is a third-party tool and is not affiliated with or endorsed by VATSIM. It
reads network traffic on your own machine to forward messages addressed to you;
use it in accordance with VATSIM's code of conduct.
