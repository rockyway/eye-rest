# PROJECT_STRUCTURE.md

| Field | Value |
|-------|-------|
| **Project** | Eye-rest |
| **Framework** | .NET 8.0 (LTS) |
| **UI Framework** | Avalonia 11.3.0 (cross-platform) + WPF (legacy Windows) |
| **Architecture** | MVVM + Service-Oriented with Platform Abstraction |
| **Solution** | `EyeRest.sln` — 8 projects |
| **Last Updated** | 2026-02-25 |

---

## Table of Contents

1. [Project Statistics](#project-statistics)
2. [Architecture Overview](#architecture-overview)
3. [Project Structure](#project-structure)
4. [Layer-by-Layer Breakdown](#layer-by-layer-breakdown)
   - [EyeRest.Abstractions](#eyerestabstractions)
   - [EyeRest.Core](#eyerestcore)
   - [EyeRest.Platform.Windows](#eyerestplatformwindows)
   - [EyeRest.Platform.macOS](#eyerestplatformmacos)
   - [EyeRest.UI](#eyerestui)
   - [EyeRest (Legacy WPF)](#eyerest-legacy-wpf)
   - [EyeRest.Tests](#eyeresttests)
   - [EyeRest.Tests.Avalonia](#eyeresttestsavalonia)
5. [Technology Stack](#technology-stack)
6. [Key Features](#key-features)
7. [Data Flow](#data-flow)
8. [Dependency Graph](#dependency-graph)
9. [Testing Strategy](#testing-strategy)
10. [Configuration & Settings](#configuration--settings)
11. [Build & Deployment](#build--deployment)
12. [Version History](#version-history)

---

## Project Statistics

| Metric | Count |
|--------|-------|
| Solution projects | 8 |
| C# source files (`.cs`) | 196 |
| Avalonia XAML (`.axaml`) | 14 |
| WPF XAML (`.xaml`) | 12 |
| Documentation files (`.md`) | 78 |
| Project files (`.csproj`) | 8 |
| Image assets (`.png`) | 21 |
| Scripts (`.ps1`, `.sh`, `.py`) | 7 |
| Total tests | 298 (240 WPF + 58 Avalonia) |

---

## Architecture Overview

### Patterns

| Pattern | Usage |
|---------|-------|
| **MVVM** | Views bind to ViewModels via Avalonia compiled bindings (`x:DataType`) |
| **Dependency Injection** | `Microsoft.Extensions.DependencyInjection` + `Microsoft.Extensions.Hosting`. Services are Singleton, ViewModels are Transient. |
| **Strategy (Platform Abstraction)** | Every platform concern is behind an interface in Abstractions. Two concrete implementations (Windows, macOS) selected at startup via `RuntimeInformation.IsOSPlatform()`. |
| **Factory** | `ITimerFactory`, `IPopupWindowFactory` |
| **Observer** | Pervasive events across TimerService, ConfigurationService, UserPresenceService, SystemTrayService |
| **Weak Event** | `WeakEventManager` using `ConditionalWeakTable` for memory-safe subscriptions |
| **Partial Class Decomposition** | TimerService split into 8 files by concern |
| **Single Instance** | Named Mutex + named pipe (Avalonia) or EventWaitHandle (WPF) |
| **Atomic Concurrency** | Triple-layer: `volatile bool` + `static lock` + `Interlocked.CompareExchange` |

---

## Project Structure

```
eye-rest/
├── EyeRest.sln                      Solution file (8 projects)
├── Directory.Build.props             TreatWarningsAsErrors, Nullable, LangVersion latest
├── appsettings.json                  Application settings
├── app.manifest                      Windows app manifest
├── CLAUDE.md                         AI agent instructions
├── master-agent.md                   Master agent protocol
│
├── EyeRest.Abstractions/            [Pure interfaces + models — zero dependencies]
│   ├── Models/                       (19 files) Configuration models, DTOs, enums
│   └── Services/                     (25 files) Service interface contracts
│
├── EyeRest.Core/                     [Platform-agnostic business logic]
│   ├── Infrastructure/               (1 file) WeakEventManager
│   ├── Services/                     (9 files) Core service implementations
│   │   └── Timer/                    (8 files) TimerService partial class files
│   └── ViewModels/                   (2 files) ViewModelBase, RelayCommand
│
├── EyeRest.Platform.Windows/        [Windows-specific implementations]
│   └── Services/                     (16 files) Audio, tray, presence, timers
│       ├── Implementation/           (4 files) Meeting detection services
│       └── Timer/                    (1 file) HybridTimer
│
├── EyeRest.Platform.macOS/          [macOS-specific implementations]
│   ├── Interop/                      (6 files) Native P/Invoke: AppKit, CoreGraphics,
│   │                                           Foundation, IOKit, ObjCRuntime,
│   │                                           UserNotifications
│   └── Services/                     (11 files) Audio, tray, presence, timers
│
├── EyeRest.UI/                       [Cross-platform Avalonia UI entry point]
│   ├── Assets/                       App icon, macOS icon
│   │   └── TrayIcons/               (18 PNGs) 9 states x 2 sizes (1x + @2x)
│   ├── Converters/                   (3 files) Avalonia value converters
│   ├── Helpers/                      (1 file) MacOSNativeWindowHelper
│   ├── Resources/                    (3 files) LightTheme, DarkTheme, GlassStyles
│   ├── Services/                     (3 files) NotificationService, PopupFactory, Dispatcher
│   ├── ViewModels/                   (2 files) MainWindowViewModel, AnalyticsDashboardViewModel
│   └── Views/                        (20 files) 10 .axaml + 10 .axaml.cs
│
├── EyeRest/                          [Legacy WPF entry point — Windows only]
│   ├── Views/                        (17 files) Legacy WPF views
│   ├── ViewModels/                   (4 files) Legacy WPF ViewModels
│   ├── Converters/                   (2 files) Legacy WPF converters
│   └── Resources/Themes/            (3 files) WPF themes
│
├── EyeRest.Tests/                    [WPF-era test suite — 240 tests, 32 files]
│   ├── E2E/                          (13 files) End-to-end tests
│   ├── Fakes/                        (3 files) FakeTimer, FakeTimerFactory, FakeDispatcherService
│   ├── Helpers/                      (1 file) TimerTestHelper
│   ├── Integration/                  (8 files) Multi-day simulations, wake recovery
│   ├── Performance/                  (2 files) Memory usage, startup (<3s)
│   ├── Services/                     (11 files) TimerService, ConfigurationService, AudioService
│   ├── UI/                           (3 files) NUnit UI automation tests
│   └── ViewModels/                   (1 file) MainWindowViewModelTests
│
├── EyeRest.Tests.Avalonia/          [Avalonia test suite — 58 tests, 5 files]
│   ├── Fakes/                        (1 file)
│   ├── Services/                     (4 files) Configuration service tests
│   └── ViewModels/                   (1 file) MainWindowViewModelTests (26 tests)
│
├── docs/                             [Documentation]
│   ├── agentic/                      (3 files) Architecture, development, testing guides
│   ├── features/                     (1 file) Break done screen enhancements
│   ├── lessons-learned/              (3 files) Crisis resolution, macOS icon sizing
│   ├── new-ui/                       (3 files) Avalonia UI design docs
│   ├── plan/                         (1 file) Popup redesign plan
│   ├── plans/                        (3 files) Design, requirements, tasks
│   ├── progress/                     (3 files) Fix summaries
│   ├── tests/                        (2 files) Test tracking, integration test plan
│   └── troubleshooting/             (7 files) Bug fix documentation
│
├── scripts/                          [Build & utility scripts]
│   ├── bundle-macos.sh              macOS .app bundling with code signing
│   └── generate-icons.py            Icon generation (Python/Pillow)
│
└── roles/                            (5 files) Agent role definitions
```

---

## Layer-by-Layer Breakdown

### EyeRest.Abstractions

> **Target:** `net8.0` | **Type:** Class Library | **Dependencies:** None

The foundational layer. Contains only interfaces and data models with zero external dependencies. Every service contract lives here so that platform-specific and core implementations can be developed independently.

**Models (19 files):**

| Model | Purpose |
|-------|---------|
| `AppConfiguration` | Root configuration object |
| `EyeRestSettings` | Eye rest timer intervals |
| `BreakSettings` | Break timer intervals |
| `AudioSettings` | Sound preferences and levels |
| `ApplicationSettings` | General app behavior |
| `UserPresenceSettings` | Idle detection thresholds |
| `MeetingDetectionSettings` | Meeting app detection config |
| `AnalyticsSettings` | Data retention, export preferences |
| `TimerControlSettings` | Timer control parameters |
| `SessionSummary` | Session statistics DTO |
| `HealthMetrics` | Health tracking data |
| `WeeklyMetrics` / `MonthlyMetrics` / `DailyMetric` | Aggregated analytics DTOs |
| `ComplianceReport` | User compliance statistics |
| `PresenceAnalytic` / `MeetingAnalytic` | Presence and meeting tracking |
| `ChartDataPoint` | UI chart data |

**Key Enums:** `RestEventType`, `UserAction`, `UserPresenceState`, `TimerType`, `TrayIconState`, `BreakAction`, `PauseReason`, `MeetingDetectionMethod`, `MeetingApplication`, `ExportFormat`

**Service Interfaces (25 files):**

| Interface | Purpose |
|-----------|---------|
| `ITimerService` | Timer lifecycle, pause/resume, warnings |
| `IApplicationOrchestrator` | Central service coordinator |
| `IConfigurationService` | Main config persistence |
| `ITimerConfigurationService` | Timer-specific config |
| `IUIConfigurationService` | UI-specific config |
| `IAnalyticsService` | SQLite analytics storage |
| `IReportingService` | Report generation (CSV, HTML, JSON, text) |
| `IAudioService` | Sound playback |
| `ISystemTrayService` | System tray icon and menu |
| `IStartupManager` | OS startup registration |
| `IScreenOverlayService` | Multi-monitor overlay |
| `IUserPresenceService` | Idle/away detection |
| `IScreenDimmingService` | Screen dimming during breaks |
| `IPauseReminderService` | Pause reminder notifications |
| `IDispatcherService` | UI thread dispatch |
| `ITimerFactory` | Platform timer creation |
| `INotificationService` | Popup notification management |
| `IPopupWindowFactory` | Popup window creation |
| `ILoggingService` | Supplementary file logging |
| `IPerformanceMonitor` | Memory/CPU monitoring |

---

### EyeRest.Core

> **Target:** `net8.0` | **Type:** Class Library | **Dependencies:** Abstractions

Platform-agnostic business logic. Contains all timer logic, configuration management, analytics, and the central orchestrator.

**Services (9 + 8 timer partials):**

| Service | Interface | Purpose |
|---------|-----------|---------|
| `ApplicationOrchestrator` | `IApplicationOrchestrator` | Central coordinator — wires all services, manages lifecycle |
| `TimerService` (8 partials) | `ITimerService` | Dual timers (eye rest 20min/20sec + break 55min/5min), warnings, pause/resume, smart pause, recovery |
| `ConfigurationService` | `IConfigurationService` | JSON persistence, atomic writes with retry |
| `TimerConfigurationService` | `ITimerConfigurationService` | Timer-specific config at `timer-config.json` |
| `UIConfigurationService` | `IUIConfigurationService` | UI-specific config at `ui-config.json` |
| `AnalyticsService` | `IAnalyticsService` | SQLite analytics (sessions, breaks, compliance) |
| `ReportingService` | `IReportingService` | Text reports from analytics data |
| `LoggingService` | `ILoggingService` | Supplementary file logger (legacy) |
| `PerformanceMonitor` | `IPerformanceMonitor` | Memory/CPU monitoring, GC trigger at 40MB |

**Infrastructure (1 file):** `WeakEventManager` — `ConditionalWeakTable`-based weak event subscriptions.

**ViewModels (2 files):**

| Class | Purpose |
|-------|---------|
| `ViewModelBase` | Abstract base with `INotifyPropertyChanged` + `SetProperty<T>` |
| `CrossPlatformRelayCommand` | `ICommand` implementation (sync + async) |

---

### EyeRest.Platform.Windows

> **Target:** `net8.0-windows10.0.19041.0` | **Type:** Class Library | **Dependencies:** Abstractions, Core

Windows-specific implementations using WPF, WinForms, WMI, and Win32 APIs.

**Services (16 files):**

| Service | Notes |
|---------|-------|
| `WindowsAudioService` | NAudio / System.Media sound playback |
| `WindowsSystemTrayService` | WinForms NotifyIcon, 9 icon states |
| `WindowsStartupManager` | Registry-based startup registration |
| `WindowsScreenOverlayService` | Multi-monitor WPF overlays |
| `WindowsUserPresenceService` | `GetLastInputInfo` P/Invoke, session switch events |
| `WindowsScreenDimmingService` | Screen dimming during breaks |
| `WindowsPauseReminderService` | Toast/balloon notifications |
| `WindowsDispatcherService` | WPF Dispatcher wrapper |
| `WindowsTimerFactory` / `HybridTimer` | DispatcherTimer + System.Timers.Timer hybrid |
| `WindowsIconService` | Icon resource management |
| Meeting detection (4 files) | Zoom, Teams, Skype, generic meeting detection via WMI |

---

### EyeRest.Platform.macOS

> **Target:** `net8.0` | **Type:** Class Library | **Dependencies:** Abstractions, Core

macOS-specific implementations using native P/Invoke into AppKit, CoreGraphics, Foundation, IOKit, and UserNotifications.

**Interop (6 files):**

| File | Native Framework |
|------|-----------------|
| `AppKit.cs` | NSApplication, NSWindow, NSStatusBar, NSMenu |
| `CoreGraphics.cs` | CGDisplay, screen enumeration |
| `Foundation.cs` | NSObject, NSString, NSAutoreleasePool |
| `IOKit.cs` | IOHIDSystem (idle time detection) |
| `ObjCRuntime.cs` | objc_msgSend, class/selector lookup |
| `UserNotifications.cs` | UNUserNotificationCenter |

**Services (11 files):**

| Service | Notes |
|---------|-------|
| `MacOSAudioService` | NSSound-based playback |
| `MacOSSystemTrayService` | NSStatusItem with NSMenu |
| `MacOSStartupManager` | Login items via SMAppService |
| `MacOSScreenOverlayService` | NSWindow overlays per screen |
| `MacOSUserPresenceService` | IOKit HID idle time |
| `MacOSScreenDimmingService` | Window-based dimming |
| `MacOSPauseReminderService` | UNUserNotificationCenter |
| `MacOSDispatcherService` | Main thread dispatch |
| `MacOSTimerFactory` / `MacOSTimer` | NSTimer-based implementation |
| `MacOSIconService` | NSImage icon management |

---

### EyeRest.UI

> **Target:** `net8.0` | **Type:** WinExe (Avalonia) | **Dependencies:** Abstractions, Core, Platform.Windows or Platform.macOS

The cross-platform Avalonia UI entry point. Contains all views, view models, converters, themes, and tray icon assets.

**Views (10 windows/controls):**

| View | Type | Size | Purpose |
|------|------|------|---------|
| `MainWindow` | Window | 340x580 | Primary settings window, tabs, timer controls |
| `AnalyticsWindow` | Window | 1200x800 | Analytics dashboard container |
| `AnalyticsDashboardView` | UserControl | — | Charts, metrics, export, retention settings |
| `PopupWindow` | Window | — | Generic popup shell (no chrome, transparent) |
| `EyeRestPopup` | UserControl | — | Eye rest reminder card with countdown |
| `EyeRestWarningPopup` | UserControl | — | Pre-eye-rest 15s warning |
| `BreakPopup` | UserControl | — | Break countdown with Delay/Skip/Done actions |
| `BreakWarningPopup` | UserControl | — | Pre-break 30s warning |
| `AboutWindow` | Window | 360x340 | App info dialog |
| `ConfirmDialog` | Window | — | Generic confirmation dialog |

**ViewModels (2 files):**

| ViewModel | Purpose |
|-----------|---------|
| `MainWindowViewModel` | Main settings/status — timer settings, audio, presence, commands |
| `AnalyticsDashboardViewModel` | Analytics dashboard — charts, compliance, export |

**UI Services (3 files):**

| Service | Purpose |
|---------|---------|
| `AvaloniaNotificationService` | Popup notification management |
| `AvaloniaPopupWindowFactory` | Popup window creation |
| `AvaloniaDispatcherService` | Avalonia UI thread dispatch |

**Resources:** `LightTheme.axaml`, `DarkTheme.axaml`, `GlassStyles.axaml` — glass card aesthetic with mesh gradients.

**Tray Icons:** 18 PNG files — 9 states (active, paused, meeting, warning, break, idle, disabled, error, default) at 2 sizes (1x, @2x Retina).

---

### EyeRest (Legacy WPF)

> **Target:** `net8.0-windows10.0.19041.0` | **Type:** WinExe | **Dependencies:** Abstractions, Core, Platform.Windows

**OBSOLETE — Read-Only.** Original Windows-only WPF entry point. Retained for reference only. Do not modify. Contains 17 views, 4 view models, 2 converters, and 3 WPF themes. All active development targets `EyeRest.UI` (Avalonia).

---

### EyeRest.Tests

> **Target:** `net8.0-windows10.0.19041.0` | **Type:** Test | **Tests:** 240 across 32 files

| Category | Files | Description |
|----------|-------|-------------|
| E2E | 13 | End-to-end workflow tests |
| Integration | 8 | Multi-day simulations, wake recovery |
| Services | 11 | TimerService, ConfigurationService, AudioService unit tests |
| Performance | 2 | Memory usage (<50MB idle), startup time (<3s) |
| UI | 3 | NUnit + TestStack.White UI automation |
| ViewModels | 1 | MainWindowViewModelTests |
| Fakes | 3 | FakeTimer, FakeTimerFactory, FakeDispatcherService |
| Helpers | 1 | TimerTestHelper |

---

### EyeRest.Tests.Avalonia

> **Target:** `net8.0` | **Type:** Test | **Tests:** 58 across 5 files

| Category | Files | Description |
|----------|-------|-------------|
| Services | 4 | Configuration service tests |
| ViewModels | 1 | MainWindowViewModelTests (26 tests) |
| Fakes | 1 | Test doubles |

---

## Technology Stack

| Technology | Version | Purpose |
|------------|---------|---------|
| .NET | 8.0 | Runtime and SDK (LTS, macOS 12+ compatible) |
| Avalonia | 11.3.0 | Cross-platform UI framework |
| FluentAvaloniaUI | 2.4.0 | Fluent Design styles for Avalonia |
| WPF / WinForms | net8.0-windows | Windows-specific UI (legacy + platform services) |
| Microsoft.Extensions.DependencyInjection | 8.0.0 | Dependency injection container |
| Microsoft.Extensions.Hosting | 8.0.0 | Generic host for app lifecycle |
| Serilog | 4.3.0 | Structured logging |
| Microsoft.Data.Sqlite | 8.0.0 / 8.0.8 | SQLite for analytics database |
| System.Text.Json | 8.0.5 | JSON serialization for config files |
| System.Management | 8.0.0 | Windows WMI (meeting detection) |
| ModernWpfUI | 0.9.6 | Modern WPF styles (legacy entry point) |
| xUnit | 2.6.1 | Primary test framework |
| NUnit | 3.14.0 | Secondary test framework (UI automation) |
| Moq | 4.20.69 | Mocking library |
| TestStack.White | 0.13.3 | UI automation framework |

---

## Key Features

1. **Dual Timer System** — Eye rest (20min interval / 20sec popup) + Break (55min interval / 5min popup), fully configurable.
2. **Warning System** — Pre-notification countdowns (15s for eye rest, 30s for break) with fallback guard timers.
3. **Smart Pause** — Auto-pause on idle / screen lock / user away; auto-resume on return.
4. **Session Reset** — Extended away detection (>30min) triggers full session reset.
5. **System Tray Integration** — 9 icon states, context menu with pause / meeting mode / analytics access.
6. **Analytics Dashboard** — SQLite-backed compliance tracking, charts, CSV / JSON / HTML / text export.
7. **Multi-Monitor Support** — Popup positioning and screen overlay across all monitors during breaks.
8. **Audio Notifications** — 5-level audio cascade with custom sound file support.
9. **Configuration Management** — 3 JSON config files, atomic writes, 1.5s debounced saves.
10. **Cross-Platform** — Windows (WPF/WinForms) + macOS (native P/Invoke via AppKit/IOKit).
11. **macOS .app Bundle** — Code-signed with hardened runtime, generated via `scripts/bundle-macos.sh`.
12. **Theming** — Light and dark themes with glass card aesthetic and mesh gradients.

---

## Data Flow

### Application Startup

```
Host.StartAsync()
  └─► ApplicationOrchestrator.InitializeAsync()
        ├─► TimerService.StartAsync()
        │     ├─► Load configuration
        │     ├─► Initialize 8 internal timers
        │     └─► Start main countdown timers
        ├─► UserPresenceService.StartMonitoringAsync()
        └─► AnalyticsService.InitializeDatabaseAsync()
```

### Timer Cycle

```
Main timer fires
  └─► Warning phase (15s or 30s countdown popup)
        └─► Fallback guard timer
              └─► Due phase
                    └─► NotificationService shows popup
                          └─► Audio plays
                                └─► User action (complete / skip / delay)
                                      ├─► Timer restarts
                                      └─► Analytics recorded
```

### Configuration Change

```
UI change (ViewModel setter)
  └─► 1.5s debounce
        └─► ConfigService.SaveAsync()
              └─► Atomic JSON write (tmp → backup → move)
                    └─► ConfigurationChanged event
                          └─► Services reload settings
```

### Presence Detection

```
Idle detected
  └─► SmartPauseAsync() → Timers pause

User returns
  └─► SmartResumeAsync() → Timers resume

Extended away (>30min)
  └─► SmartSessionResetAsync() → Full timer reset
```

---

## Dependency Graph

```
EyeRest.Abstractions              (leaf — no dependencies)
         ▲
         │
EyeRest.Core                      ──► Abstractions
         ▲
         │
EyeRest.Platform.Windows          ──► Abstractions + Core
EyeRest.Platform.macOS             ──► Abstractions + Core
         ▲
         │
EyeRest.UI                        ──► Abstractions + Core + Platform.Windows (Win)
                                                           / Platform.macOS (macOS)
EyeRest (Legacy WPF)              ──► Abstractions + Core + Platform.Windows

EyeRest.Tests                     ──► EyeRest (Legacy) + Core + Abstractions
EyeRest.Tests.Avalonia             ──► EyeRest.UI + Core + Abstractions
```

---

## Testing Strategy

| Aspect | Details |
|--------|---------|
| **Total tests** | 298 (240 WPF + 58 Avalonia) |
| **Primary framework** | xUnit 2.6.1 |
| **Secondary framework** | NUnit 3.14.0 (UI automation only) |
| **Mocking** | Moq 4.20.69 |
| **Naming convention** | `MethodName_StateUnderTest_ExpectedBehavior` |
| **Pattern** | Arrange-Act-Assert with constructor-based setup |
| **Test doubles** | FakeTimer, FakeTimerFactory, FakeDispatcherService |

**Test categories and commands:**

```bash
dotnet test                              # Run all tests
dotnet test --filter Category=Unit       # Unit tests only
dotnet test --filter Category=Integration # Integration tests
dotnet test --filter Category=Performance # Performance benchmarks
run-ui-tests.bat                         # UI automation (Windows)
```

**Performance requirements enforced by tests:**
- Startup time: < 3 seconds
- Memory idle: < 50MB
- Memory active: < 100MB

---

## Configuration & Settings

Three JSON configuration files stored under `%APPDATA%\EyeRest\` (Windows) or `~/.config/EyeRest/` (macOS):

| File | Service | Contents |
|------|---------|----------|
| `config.json` | `ConfigurationService` | Full `AppConfiguration` — all settings |
| `timer-config.json` | `TimerConfigurationService` | Timer intervals only |
| `ui-config.json` | `UIConfigurationService` | Audio, app behavior, analytics |

**Write strategy:** Atomic file operations — write to temp file, create backup, move to overwrite. 3 retries with exponential backoff on failure.

**Save debouncing:** UI changes trigger a 1.5-second debounce before persisting to disk.

---

## Build & Deployment

| Task | Command / Script |
|------|-----------------|
| Build | `dotnet build` |
| Run | `dotnet run --project EyeRest.UI` |
| Publish | `dotnet publish` |
| macOS .app bundle | `scripts/bundle-macos.sh` — self-contained publish, .app structure, code signing with hardened runtime |
| Icon generation | `scripts/generate-icons.py` (requires Python + Pillow) |

**Solution-wide properties** (`Directory.Build.props`):
- `TreatWarningsAsErrors`: true
- `Nullable`: enable
- `LangVersion`: latest

**CI/CD:** Not yet configured.

---

## Version History

| Date | Change |
|------|--------|
| 2026-02-25 | Downgraded to .NET 8 LTS for macOS 12+ compatibility |
| 2026-02-25 | Merged Avalonia UI, macOS bundling, and code signing |
| 2026-02-25 | Added macOS .app bundling with code signing |
| 2026-02-25 | Merged new UI feature branch into master |
| 2026-02-25 | Added lessons learned, new UI references, popup redesign plan |
