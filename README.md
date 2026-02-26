# Eye-Rest

A cross-platform desktop application built with .NET 8 and Avalonia UI that provides automated eye rest and break reminders to promote healthy computer usage habits. Supports Windows and macOS.

## Features

### Core Functionality
- **Eye Rest Reminders**: Automated 20-second eye rest reminders every 20 minutes (configurable)
- **Break Reminders**: 5-minute break reminders every 55 minutes (configurable) with pre-break warnings
- **Smart Pause**: Auto-pause on idle, screen lock, or user away; auto-resume on return
- **Audio Notifications**: Optional sound alerts for reminder start/end events
- **System Tray Integration**: Runs unobtrusively in system tray (Windows) or menu bar (macOS)
- **Multi-Monitor Support**: Full-screen popups work across multiple monitors

### User Experience
- **Glass-Morphism UI**: Modern design with light and dark themes
- **Intuitive Settings**: Easy-to-use configuration with real-time preview
- **Break Controls**: Delay (1 or 5 minutes) or skip break options
- **Analytics Dashboard**: Track compliance, view charts, export data (CSV/JSON/HTML)
- **Startup Integration**: Optional system startup registration on both platforms

### Performance & Reliability
- **Fast Startup**: Application starts in under 3 seconds
- **Low Memory Usage**: Stays under 100MB memory consumption
- **Error Recovery**: Automatic recovery from timer failures
- **Comprehensive Logging**: Detailed logging with automatic cleanup

## Architecture

| Layer | Project | Purpose |
|-------|---------|---------|
| Abstractions | `EyeRest.Abstractions` | Pure interfaces and models (zero dependencies) |
| Core | `EyeRest.Core` | Platform-agnostic business logic, timers, analytics |
| Windows | `EyeRest.Platform.Windows` | Windows-specific services (WPF, Win32, WMI) |
| macOS | `EyeRest.Platform.macOS` | macOS-specific services (AppKit, IOKit P/Invoke) |
| UI | `EyeRest.UI` | Cross-platform Avalonia UI entry point |
| Tests | `EyeRest.Tests.Avalonia` | Test suite (86+ tests) |

### Design Patterns
- **MVVM** with Avalonia compiled bindings
- **Dependency Injection** via Microsoft.Extensions.DependencyInjection
- **Platform Abstraction** with strategy pattern for Windows/macOS
- **Observer Pattern** for event-driven service communication

## Requirements

- .NET 8.0 SDK
- **Windows**: Windows 10 (1903+) or Windows 11
- **macOS**: macOS 12 (Monterey) or later, ARM64

## Getting Started

```bash
# Build
dotnet build

# Run
dotnet run --project EyeRest.UI

# Test
dotnet test
dotnet test --filter Category=Unit
dotnet test --filter Category=Integration
```

### Platform-Specific Builds

```bash
# Windows MSIX (for Microsoft Store)
.\scripts\build-msix.ps1 -ForStore

# macOS .app bundle (requires code signing)
./scripts/bundle-macos.sh
```

## Configuration

Settings are stored in JSON files at:
- **Windows**: `%APPDATA%\EyeRest\`
- **macOS**: `~/.config/EyeRest/`

| File | Contents |
|------|----------|
| `config.json` | All application settings |
| `timer-config.json` | Timer intervals |
| `ui-config.json` | Audio, app behavior, analytics |

## Technology Stack

| Technology | Version | Purpose |
|------------|---------|---------|
| .NET | 8.0 LTS | Runtime |
| Avalonia | 11.3.0 | Cross-platform UI |
| FluentAvaloniaUI | 2.4.0 | Fluent Design styles |
| Serilog | 4.3.0 | Structured logging |
| Microsoft.Data.Sqlite | 8.0.0 | Analytics database |
| xUnit + Moq | — | Testing |

## License

MIT License. See [LICENSE](LICENSE) for details.

## Contributing

See [CONTRIBUTING.md](CONTRIBUTING.md) for development setup and guidelines.
