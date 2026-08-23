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

`server_location.txt` is bundled into the MSI — it's committed at the repo
root (`../server_location.txt`) and gets published/harvested like any other
output file, so installed clients work out of the box with no manual setup
step.

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
- Committing `server_location.txt` means the repo's copy points at a real
  server. Keep that in mind before making the repo public if it isn't
  already, and update it there (not just locally) if the server address
  ever changes.
