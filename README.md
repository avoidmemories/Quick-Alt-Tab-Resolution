# Quick Alt Tab

> ## ⚠️ IMPORTANT: NVIDIA GPUs and DisplayPort monitors only
>
> **This program works only with NVIDIA graphics cards and monitors connected via DisplayPort.**
> **It will NOT help with monitors connected via HDMI.**

---

## The Problem

When you Alt+Tab out of a game running at a custom resolution, many monitors go black for a few seconds. This happens because the game and the desktop use different refresh rates, and the monitor needs time to switch between them.

Windows may show the same refresh rate everywhere, for example **165 Hz**, but the real values can differ. Windows rounds the number shown in its settings. The actual refresh rate of the desktop might be **164.843 Hz**, while the game runs at **165 Hz**. The exact rounding depends on the monitor and the display settings.

## The Solution

Quick Alt Tab makes the desktop and the custom resolution use the same refresh rate, so the monitor no longer has to switch modes when you Alt+Tab.

1. Enter a custom resolution in the **Custom** fields and press **Apply**.
2. Two confirmation prompts will appear, similar to the NVIDIA Control Panel when you add a new resolution. Press **Y** within **15 seconds** if everything looks fine and the resolution works correctly. Press **N** if there are any problems with the resolution.
3. The program creates two presets for your monitor:
   - **Native preset:** your monitor's native resolution and refresh rate (for example, 2560 × 1440 @ 165 Hz).
   - **Custom preset:** your custom resolution with a refresh rate that is **0.001 Hz higher** (for example, 1920 × 1080 @ 165.001 Hz).
4. Because the two refresh rates are synchronized, the monitor does not need to change modes when you switch between the game and the desktop.

## Requirements

- Windows 10 or Windows 11
- NVIDIA graphics card with the NVIDIA driver installed
- Monitor connected via **DisplayPort**
- .NET 8 Desktop Runtime, only if you use the framework-dependent build (see below)

## Installation

Download `Quick Alt Tab.exe` from the [Releases](../../releases) page and run it. No installation is required.

## Building from Source

Requires the [.NET 8 SDK](https://dotnet.microsoft.com/download) and Visual Studio 2022 (or the `dotnet` CLI).

Open a PowerShell window in the folder that contains `Quick Alt Tab.csproj` and run:

```powershell
dotnet publish "Quick Alt Tab.csproj" -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true -p:EnableCompressionInSingleFile=true -p:DebugType=none -o ".\publish"
```

For a smaller file (a few MB instead of about 70 MB), build without `--self-contained true`. The target PC will then need the .NET 8 Desktop Runtime installed.

## Limitations

- NVIDIA only. The program uses NVIDIA APIs and will not work with AMD or Intel graphics.
- DisplayPort only. HDMI connections are not supported and will not benefit from this tool.
- The result depends on your monitor and driver. Some monitors may still show a short delay.