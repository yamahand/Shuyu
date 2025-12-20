# Shuyu

Shuyu is a lightweight, open-source screen-capture utility for Windows, implemented with WPF and .NET 10.

## Key Features

- Capture arbitrary regions with an overlay selection UI
- System tray integration with context menu and settings
- Pin captured images as always-on-top windows for quick reference
- Configurable global hotkeys for fast captures
- Works across multiple monitors and handles different DPI scalings
- Built-in debug logging for troubleshooting

## Requirements

- Windows 10 or later
- .NET 10 runtime (or SDK to build from source)

## Quick Start

1. Download a release from the Releases page and extract it.
2. Run `Shuyu.exe` — the app will appear in the system tray.

Right-click the tray icon to start a capture, open Settings, or exit the app.

During a capture: click-and-drag to select a region; right-click to cancel.

## Build from Source

```powershell
git clone https://github.com/yourusername/Shuyu.git
cd Shuyu
dotnet build --configuration Release
```

Open [Shuyu.slnx](Shuyu.slnx) in Visual Studio 2022 or later for development.

## DPI test helper

This repository includes a PowerShell helper at `scripts\dpi_test.ps1` to validate captures in multi-monitor and DPI-scaled environments. It attempts multiple DPI-retrieval methods (per-monitor APIs, window DPI, device caps, and GDI+ fallbacks), computes expected pixel sizes from DIP values, captures the screen and compares results.

Run from the repository root (requires an interactive desktop session):

```powershell
pwsh -File .\scripts\dpi_test.ps1 -OutDir .\artifacts\dpi-tests -Verbose
```

The script writes PNG samples to `artifacts/dpi-tests` and logs the expected vs actual pixel sizes.

## Architecture (brief)

- `CaptureOverlayWindow`: selection overlay and UX
- `AsyncScreenCaptureService`: captures screen regions asynchronously
- `TrayService`: tray icon, context menu and commands
- `PinnedWindowManager`: creates/maintains pinned capture windows
- `HotkeyManager`: global hotkey registration and handling

The app uses async patterns for non-blocking capture and file I/O, and includes logging to help diagnose environment-specific issues (particularly DPI and multi-monitor setups).

## Contributing

- Open issues for design or feature discussions before large changes.
- Pull requests are welcome; follow the existing code style and keep changes focused.

See [CONTRIBUTING.md](CONTRIBUTING.md) for details.

## License

This project is licensed under the MIT License — see [LICENSE](LICENSE).

## Support

If you have problems or questions, please open an issue on GitHub.

