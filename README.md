# Mint Launcher
A lightweight, modern Windows app launcher living in your system tray. Reimagined and vibe-coded with Google Gemini, based on the original project by [hellzerg/mint](https://github.com/hellzerg/mint).

## Features
- Boots silently to the system tray using ~15–20 MB RAM.
- Right-click taskbar menu to launch grouped apps with custom icons.
- Modern Fluent UI with soft Dark and Light modes.
- Reorder apps by dragging with visual insertion hints and auto-scrolling.
- Launch executables, shortcuts, scripts, documents, or URLs.
- Persistent local icon caching for instant loading.

## Usage
- **Right-click tray icon:** Opens your launcher menu.
- **Double-click tray icon:** Opens or restores the configuration window.
- **Drag files into window:** Adds apps or reorders them.

## Build from Source
### Prerequisites
- .NET 10 SDK

### Build Commands
Standalone single-file executable (runs without .NET installed):
```bash
dotnet publish Mint.csproj -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true -o ./dist
```

Lightweight framework-dependent build:
```bash
dotnet publish Mint.csproj -c Release -r win-x64 --self-contained false -p:PublishSingleFile=true -o ./dist
```

You can also trigger builds on demand via the **Actions** tab on GitHub.

## Credits
- Original concept by [hellzerg](https://github.com/hellzerg/mint).
- Completely vibe-coded with Google Gemini.
