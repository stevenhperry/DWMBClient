# DWMB — Don't Wallop Me Bro (v2.0)

DWMB lets you know when someone is trying to reach you on VATSIM — so you don't
get "walloped" for going unresponsive. It watches for FSD private messages and
on-frequency messages addressed to your callsign, and forwards them to the DWMB
service, which pings you on Discord (or Pushover). It never sends anything on the
network and never transmits on your behalf; it only reads and forwards messages
meant for you.

**v2.0 is a greenfield rewrite** (see `CLAUDE.md` for the full architecture).
There are now three ways to capture messages, all sharing the same core logic:

* **`DWMB.Plugin.VPilot`** — a vPilot plugin. Recommended if you fly with vPilot:
  no packet-capture driver needed, and your callsign is auto-detected.
* **`DWMB.Plugin.XPilot`** — an xPilot plugin. Same benefits, for xPilot users.
* **`DWMB.Client.Npcap`** — the original standalone WPF app, kept as a fallback
  for clients without a plugin SDK (e.g. swift). Requires Npcap.

## Registration

All three adapters need a **token**, obtained by logging in with Discord at the
DWMB server's onboarding page (`https://<your-server>/oauth/discord/login`). That
page also lets you choose how you're notified (Discord DM or Pushover).

## Setup: vPilot / xPilot plugins

1. Build the plugin project (see "Building from source" — you'll need to supply
   the third-party plugin SDK DLL yourself; see `DWMB.Plugin.VPilot/lib/README.md`
   or `DWMB.Plugin.XPilot/lib/README.md`).
2. Copy the built plugin DLL into your pilot client's Plugins folder (vPilot's
   `Plugins` folder next to `vPilot.exe`; xPilot's appdata `Plugins` folder).
3. Next to that DLL, create `dwmb.config.json`:
   ```json
   { "server": "https://your-dwmb-server.example.com", "token": "PASTE_FROM_ONBOARDING" }
   ```
4. Launch vPilot/xPilot and connect. Your callsign is detected automatically —
   there's nothing else to enter.

## Setup: npcap fallback

1. Install the [.NET 9 Desktop Runtime](https://dotnet.microsoft.com/download/dotnet/9.0)
   and [Npcap](https://npcap.com/) (see Npcap notes below).
2. Next to `DWMB.Client.Npcap.exe`, create `dwmb.config.json`:
   ```json
   { "server": "https://your-dwmb-server.example.com" }
   ```
   (No `token` here — the npcap client, unlike the plugins, still asks for your
   token in the app itself each session.)
3. Launch the app, enter your **Callsign** and the **token** from the onboarding
   page, and click **Start**. If prompted, select your network adapter.
4. The status fields show whether you are **Registered** and **Capturing**.
   **Pause** stops forwarding; **Deregister** stops and removes your registration.

### Npcap / WinPcap

DWMB.Client.Npcap uses [SharpPcap](https://github.com/dotpcap/sharppcap) for
packet capture, which needs a libpcap-compatible driver.

- **Npcap is recommended** (WinPcap is deprecated/unmaintained).
- Enabling **"Install Npcap in WinPcap API-compatible Mode"** is the safest option.
- If you install Npcap with **"Restrict Npcap driver's access to Administrators
  only,"** you must run the app **as Administrator**.

## Building from source

Requires Windows, the .NET 9 SDK, and Visual Studio 2022 (17.14+) or the `dotnet`
CLI. The solution has five projects:

| Project | Target | Notes |
|---|---|---|
| `DWMB.Core` | `netstandard2.0` | Shared logic: API client, message filtering, config loading. Referenced by all four projects below. |
| `DWMB.Core.Tests` | `net9.0` | xUnit tests for `DWMB.Core` — the only project runnable/testable outside Windows. |
| `DWMB.Plugin.VPilot` | `net48` | vPilot plugin. Needs `RossCarlson.Vatsim.Vpilot.Plugins.dll` in `lib/` — see its README there. |
| `DWMB.Plugin.XPilot` | `net10.0` | xPilot plugin. Needs `xPilot.PluginSdk.dll` in `lib/` — see its README there. |
| `DWMB.Client.Npcap` | `net9.0-windows` | The WPF fallback app. |

```
dotnet restore DWMB-AIO.sln
dotnet build DWMB-AIO.sln -c Release
```

`DWMB.Core.Tests` can be run on any platform:

```
dotnet test DWMB.Core.Tests/DWMB.Core.Tests.csproj
```

Remember to place a `dwmb.config.json` next to whichever built output you're
running (it is copied to the output directory on build if present, but is not
committed to the repository — see Setup above for its shape per adapter).

## Support

If DWMB is useful to you, you can support development on
[Ko‑fi](https://ko-fi.com/dontwallopmebro). There's also a QR code and button in
the npcap app itself.

## Disclaimer

DWMB is a third-party tool and is not affiliated with or endorsed by VATSIM. It
reads network traffic or plugin events on your own machine to forward messages
addressed to you; use it in accordance with VATSIM's rules and your local
regulations.
