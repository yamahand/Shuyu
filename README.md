# Shuyu

A lightweight and efficient screen capture application for Windows, built with WPF and .NET 9.

## 🌐 Languages
- [English](README.md)
- [日本語](README.ja.md)

## 📸 Features

- **Screen Capture**: Capture selected areas of your screen with precision
- **Overlay Selection**: Intuitive overlay interface for selecting capture regions
- **System Tray Integration**: Runs quietly in the system tray for quick access
- **Pinned Windows**: Pin captured images for easy reference
- **Hotkey Support**: Quick capture with customizable keyboard shortcuts
- **Multiple Display Support**: Works seamlessly with multi-monitor setups
- **Debug Logging**: Built-in logging system for troubleshooting

## 🚀 Getting Started

### Prerequisites

- Windows 10/11
- .NET 9.0 Runtime

### Installation

1. Download the latest release from the [Releases](../../releases) page
2. Extract the archive to your desired location
3. Run `Shuyu.exe`

### Building from Source

```bash
git clone https://github.com/yourusername/Shuyu.git
cd Shuyu
dotnet build --configuration Release
```

## 🎯 Usage

1. Launch Shuyu - it will appear in your system tray
2. Right-click the tray icon to access options:
   - **Capture**: Start a new screen capture
   - **Settings**: Configure application preferences
   - **Exit**: Close the application
3. During capture:
   - Click and drag to select the area you want to capture
   - Right-click to cancel
   - The captured image will be saved and can be pinned for reference

## ⚙️ Configuration

The application stores settings automatically. You can access configuration through the Settings window from the system tray menu.

## 🏗️ Architecture

- **WPF Application**: Modern Windows desktop application framework
- **Service Layer**: Modular services for capture, logging, and system integration
- **Async Operations**: Non-blocking screen capture and file operations
- **Multi-DPI Aware**: Proper handling of different display scaling

### Key Components

- `CaptureOverlayWindow`: Full-screen overlay for area selection
- `AsyncScreenCaptureService`: Handles screen capture operations
- `TrayService`: System tray integration and context menu
- `PinnedWindowManager`: Manages pinned capture windows
- `HotkeyManager`: Global hotkey registration and handling

## 🤝 Contributing

Contributions are welcome! Please feel free to submit a Pull Request. For major changes, please open an issue first to discuss what you would like to change.

### Development Setup

1. Clone the repository
2. Open `Shuyu.sln` in Visual Studio 2022 or later
3. Build and run the project

## 📝 License

This project is licensed under the MIT License - see the [LICENSE](LICENSE) file for details.

## 🙏 Acknowledgments

- Built with WPF and .NET 9
- Uses System.Drawing.Common for image processing
- Inspired by modern screen capture tools

## 📞 Support

If you encounter any issues or have questions, please [open an issue](../../issues) on GitHub.

