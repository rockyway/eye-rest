# Design Document

## Overview

Eye-rest is a Windows desktop application built using .NET 8 and WPF with MVVM architecture. The application provides automated eye rest and break reminders to promote healthy computer usage habits. The design emphasizes performance (startup <3s, memory <50MB), reliability, and user experience through a clean separation of concerns and testable architecture.

## Architecture

### High-Level Architecture

The application follows the MVVM (Model-View-ViewModel) pattern with a service-oriented architecture:

```
┌─────────────────┐    ┌─────────────────┐    ┌─────────────────┐
│      Views      │◄──►│   ViewModels    │◄──►│    Services     │
│   (WPF XAML)    │    │  (Presentation) │    │   (Business)    │
└─────────────────┘    └─────────────────┘    └─────────────────┘
                                │                       │
                                ▼                       ▼
                       ┌─────────────────┐    ┌─────────────────┐
                       │     Models      │    │  Infrastructure │
                       │   (Data DTOs)   │    │ (Config, Timers)│
                       └─────────────────┘    └─────────────────┘
```

### Core Architectural Principles

- **Dependency Injection**: Using Microsoft.Extensions.DependencyInjection for service registration and lifetime management
- **Async/Await**: All timer operations and file I/O use .NET 8 async patterns
- **Weak Event Handlers**: Preventing memory leaks in long-running timer scenarios
- **Lazy Loading**: Services initialized on-demand to optimize startup time
- **Resource Preloading**: Critical UI resources loaded during application startup

## Components and Interfaces

### 1. Timer Management System

#### ITimerService Interface
```csharp
public interface ITimerService
{
    event EventHandler<TimerEventArgs> EyeRestWarning;
    event EventHandler<TimerEventArgs> EyeRestDue;
    event EventHandler<TimerEventArgs> BreakWarning;
    event EventHandler<TimerEventArgs> BreakDue;
    
    bool IsRunning { get; }
    Task StartAsync();
    Task StopAsync();
    Task ResetEyeRestTimer();
    Task ResetBreakTimer();
    Task DelayBreak(TimeSpan delay);
}
```

#### Implementation Strategy
- **Dual Timer System**: Separate `DispatcherTimer` instances for eye rest and break reminders
- **Background Processing**: Timer callbacks execute on background threads with UI marshaling
- **State Persistence**: Timer states saved to prevent loss during application restart
- **Performance Optimization**: Timers use minimal CPU through efficient callback patterns

### 2. Notification Management System

#### INotificationService Interface
```csharp
public interface INotificationService
{
    Task ShowEyeRestWarningAsync(TimeSpan timeUntilBreak);
    Task ShowEyeRestReminderAsync(TimeSpan duration);
    Task ShowBreakWarningAsync(TimeSpan timeUntilBreak);
    Task<BreakAction> ShowBreakReminderAsync(TimeSpan duration, IProgress<double> progress);
    Task HideAllNotifications();
}
```

#### Popup Window Architecture
- **Full-Screen Overlays**: Custom WPF windows with `WindowStyle.None` and `Topmost=true`
- **Multi-Monitor Support**: Popup positioning calculated for primary/all monitors using `System.Windows.Forms.Screen`
- **Animation System**: Smooth progress bars using WPF Storyboard animations
- **Memory Efficiency**: Popup windows reused rather than recreated

### 3. Configuration Management System

#### IConfigurationService Interface
```csharp
public interface IConfigurationService
{
    Task<AppConfiguration> LoadConfigurationAsync();
    Task SaveConfigurationAsync(AppConfiguration config);
    Task<AppConfiguration> GetDefaultConfiguration();
    event EventHandler<ConfigurationChangedEventArgs> ConfigurationChanged;
}
```

#### Configuration Model
```csharp
public class AppConfiguration
{
    public EyeRestSettings EyeRest { get; set; }
    public BreakSettings Break { get; set; }
    public AudioSettings Audio { get; set; }
    public ApplicationSettings Application { get; set; }
}
```

#### Storage Strategy
- **JSON Serialization**: Using `System.Text.Json` for configuration persistence
- **File Location**: `%APPDATA%/EyeRest/config.json` for user-specific settings
- **Validation**: Configuration validation with fallback to defaults on corruption
- **Change Notification**: Real-time configuration updates using file system watchers

### 4. Audio Management System

#### IAudioService Interface
```csharp
public interface IAudioService
{
    Task PlayEyeRestStartSound();
    Task PlayEyeRestEndSound();
    Task PlayBreakWarningSound();
    bool IsAudioEnabled { get; }
}
```

#### Implementation Details
- **System Sounds**: Default Windows system sounds for notifications
- **Custom Audio**: Support for WAV files in application directory
- **Volume Control**: Respect system volume settings
- **Error Handling**: Graceful fallback when audio devices unavailable

### 5. System Tray Integration

#### ISystemTrayService Interface
```csharp
public interface ISystemTrayService
{
    void Initialize();
    void ShowTrayIcon();
    void HideTrayIcon();
    void UpdateTrayIcon(TrayIconState state);
    event EventHandler RestoreRequested;
    event EventHandler ExitRequested;
}
```

#### Tray Icon States
- **Active**: Normal operation with timer running
- **Paused**: User has paused reminders
- **Break**: Currently in break mode
- **Error**: Configuration or system error

## Data Models

### Configuration Models

```csharp
public class EyeRestSettings
{
    public int IntervalMinutes { get; set; } = 20;
    public int DurationSeconds { get; set; } = 20;
    public bool StartSoundEnabled { get; set; } = true;
    public bool EndSoundEnabled { get; set; } = true;
    public bool WarningEnabled { get; set; } = true;
    public int WarningSeconds { get; set; } = 30;
}

public class BreakSettings
{
    public int IntervalMinutes { get; set; } = 55;
    public int DurationMinutes { get; set; } = 5;
    public bool WarningEnabled { get; set; } = true;
    public int WarningSeconds { get; set; } = 30;
}

public class AudioSettings
{
    public bool Enabled { get; set; } = true;
    public string CustomSoundPath { get; set; }
    public int Volume { get; set; } = 50;
}

public class ApplicationSettings
{
    public bool StartWithWindows { get; set; } = false;
    public bool MinimizeToTray { get; set; } = true;
    public bool ShowInTaskbar { get; set; } = false;
}
```

### Event Models

```csharp
public class TimerEventArgs : EventArgs
{
    public DateTime TriggeredAt { get; set; }
    public TimeSpan NextInterval { get; set; }
    public TimerType Type { get; set; }
}

public enum BreakAction
{
    Completed,
    DelayOneMinute,
    DelayFiveMinutes,
    Skipped
}

public enum TimerType
{
    EyeRest,
    BreakWarning,
    Break
}
```

## Error Handling

### Exception Handling Strategy

1. **Service Level**: All service methods wrapped in try-catch with logging
2. **UI Level**: Global exception handler for unhandled WPF exceptions
3. **Timer Resilience**: Timer failures automatically restart with exponential backoff
4. **Configuration Recovery**: Corrupted configuration files reset to defaults
5. **Popup Failures**: Notification failures logged but don't crash application

### Logging Implementation

```csharp
public interface ILoggingService
{
    void LogInfo(string message);
    void LogWarning(string message);
    void LogError(string message, Exception exception = null);
}
```

- **File Logging**: Daily log files in `%APPDATA%/EyeRest/logs/`
- **Log Rotation**: Automatic cleanup of logs older than 30 days
- **Performance Impact**: Async logging to prevent UI blocking

## Testing Strategy

### Unit Testing Approach

1. **Service Testing**: Mock dependencies using Moq framework
2. **ViewModel Testing**: Test property changes and command execution
3. **Timer Testing**: Use `Microsoft.Extensions.Time.Testing` for time manipulation
4. **Configuration Testing**: Test serialization/deserialization and validation
5. **Audio Testing**: Mock audio services for reliable testing

### Integration Testing

1. **End-to-End Flows**: Test complete reminder cycles
2. **Multi-Monitor Testing**: Validate popup positioning across monitor configurations
3. **Performance Testing**: Verify startup time and memory usage requirements
4. **Configuration Persistence**: Test settings save/load cycles

### Test Structure

```
Tests/
├── Unit/
│   ├── Services/
│   ├── ViewModels/
│   └── Models/
├── Integration/
│   ├── TimerIntegration/
│   ├── NotificationIntegration/
│   └── ConfigurationIntegration/
└── Performance/
    ├── StartupTests/
    └── MemoryTests/
```

## Performance Optimizations

### Startup Performance (<3s requirement)

1. **Lazy Service Initialization**: Services created on first use
2. **Background Configuration Loading**: Settings loaded asynchronously
3. **Minimal UI Initialization**: Main window created but not shown initially
4. **Resource Preloading**: Critical XAML resources loaded during splash
5. **Assembly Optimization**: Use ReadyToRun images for faster JIT

### Memory Management (<50MB requirement)

1. **Weak Event Patterns**: Prevent memory leaks in long-running timers
2. **Popup Window Reuse**: Single popup instances reused rather than recreated
3. **Resource Disposal**: Proper disposal of timers, file handles, and audio resources
4. **XAML Optimization**: Use virtualization for any list controls
5. **Image Optimization**: Cartoon character graphics optimized for size

### Runtime Performance

1. **Timer Efficiency**: Minimal CPU usage through optimized timer callbacks
2. **UI Thread Management**: All heavy operations on background threads
3. **Animation Performance**: Hardware-accelerated WPF animations
4. **File I/O Optimization**: Async file operations with minimal blocking

## Security Considerations

1. **Configuration Security**: Settings file permissions restricted to current user
2. **Process Isolation**: Application runs in user context, no elevation required
3. **Network Security**: No network communication, fully offline application
4. **File System Access**: Limited to user's AppData directory
5. **Audio Security**: Only system sounds and user-selected files

## Deployment Architecture

### Application Structure
```
EyeRest/
├── EyeRest.exe                 # Main executable
├── EyeRest.dll                 # Core application logic
├── Resources/
│   ├── character.png           # Cartoon character graphic
│   ├── icon.ico               # Application icon
│   └── sounds/                # Default audio files
├── Themes/
│   └── Default.xaml           # Default theme resources
└── Dependencies/              # .NET runtime dependencies
```

### Installation Requirements
- .NET 8.0 Desktop Runtime
- Windows 10 version 1903+ or Windows 11
- 100MB disk space
- Audio device (optional, for sound notifications)

This design provides a robust, performant, and maintainable foundation for the Eye-rest application while meeting all specified requirements and performance constraints.