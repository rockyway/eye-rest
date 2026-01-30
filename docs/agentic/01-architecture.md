# Phase 1: Architecture Guide

> Load this document when understanding the codebase, service structure, or design patterns.

---

## Project Structure

```
EyeRest/
├── Services/              # Business logic services with interfaces
│   ├── Abstractions/     # Service interfaces (ITimerService, etc.)
│   ├── Implementation/   # Service implementations
│   └── Timer/           # Timer-specific service components
├── ViewModels/           # MVVM presentation logic
│   ├── MainWindowViewModel.cs
│   ├── ViewModelBase.cs
│   └── RelayCommand.cs
├── Views/                # WPF XAML windows
│   ├── MainWindow.xaml
│   ├── BasePopupWindow.xaml
│   ├── EyeRestPopup.xaml
│   ├── BreakPopup.xaml
│   └── *WarningPopup.xaml
├── Models/               # Configuration DTOs
│   └── AppConfiguration.cs
├── Infrastructure/       # Utilities
│   └── WeakEventManager.cs
├── Resources/           # Themes and visual assets
├── Converters/          # WPF value converters
├── EyeRest.Tests/       # Comprehensive test suite
├── docs/                # Documentation
└── App.xaml.cs          # DI container configuration
```

---

## Core Design Patterns

### MVVM Architecture
- **Models**: Configuration DTOs in `Models/`
- **Views**: XAML files in `Views/`
- **ViewModels**: Presentation logic in `ViewModels/`
- **Data Binding**: Two-way binding with INotifyPropertyChanged

### Dependency Injection
- **Container**: Microsoft.Extensions.DependencyInjection
- **Registration**: All services registered in `App.xaml.cs` ConfigureServices method
- **Lifetime**: Singleton for stateful services, Transient for ViewModels

### Service-Oriented Design
- **Interface Segregation**: Each service has focused interface
- **Single Responsibility**: Services have single, well-defined purpose
- **Loose Coupling**: Services communicate through events and interfaces

### Observer Pattern
- **Event-Driven**: TimerService emits events, Orchestrator subscribes
- **WeakEventManager**: Prevents memory leaks in long-lived subscriptions

### Orchestrator Pattern
- **ApplicationOrchestrator**: Central coordinator for service interactions
- Handles application lifecycle and event routing

---

## Key Services

### ApplicationOrchestrator
Central coordinator that manages service interactions and application lifecycle.

```
ApplicationOrchestrator (Central Coordinator)
├── TimerService (Core break timing)
├── UserPresenceService (Presence detection)
├── MeetingDetectionService (Meeting awareness)
├── AnalyticsService (Data collection)
├── PauseReminderService (Pause management)
├── NotificationService (UI notifications)
├── SystemTrayService (System integration)
└── ConfigurationService (Settings management)
```

### TimerService
Dual-timer system for eye rest (20min/20sec) and break (55min/5min) reminders.
- Uses DispatcherTimer (requires UI thread)
- Independent lifecycles for each timer type
- Events: EyeRestWarning, EyeRestDue, BreakWarning, BreakDue

### NotificationService
Full-screen popup management with multi-monitor support.
- Creates and positions popups across all monitors
- Manages popup lifecycle and user interactions
- Inherits from BasePopupWindow

### ConfigurationService
JSON-based settings persistence.
- Storage: `%APPDATA%\EyeRest\config.json`
- Change notifications to subscribers
- Schema validation and default restoration

### SystemTrayService
Windows system tray integration.
- Context menu (Open App, Exit)
- Icon colors: Green (active), Yellow (paused), Blue (break), Red (error)
- Double-click opens settings

---

## Event Flow

```
Timer Events:
TimerService.EyeRestDue/BreakDue
    ↓
ApplicationOrchestrator.OnTimerEvent
    ↓
NotificationService.ShowPopup + AudioService.PlaySound

Presence Events:
UserPresenceService.UserPresenceChanged
    ↓
ApplicationOrchestrator.OnUserPresenceChanged
    ↓
TimerService.SmartPauseAsync/SmartResumeAsync
    ↓
SystemTrayService.UpdateTrayIcon
```

---

## Configuration Schema

```json
{
  "EyeRest": { "IntervalMinutes": 20, "DurationSeconds": 20 },
  "Break": { "IntervalMinutes": 55, "DurationMinutes": 5 },
  "Audio": { "Enabled": true, "Volume": 50 },
  "Application": { "StartWithWindows": false, "MinimizeToTray": true },
  "UserPresence": { "Enabled": true, "IdleThresholdMinutes": 5 },
  "MeetingDetection": { "Enabled": true, "AutoPauseTimers": true },
  "Analytics": { "Enabled": true, "DataRetentionDays": 90 },
  "TimerControls": { "ShowPauseReminders": true, "MaxPauseHours": 8 }
}
```

---

## Thread Safety Model

### UI Thread Requirements
- **DispatcherTimer**: All timer operations MUST be on UI thread
- **WPF Binding**: Automatic UI thread marshalling for bound properties
- **Background to UI**: Use `Dispatcher.BeginInvoke` or `Dispatcher.InvokeAsync`

### Background Operations
- Heavy computations (performance monitoring, file I/O)
- Proper async/await patterns
- Resource cleanup via Dispose pattern

### Memory Leak Prevention
- **WeakEventManager**: For long-lived event subscriptions
- **IDisposable**: All services implement proper disposal
- **Event Cleanup**: Unsubscribe in Dispose methods

---

## Application Data Locations

| Data Type | Location |
|-----------|----------|
| Configuration | `%APPDATA%\EyeRest\config.json` |
| Logs | `%APPDATA%\EyeRest\logs\eyerest.log` |
| Analytics DB | `%APPDATA%\EyeRest\analytics.db` |

---

## Performance Characteristics

### Targets
- **Startup**: <3 seconds from launch to ready
- **Memory**: <50MB idle, <100MB active
- **CPU**: <5% average, <15% during initialization

### Optimization Strategies
- Lazy loading of services
- Resource reuse (popup windows cached)
- Weak event handlers
- Background processing for heavy operations
- Automatic garbage collection triggers
