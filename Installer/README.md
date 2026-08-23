# DWMB MSI installer

This folder builds a Windows Installer (`.msi`) package for the DWMB AIO Client
using the [WiX Toolset](https://wixtoolset.org/) v7 SDK-style project format.

## What it does

1. `dotnet publish`-es `../DWMB.csproj` as a **self-contained, win-x64** build
   (so end users don't need the .NET 9 Desktop Runtime installed separately).
2. Harvests the published output into `Program Files\DontWallopMeBro\`.
3. Adds Start Menu and Desktop shortcuts to `DWMB.exe`.
4. Produces `DWMB-AIO-Client-Setup.msi`, versioned to match `DWMB.exe`'s file
   version (i.e. whatever `<FileVersion>` is set to in `DWMB.csproj`).

The DWMB production and development server URLs are compiled into `DWMB.exe`
(see `../DWMB.Serialization/ServerConfig.cs`) rather than shipped as a loose
file next to it, so installed clients work out of the box with no manual
setup step and there's no plaintext config file sitting in the install
folder. The app's "Use development server" checkbox (default off) picks
which of the two compiled-in URLs a session connects to. The committed
constants hold **placeholders** (`https://example.com`), not real server
addresses — see Notes below.

## Building

Requires Windows, the .NET 9 SDK, and internet access on first build (to
restore the `WixToolset.Sdk` / `WixToolset.UI.wixext` NuGet packages — no
separate WiX install needed).

```
dotnet build Installer\DWMB.Installer.wixproj -c Release
```

The `.msi` is written under `Installer\bin\...\`. A GitHub Actions workflow
(`.github/workflows/installer.yml`) builds it on `windows-latest` and uploads
it as a workflow artifact — trigger it manually from the Actions tab if you
don't have a Windows machine handy.

## Notes

- `UpgradeCode` in `Package.wxs` is fixed — do not change it, or upgrades will
  stop detecting/replacing prior installs.
- The installer targets `win-x64` only, matching `DWMB.csproj`'s
  `<PlatformTarget>x64</PlatformTarget>`.
- The license/notice page shown during setup (`License.rtf`) isn't a legal
  license — there isn't one for this project — it's a short heads-up about
  the VATSIM CoC and the Npcap requirement.
- The committed `ServerConfig.ServerUrl`/`ServerUrlDev` are placeholders
  (`https://example.com`) on purpose — this repo is public, so don't commit
  the real URLs here. `.github/workflows/installer.yml` patches them in
  automatically for tagged builds (`v*`), reading the real URLs from the
  `DWMB_SERVER_URL` (production) and `DWMB_SERVER_URL_DEV` (development)
  repository secrets, so neither ever touches git history. A tagged build
  fails loudly if either secret isn't set, rather than silently shipping the
  placeholder. For a manual/local release build, edit
  `DWMB.Serialization/ServerConfig.cs` locally (uncommitted) before building.
- Note this only keeps the real URL out of the public repo — it does not
  hide it from anyone who has the installed app. A .NET string constant
  sits in the compiled assembly in plaintext and is trivial to recover with
  `strings` or a decompiler (ILSpy/dnSpy). Don't treat this as a security
  boundary; if the goal is preventing unauthorized use of the API, enforce
  that server-side (validate the registration code, rate-limit), not by
  hiding the endpoint.
