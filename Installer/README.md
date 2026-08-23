# DWMB MSI installer

This folder builds a Windows Installer (`.msi`) package for the DWMB AIO Client
using the [WiX Toolset](https://wixtoolset.org/) v7 SDK-style project format.

## What it does

1. `dotnet publish`-es `../DWMB.csproj` as a **self-contained, win-x64** build
   (so end users don't need the .NET 9 Desktop Runtime installed separately).
2. Harvests the published output into `Program Files\DWMB AIO Client\`.
3. Adds Start Menu and Desktop shortcuts to `DWMB.exe`.
4. Produces `DWMB-AIO-Client-Setup.msi`, versioned to match `DWMB.exe`'s file
   version (i.e. whatever `<FileVersion>` is set to in `DWMB.csproj`).

`server_location.txt` is **never** bundled into the MSI — same as the normal
build, it must be created next to the installed `DWMB.exe` after setup (see
the main [README](../README.md#setup)). If the file doesn't already exist
locally, the build drops a harmless placeholder so `dotnet publish` has
something to copy; that placeholder is explicitly excluded from the MSI.

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
  the VATSIM CoC, Npcap, and `server_location.txt` requirements.
