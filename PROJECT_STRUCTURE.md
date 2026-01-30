# Eye-rest - Project Structure

Eye-rest is a Windows desktop application built with .NET 8 and WPF that provides automated eye rest and break reminders. The application uses MVVM architecture with dependency injection, runs as a system tray application, and includes advanced features like user presence detection, analytics tracking, and smart session management.

**Application Version:** 1.0.0.0
**Target Framework:** .NET 8.0-windows10.0.19041.0
**Architecture Style:** MVVM with Service-Oriented Design
**Last Updated:** 2026-01-29

---

## Project Statistics

| Component | Files | Lines of Code |
|-----------|-------|---------------|
| Main Project (C#) | 99 | ~27,000 |
| Test Project (C#) | 43 | ~12,000 |
| XAML Files | 12 | ~2,500 |
| **Total** | **142** | **~39,500** |

### Source File Distribution

| Directory | C# Files | Purpose |
|-----------|----------|---------|
| Services/ | 45 | Business logic and system integration |
| Services/Abstractions/ | 2 | Service interface contracts |
| Services/Implementation/ | 4 | Timer implementations |
| Services/Timer/ | 9 | Timer service partials |
| ViewModels/ | 4 | MVVM presentation logic |
| Views/ | 8 | WPF XAML windows |
| Models/ | 19 | Configuration and data models |
| Infrastructure/ | 1 | Utilities |
| Converters/ | 2 | WPF value converters |

### Test File Distribution

| Test Category | Files | Purpose |
|---------------|-------|---------|
| E2E/ | 12 | End-to-end workflow tests |
| Integration/ | 8 | Service integration tests |
| Services/ | 11 | Service unit tests |
| UI/ | 3 | UI automation tests |
| Performance/ | 2 | Performance validation |
| Fakes/ | 2 | Test doubles |
| Helpers/ | 1 | Test utilities |
| ViewModels/ | 1 | ViewModel unit tests |

---

## Table of Contents

1. [Quick Reference](#quick-reference)
2. [Architecture Overview](#architecture-overview)
3. [Project Structure](#project-structure)
4. [Layer-by-Layer Breakdown](#layer-by-layer-breakdown)
5. [Technology Stack](#technology-stack)
6. [Key Features](#key-features)
7. [Data Flow](#data-flow)
8. [Testing Strategy](#testing-strategy)
9. [Configuration & Settings](#configuration--settings)
10. [Build & Development](#build--development)
11. [Dependencies](#dependencies)

---

## Quick Reference

### Build Commands
```bash
dotnet build                         # Debug build
dotnet build --configuration Release # Release build
dotnet run                          # Run application
dotnet test                         # Run all tests
```

### Key Paths
- **Configuration**: `%APPDATA%\EyeRest\config.json`
- **Logs**: `%APPDATA%\EyeRest\logs\eyerest.log`
- **Analytics DB**: `%APPDATA%\EyeRest\analytics.db`

### Core Services (DI Container)
| Service | Interface | Responsibility |
|---------|-----------|----------------|
| ApplicationOrchestrator | IApplicationOrchestrator | Central coordinator |
| TimerService | ITimerService | Dual-timer management |
| NotificationService | INotificationService | Popup management |
| ConfigurationService | IConfigurationService | Settings persistence |
| UserPresenceService | IUserPresenceService | User activity detection |
| AnalyticsService | IAnalyticsService | Usage tracking & reporting |

---

## Architecture Overview

### Design Patterns

| Pattern | Implementation | Location |
|---------|----------------|----------|
| **MVVM** | ViewModelBase, RelayCommand | ViewModels/ |
| **Dependency Injection** | Microsoft.Extensions.DI | App.xaml.cs |
| **Orchestrator** | ApplicationOrchestrator | Services/ |
| **Observer** | Events & INotifyPropertyChanged | Throughout |
| **Factory** | ITimerFactory | Services/Abstractions/ |
| **Partial Classes** | TimerService partials | Services/Timer/ |

### Core Principles
- **SOLID**: Interface-based services, single responsibility
- **DRY**: Shared timer calculation methods
- **Separation of Concerns**: Services, ViewModels, Views clearly separated
- **Event-Driven**: Timer → Orchestrator → Notification chain

### Architecture Diagram
```
┌─────────────────────────────────────────────────────────────────┐
│                         App.xaml.cs                             │
│                    (DI Container Setup)                         │
└─────────────────────────┬───────────────────────────────────────┘
                          │
┌─────────────────────────▼───────────────────────────────────────┐
│                 ApplicationOrchestrator                         │
│         (Central Coordinator - Event Routing)                   │
└──────┬──────────┬──────────┬──────────┬──────────┬─────────────┘
       │          │          │          │          │
       ▼          ▼          ▼          ▼          ▼
┌──────────┐ ┌──────────┐ ┌──────────┐ ┌──────────┐ ┌──────────┐
│  Timer   │ │Notifica- │ │  Audio   │ │User Pre- │ │Analytics │
│ Service  │ │  tion    │ │ Service  │ │  sence   │ │ Service  │
│          │ │ Service  │ │          │ │ Service  │ │          │
└──────────┘ └──────────┘ └──────────┘ └──────────┘ └──────────┘
       │          │
       │          ▼
       │    ┌──────────────────────────────────────┐
       │    │            Views/Popups              │
       │    │  EyeRestPopup, BreakPopup, etc.     │
       │    └──────────────────────────────────────┘
       │
       ▼
┌──────────────────────────────────────┐
│         System Tray Service          │
│     (Icon, Menu, Notifications)      │
└──────────────────────────────────────┘
```

---

## Project Structure

```
EyeRest/
├── 📁 Services/                    (45 files) [Business Logic Layer]
│   ├── 📁 Abstractions/           (2 files)  [Service Interfaces]
│   │   ├── ITimer.cs
│   │   └── ITimerFactory.cs
│   ├── 📁 Implementation/         (4 files)  [Timer Implementations]
│   │   ├── HybridTimer.cs
│   │   ├── HybridTimerFactory.cs
│   │   ├── ProductionTimer.cs
│   │   └── ProductionTimerFactory.cs
│   ├── 📁 Timer/                  (9 files)  [Timer Service Partials]
│   │   ├── TimerService.cs                   [Core structure]
│   │   ├── TimerService.State.cs             [State management]
│   │   ├── TimerService.Initialization.cs    [Startup logic]
│   │   ├── TimerService.Lifecycle.cs         [Start/Stop/Pause]
│   │   ├── TimerService.EventHandlers.cs     [Timer event handlers]
│   │   ├── TimerService.Coordination.cs      [Timer coordination]
│   │   ├── TimerService.PauseManagement.cs   [Pause logic]
│   │   ├── TimerService.Recovery.cs          [Error recovery]
│   │   └── ITimerWrapper.cs
│   ├── ApplicationOrchestrator.cs           [Central coordinator]
│   ├── NotificationService.cs               [Popup management]
│   ├── ConfigurationService.cs              [JSON settings]
│   ├── AudioService.cs                      [Sound notifications]
│   ├── SystemTrayService.cs                 [System tray]
│   ├── IconService.cs                       [Tray icons]
│   ├── UserPresenceService.cs               [Activity detection]
│   ├── AnalyticsService.cs                  [Usage tracking]
│   ├── ReportingService.cs                  [Health reports]
│   ├── PauseReminderService.cs              [Pause notifications]
│   ├── PerformanceMonitor.cs                [Resource monitoring]
│   ├── StartupManager.cs                    [Windows startup]
│   ├── LoggingService.cs                    [Application logging]
│   ├── ScreenOverlayService.cs              [Screen dimming]
│   ├── ScreenDimmingService.cs              [Overlay effects]
│   ├── MeetingDetectionManager.cs           [Meeting detection]
│   ├── WindowBasedMeetingDetectionService.cs
│   ├── NetworkBasedMeetingDetectionService.cs
│   ├── HybridMeetingDetectionService.cs
│   ├── MeetingDetectionServiceFactory.cs
│   ├── WindowsNetworkEndpointMonitor.cs
│   ├── WindowsProcessMonitor.cs
│   ├── TimerConfigurationService.cs
│   ├── UIConfigurationService.cs
│   └── [Interface files: I*.cs]             (18 interface files)
│
├── 📁 ViewModels/                  (4 files)  [Presentation Logic]
│   ├── MainWindowViewModel.cs               [Main window logic]
│   ├── AnalyticsDashboardViewModel.cs       [Dashboard logic]
│   ├── ViewModelBase.cs                     [INotifyPropertyChanged]
│   └── RelayCommand.cs                      [ICommand implementation]
│
├── 📁 Views/                       (8 files)  [WPF Windows]
│   ├── MainWindow.xaml(.cs)                 [Main application window]
│   ├── BasePopupWindow.xaml(.cs)            [Base popup class]
│   ├── EyeRestPopup.xaml(.cs)               [Eye rest reminder]
│   ├── EyeRestWarningPopup.xaml(.cs)        [Eye rest warning]
│   ├── BreakPopup.xaml(.cs)                 [Break reminder]
│   ├── BreakWarningPopup.xaml(.cs)          [Break warning]
│   ├── AnalyticsWindow.xaml(.cs)            [Analytics dashboard]
│   └── AnalyticsDashboardView.xaml(.cs)     [Dashboard view]
│
├── 📁 Models/                      (19 files) [Data Models]
│   ├── AppConfiguration.cs                  [Main config model]
│   ├── TimerConfiguration.cs                [Timer settings]
│   ├── UIConfiguration.cs                   [UI settings]
│   ├── MeetingDetectionConfiguration.cs     [Meeting config]
│   ├── AnalyticsModels.cs                   [Analytics DTOs]
│   ├── AnalyticsEnums.cs                    [Analytics enums]
│   ├── SessionSummary.cs                    [Session data]
│   ├── ComplianceReport.cs                  [Compliance data]
│   ├── ComplianceTrend.cs                   [Trend data]
│   ├── HealthMetrics.cs                     [Health metrics]
│   ├── DailyMetric.cs                       [Daily stats]
│   ├── MeetingStats.cs                      [Meeting stats]
│   ├── MeetingApplication.cs                [Meeting app enum]
│   ├── MeetingDetectionMethod.cs            [Detection method enum]
│   ├── NetworkEndpoint.cs                   [Network endpoint model]
│   ├── PauseReason.cs                       [Pause reason enum]
│   ├── ResumeReason.cs                      [Resume reason enum]
│   ├── ChartType.cs                         [Chart type enum]
│   └── ExportFormat.cs                      [Export format enum]
│
├── 📁 Infrastructure/              (1 file)   [Utilities]
│   └── WeakEventManager.cs                  [Memory leak prevention]
│
├── 📁 Converters/                  (2 files)  [WPF Converters]
│   ├── BooleanToVisibilityConverter.cs
│   └── ChartConverters.cs
│
├── 📁 Resources/                              [Assets]
│   ├── 📁 Themes/
│   │   ├── DefaultTheme.xaml
│   │   ├── DarkTheme.xaml
│   │   └── LightTheme.xaml
│   └── app.ico
│
├── 📁 EyeRest.Tests/               (43 files) [Test Suite]
│   ├── 📁 Services/               (11 tests)
│   ├── 📁 Integration/            (8 tests)
│   ├── 📁 E2E/                    (12 tests)
│   ├── 📁 Performance/            (2 tests)
│   ├── 📁 UI/                     (3 tests)
│   ├── 📁 Fakes/                  (2 files)
│   ├── 📁 Helpers/                (1 file)
│   ├── 📁 EndToEnd/               (1 test)
│   └── 📁 ViewModels/             (1 test)
│
├── 📁 docs/                        (31 files) [Documentation]
│   ├── 📁 plans/
│   ├── 📁 progress/
│   ├── 📁 features/
│   ├── 📁 troubleshooting/
│   ├── 📁 lessons-learned/
│   ├── 📁 tests/
│   └── 📁 agentic/
│
├── App.xaml(.cs)                            [Application entry]
├── EyeRest.csproj                           [Project file]
├── EyeRest.sln                              [Solution file]
├── CLAUDE.md                                [Development guidance]
├── appsettings.json                         [App settings]
└── app.manifest                             [Windows manifest]
```

---

## Layer-by-Layer Breakdown

### 1. Application Entry (App.xaml.cs)

**Purpose**: Application bootstrap, DI container setup, startup orchestration

**Key Responsibilities**:
- Configures Microsoft.Extensions.DependencyInjection container
- Initializes Serilog logging
- Implements single-instance mutex
- Handles global exception handling
- Manages application lifecycle events

**Notable Features**:
- HybridTimerFactory for robust timer creation
- Phased startup logging (PHASE 1-4)
- System tray initialization before main window

### 2. Services Layer

#### ApplicationOrchestrator
**File**: `Services/ApplicationOrchestrator.cs` (1,253 lines)

**Purpose**: Central coordinator managing all service interactions

**Key Features**:
- Subscribes to TimerService events (EyeRestWarning, EyeRestDue, BreakWarning, BreakDue)
- Coordinates NotificationService and AudioService
- Handles user presence changes with smart pause/resume
- Extended away session detection and smart reset
- System tray icon state management
- Analytics event recording
- Session validation timer (15-minute intervals)

#### TimerService (Partial Classes)
**Files**: `Services/Timer/TimerService*.cs` (9 files)

**Purpose**: Dual-timer system for eye rest (20min) and breaks (55min)

**Partials**:
| File | Responsibility |
|------|----------------|
| TimerService.cs | Core structure, public interface |
| TimerService.State.cs | State fields, properties |
| TimerService.Initialization.cs | Timer setup, startup |
| TimerService.Lifecycle.cs | Start, Stop, Pause, Resume |
| TimerService.EventHandlers.cs | Timer tick handlers |
| TimerService.Coordination.cs | Smart timer coordination |
| TimerService.PauseManagement.cs | Pause logic, meeting pause |
| TimerService.Recovery.cs | System resume recovery |

**Key Features**:
- DispatcherTimer for UI thread safety
- Warning timers (30s before events)
- Fallback timers for reliability
- Health monitor timer
- Timeline protection (prevents double triggers)
- Processing flags for synchronization

#### NotificationService
**Purpose**: Full-screen popup management across all monitors

**Features**:
- Multi-monitor support with BasePopupWindow
- Warning popups (EyeRestWarningPopup, BreakWarningPopup)
- Main popups (EyeRestPopup, BreakPopup)
- Countdown timer display
- User action handling (Delay, Skip, Complete)

#### UserPresenceService
**Purpose**: Detects user activity and system state changes

**Features**:
- Idle detection via Windows API (GetLastInputInfo)
- Session state monitoring (lock, unlock)
- Power event monitoring (sleep, resume)
- Extended away detection (30+ minutes)
- Smart session reset triggering

#### AnalyticsService
**Purpose**: Usage tracking and health metrics

**Features**:
- SQLite database storage
- Break/eye rest event recording
- Presence change tracking
- Session metrics
- Health reports and compliance rates
- Data export (JSON format)

### 3. ViewModels Layer

#### MainWindowViewModel
**Purpose**: Main window presentation logic

**Features**:
- Timer countdown display binding
- Settings management
- Command handling (Pause, Resume, Reset)
- INotifyPropertyChanged implementation

#### AnalyticsDashboardViewModel
**Purpose**: Analytics dashboard presentation

**Features**:
- Date range selection
- Compliance statistics
- Chart data preparation
- Export functionality

### 4. Views Layer

#### Popup Hierarchy
```
BasePopupWindow (abstract)
├── EyeRestWarningPopup
├── EyeRestPopup
├── BreakWarningPopup
└── BreakPopup
```

**BasePopupWindow Features**:
- Multi-monitor positioning
- Topmost window management
- Escape key handling
- Window chrome removal

#### MainWindow
- Settings UI with tabs
- Timer countdown display
- System tray minimization
- Theme switching

### 5. Models Layer

#### AppConfiguration
Main configuration model with nested settings:

```csharp
AppConfiguration
├── EyeRestSettings      // interval, duration, warning
├── BreakSettings        // interval, duration, confirmation
├── AudioSettings        // enabled, volume, custom sounds
├── ApplicationSettings  // startup, tray, theme
├── UserPresenceSettings // idle detection, smart reset
├── MeetingDetectionSettings // detection method, apps
├── AnalyticsSettings    // retention, export, tracking
└── TimerControlSettings // pause, reminders
```

---

## Technology Stack

### Core Framework
| Technology | Version | Purpose |
|------------|---------|---------|
| .NET | 8.0 | Runtime framework |
| WPF | Built-in | UI framework |
| Windows Forms | Built-in | System tray (NotifyIcon) |
| C# | 12.0 | Primary language |

### NuGet Packages

| Package | Version | Purpose |
|---------|---------|---------|
| Microsoft.Extensions.DependencyInjection | 8.0.0 | IoC container |
| Microsoft.Extensions.Hosting | 8.0.0 | Application hosting |
| Microsoft.Extensions.Logging | 8.0.0 | Logging abstractions |
| Microsoft.Extensions.Configuration.Json | 8.0.0 | JSON configuration |
| Serilog | 4.3.0 | Structured logging |
| Serilog.Extensions.Hosting | 8.0.0 | Serilog hosting integration |
| Serilog.Sinks.Console | 6.0.0 | Console logging |
| Serilog.Sinks.File | 7.0.0 | File logging |
| System.Text.Json | 8.0.5 | JSON serialization |
| System.Management | 8.0.0 | WMI queries |
| System.Drawing.Common | 8.0.0 | Graphics support |
| Microsoft.Data.Sqlite | 8.0.0 | SQLite database |
| Microsoft.WindowsAPICodePack-Shell | 1.1.0 | Windows shell integration |
| ModernWpfUI | 0.9.6 | Modern Windows 11 styling |

### Test Packages

| Package | Version | Purpose |
|---------|---------|---------|
| xunit | 2.6.1 | Test framework |
| xunit.runner.visualstudio | 2.5.3 | VS test runner |
| Moq | 4.20.69 | Mocking framework |
| Microsoft.NET.Test.Sdk | 17.8.0 | Test SDK |
| TestStack.White | 0.13.3 | UI automation |
| NUnit | 3.14.0 | Additional test framework |
| Xunit.StaFact | 1.1.11 | STA thread tests |

---

## Key Features

### 1. Dual Timer System
- **Eye Rest Timer**: 20-minute intervals, 20-second breaks
- **Break Timer**: 55-minute intervals, 5-minute breaks
- **Warning System**: 30-second pre-notifications
- **Smart Coordination**: Timers pause each other during active notifications

### 2. User Presence Detection
- Idle detection (configurable threshold)
- Screen lock/unlock monitoring
- System sleep/wake monitoring
- Extended away session detection (30+ minutes)
- Smart session reset on return

### 3. Analytics & Reporting
- Break completion tracking
- Compliance rate calculations
- Daily/weekly health metrics
- Data export capabilities
- SQLite database storage

### 4. Multi-Monitor Support
- Full-screen popups on all monitors
- Per-monitor DPI awareness
- Proper window positioning

### 5. System Tray Integration
- Minimize to tray
- Context menu (Pause, Resume, Status, Exit)
- Live tooltip with timer countdowns
- State-based icon changes

### 6. Meeting Detection (Disabled)
- Window-based detection
- Network-based detection
- Hybrid detection mode
- Auto-pause during meetings

---

## Data Flow

### Timer Event Flow
```
TimerService.Tick
    │
    ├─[Warning]──► ApplicationOrchestrator.OnXxxWarning
    │                   │
    │                   ├─► AudioService.PlayWarningSound
    │                   └─► NotificationService.ShowWarningPopup
    │
    └─[Due]──────► ApplicationOrchestrator.OnXxxDue
                        │
                        ├─► AudioService.PlayStartSound
                        ├─► NotificationService.ShowPopup
                        │       │
                        │       └─► User Action (Complete/Skip/Delay)
                        │               │
                        ├─► AnalyticsService.RecordEvent
                        └─► TimerService.Restart
```

### User Presence Flow
```
UserPresenceService.Monitor
    │
    ├─[Idle/Away]──► UserPresenceChanged Event
    │                   │
    │                   └─► ApplicationOrchestrator.OnUserPresenceChanged
    │                           │
    │                           ├─► TimerService.SmartPause
    │                           ├─► AnalyticsService.PauseSession
    │                           └─► SystemTrayService.UpdateIcon
    │
    └─[Present]────► ExtendedAwaySessionDetected (if >30 min)
                        │
                        └─► TimerService.SmartSessionReset
```

---

## Testing Strategy

### Test Categories

| Category | Files | Purpose | Command Filter |
|----------|-------|---------|----------------|
| Unit | 11 | Service isolation tests | `--filter Category=Unit` |
| Integration | 8 | Service interaction tests | `--filter Category=Integration` |
| Performance | 2 | Startup/memory validation | `--filter Category=Performance` |
| E2E | 12 | Complete workflow tests | `--filter Category=E2E` |

### Test Infrastructure

**FakeTimer & FakeTimerFactory**: Test doubles for timer control in unit tests

**TimerTestHelper**: Utilities for timer testing

**UIAutomationFramework**: TestStack.White wrapper for UI tests

### Performance Requirements
- **Startup Time**: < 3 seconds
- **Memory Usage**: < 50MB idle

---

## Configuration & Settings

### Configuration File
**Path**: `%APPDATA%\EyeRest\config.json`

```json
{
  "EyeRest": {
    "IntervalMinutes": 20,
    "DurationSeconds": 20,
    "WarningEnabled": true,
    "WarningSeconds": 30
  },
  "Break": {
    "IntervalMinutes": 55,
    "DurationMinutes": 5,
    "WarningEnabled": true,
    "WarningSeconds": 30,
    "RequireConfirmationAfterBreak": true,
    "ResetTimersOnBreakConfirmation": true
  },
  "UserPresence": {
    "Enabled": true,
    "IdleThresholdMinutes": 5,
    "EnableSmartSessionReset": true,
    "ExtendedAwayThresholdMinutes": 30
  },
  "Analytics": {
    "Enabled": true,
    "DataRetentionDays": 90
  }
}
```

### Application Data
- **Logs**: `%APPDATA%\EyeRest\logs\eyerest.log`
- **Analytics DB**: `%APPDATA%\EyeRest\analytics.db`

---

## Build & Development

### Prerequisites
- Windows 10/11 (SDK 10.0.19041.0+)
- .NET 8.0 SDK
- Visual Studio 2022 or VS Code

### Build Commands
```bash
# Restore dependencies
dotnet restore

# Build debug
dotnet build

# Build release
dotnet build --configuration Release

# Run application
dotnet run

# Run all tests
dotnet test

# Run specific test category
dotnet test --filter Category=Unit
dotnet test --filter Category=Integration
dotnet test --filter Category=E2E
```

### UI Test Execution
```bash
# Via batch file
run-ui-tests.bat

# Or directly
dotnet run -- RunUITests --build
```

---

## Dependencies

### Internal Dependencies
```
EyeRest.Tests
    └── EyeRest (ProjectReference)
```

### Windows API Dependencies
- **user32.dll**: GetLastInputInfo, SetForegroundWindow
- **kernel32.dll**: GetTickCount, system info
- **wtsapi32.dll**: Session notifications

### Service Dependencies (DI Graph)
```
ApplicationOrchestrator
├── ITimerService
├── INotificationService
├── IAudioService
├── ISystemTrayService
├── IPerformanceMonitor
├── IConfigurationService
├── IUserPresenceService
├── IAnalyticsService
└── IPauseReminderService

TimerService
├── IConfigurationService
├── IAnalyticsService
├── ITimerFactory
└── IPauseReminderService
```

---

## Version History

| Date | Changes |
|------|---------|
| 2026-01-29 | Comprehensive documentation update |
| 2025-01 | Initial architecture, dual-timer system |
| 2025-02 | Added user presence detection |
| 2025-03 | Added analytics service, SQLite storage |
| 2025-04 | Timer service refactoring (partial classes) |
| 2025-05 | Smart session reset, extended away detection |
| 2025-06 | Meeting detection (disabled pending improvements) |

---

## Performance Characteristics

### Memory Usage
| State | Target | Monitoring |
|-------|--------|------------|
| Idle | < 50MB | PerformanceMonitor service |
| Active (popup visible) | < 100MB | Automatic tracking |
| Peak (analytics export) | < 150MB | Logged warnings |

**Optimization Strategies**:
- Lazy loading of analytics data
- Resource reuse for popup windows
- Weak event handlers (WeakEventManager)
- Proper disposal patterns

### Startup Performance
| Phase | Target | Actual |
|-------|--------|--------|
| DI Container Setup | < 500ms | ~300ms |
| Service Initialization | < 1.5s | ~1s |
| UI Ready | < 3s total | ~2.5s |

**Strategy**:
- Phased service initialization
- Lazy loading for non-critical services
- Background analytics database init
- Validated by StartupPerformanceTests

### Multi-Monitor Support
- **Full-Screen Popups**: Span all connected monitors during breaks
- **Positioning**: BasePopupWindow handles multi-monitor scenarios
- **DPI Awareness**: Per-monitor DPI handling in app.manifest
- **Testing**: UI tests validate behavior across monitor configurations

---

## Thread Safety Model

### UI Thread Operations
All WPF operations must run on the UI thread:

```csharp
// Timer operations - DispatcherTimer automatically runs on UI thread
_eyeRestTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(1) };

// Background to UI thread marshalling
Application.Current.Dispatcher.BeginInvoke(() => {
    // UI update code here
});
```

**Key Rules**:
- **DispatcherTimer**: All timer operations automatically on UI thread
- **UI Updates**: Use `Dispatcher.BeginInvoke` from background threads
- **WPF Binding**: Automatic UI thread marshalling via INotifyPropertyChanged
- **Popup Windows**: Must be created and shown on UI thread

### Background Operations
| Operation | Thread | Synchronization |
|-----------|--------|-----------------|
| Performance monitoring | Background | async/await |
| Analytics DB queries | Background | async/await |
| File I/O (config) | Background | async/await |
| Timer callbacks | UI thread | None needed |

### Resource Cleanup
- **IDisposable**: All services implement proper disposal
- **Event Unsubscription**: ApplicationOrchestrator unsubscribes all events
- **Timer Disposal**: Timers stopped and disposed on shutdown
- **SQLite Connection**: Properly closed in AnalyticsService.Dispose()

---

## Build Scripts and Utilities

| File | Purpose |
|------|---------|
| `run-ui-tests.bat` | UI test execution batch script |
| `test-audio.ps1` | Audio service testing |
| `test-startup.ps1` | Startup performance testing |
| `test-timer-behavior.ps1` | Timer behavior validation |

---

## Future Enhancements

### Planned Improvements
- [ ] Meeting detection reliability improvements
- [ ] Cross-platform support investigation
- [ ] Cloud sync for settings/analytics
- [ ] Custom notification sounds
- [ ] Accessibility improvements

### Technical Debt
- Meeting detection services disabled (needs improvement)
- Some legacy service files (TrayService.cs)
- Test coverage gaps in UI automation

---

**Document Version:** 2.0
**Generated:** 2026-01-29
**Maintainer:** Development Team
