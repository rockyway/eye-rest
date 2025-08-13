# Eye-rest API Reference

## Overview
This document provides a comprehensive reference for all interfaces, services, and APIs in the Eye-rest application.

## Core Services

### ITimerService
**Location**: `Services/ITimerService.cs`  
**Purpose**: Manages dual-timer system for eye rest and break reminders

#### Properties
```csharp
bool IsRunning { get; }                    // Current timer status
TimeSpan TimeUntilNextEyeRest { get; }    // Time remaining until next eye rest
TimeSpan TimeUntilNextBreak { get; }      // Time remaining until next break  
string NextEventDescription { get; }      // Human-readable next event description
```

#### Events
```csharp
event EventHandler<TimerEventArgs> EyeRestWarning;  // Pre-eye rest notification
event EventHandler<TimerEventArgs> EyeRestDue;      // Eye rest timer triggered
event EventHandler<TimerEventArgs> BreakWarning;    // Pre-break notification
event EventHandler<TimerEventArgs> BreakDue;        // Break timer triggered
```

#### Methods
```csharp
Task StartAsync();                     // Start both timers
Task StopAsync();                      // Stop both timers
Task ResetEyeRestTimer();             // Reset eye rest countdown
Task ResetBreakTimer();               // Reset break countdown
Task DelayBreak(TimeSpan delay);      // Delay break by specified time
```

#### Supporting Types
```csharp
public class TimerEventArgs : EventArgs
{
    DateTime TriggeredAt { get; set; }    // When event was triggered
    TimeSpan NextInterval { get; set; }   // Time until next occurrence
    TimerType Type { get; set; }          // Type of timer event
}

public enum TimerType
{
    EyeRestWarning,    // Pre-eye rest warning
    EyeRest,          // Eye rest notification
    BreakWarning,     // Pre-break warning
    Break             // Break notification
}
```

### IConfigurationService
**Location**: `Services/IConfigurationService.cs`  
**Purpose**: Manages application configuration persistence and validation

#### Methods
```csharp
Task<AppConfiguration> LoadConfigurationAsync();           // Load from disk
Task SaveConfigurationAsync(AppConfiguration config);      // Save to disk
Task<AppConfiguration> GetDefaultConfiguration();          // Get defaults
```

#### Events
```csharp
event EventHandler<ConfigurationChangedEventArgs> ConfigurationChanged;
```

#### Supporting Types
```csharp
public class ConfigurationChangedEventArgs : EventArgs
{
    AppConfiguration NewConfiguration { get; set; }  // Updated configuration
    AppConfiguration OldConfiguration { get; set; }  // Previous configuration
}
```

### INotificationService
**Location**: `Services/INotificationService.cs`  
**Purpose**: Manages full-screen popup notifications and user interactions

#### Methods
```csharp
Task ShowEyeRestWarningAsync(TimeSpan timeUntilBreak);                    // Pre-eye rest popup
Task ShowEyeRestReminderAsync(TimeSpan duration);                        // Eye rest popup
Task ShowBreakWarningAsync(TimeSpan timeUntilBreak);                     // Pre-break popup
Task<BreakAction> ShowBreakReminderAsync(TimeSpan duration, IProgress<double> progress);  // Break popup with user actions
Task HideAllNotifications();                                             // Close all popups
```

#### Supporting Types
```csharp
public enum BreakAction
{
    Completed,        // Break completed normally
    DelayOneMinute,   // User requested 1-minute delay
    DelayFiveMinutes, // User requested 5-minute delay
    Skipped          // User skipped the break
}
```

### IAudioService
**Location**: `Services/IAudioService.cs`  
**Purpose**: Manages audio notifications for timer events

#### Properties
```csharp
bool IsAudioEnabled { get; }  // Current audio enablement status
```

#### Methods
```csharp
Task PlayEyeRestStartSound();   // Play sound when eye rest begins
Task PlayEyeRestEndSound();     // Play sound when eye rest ends
Task PlayBreakWarningSound();   // Play sound for break warning
```

### ISystemTrayService
**Location**: `Services/ISystemTrayService.cs`  
**Purpose**: Manages Windows system tray integration and icon states

#### Methods
```csharp
void Initialize();                              // Initialize tray service
void ShowTrayIcon();                           // Show icon in system tray
void HideTrayIcon();                           // Hide icon from system tray
void UpdateTrayIcon(TrayIconState state);      // Update icon based on app state
void ShowBalloonTip(string title, string text); // Show balloon notification
```

#### Events
```csharp
event EventHandler RestoreRequested;   // User requested window restore
event EventHandler ExitRequested;      // User requested application exit
```

#### Supporting Types
```csharp
public enum TrayIconState
{
    Active,   // Timers running normally (green)
    Paused,   // Timers paused (yellow)
    Break,    // Currently in break (blue)
    Error     // Error state (red)
}
```

### IPerformanceMonitor
**Location**: `Services/IPerformanceMonitor.cs`  
**Purpose**: Monitors application performance metrics and resource usage

#### Methods
```csharp
long GetMemoryUsageMB();           // Current memory usage in MB
double GetCpuUsagePercent();       // Current CPU usage percentage
TimeSpan GetUptime();              // Application uptime
void LogPerformanceMetrics();      // Log current metrics
```

## Configuration Models

### AppConfiguration
**Location**: `Models/AppConfiguration.cs`  
**Purpose**: Root configuration object containing all application settings

#### Properties
```csharp
EyeRestSettings EyeRest { get; set; }          // Eye rest configuration
BreakSettings Break { get; set; }              // Break configuration
AudioSettings Audio { get; set; }              // Audio configuration
ApplicationSettings Application { get; set; }   // Application configuration
```

### EyeRestSettings
**Purpose**: Configuration for eye rest reminders

#### Properties
```csharp
int IntervalMinutes { get; set; } = 20;        // Minutes between eye rests (default: 20)
int DurationSeconds { get; set; } = 20;        // Eye rest duration in seconds (default: 20)
bool StartSoundEnabled { get; set; } = true;   // Play sound when eye rest starts
bool EndSoundEnabled { get; set; } = true;     // Play sound when eye rest ends
bool WarningEnabled { get; set; } = true;      // Show warning before eye rest
int WarningSeconds { get; set; } = 30;         // Warning time in seconds (default: 30)
```

### BreakSettings
**Purpose**: Configuration for break reminders

#### Properties
```csharp
int IntervalMinutes { get; set; } = 10;        // Minutes between breaks (default: 10)
int DurationMinutes { get; set; } = 2;         // Break duration in minutes (default: 2)  
bool WarningEnabled { get; set; } = true;      // Show warning before break
int WarningSeconds { get; set; } = 30;         // Warning time in seconds (default: 30)
```

### AudioSettings
**Purpose**: Configuration for audio notifications

#### Properties
```csharp
bool Enabled { get; set; } = true;             // Audio notifications enabled
string? CustomSoundPath { get; set; }          // Path to custom sound file
int Volume { get; set; } = 50;                 // Volume level (0-100)
```

### ApplicationSettings
**Purpose**: Configuration for general application behavior

#### Properties
```csharp
bool StartWithWindows { get; set; } = false;   // Start application with Windows
bool MinimizeToTray { get; set; } = true;      // Minimize to system tray
bool ShowInTaskbar { get; set; } = false;      // Show application in taskbar
```

## Architecture Patterns

### Service Registration
Services are registered in `App.xaml.cs` using Microsoft.Extensions.DependencyInjection:

```csharp
// Core services
services.AddSingleton<IConfigurationService, ConfigurationService>();
services.AddSingleton<ITimerService, TimerService>();
services.AddSingleton<INotificationService, NotificationService>();
services.AddSingleton<IAudioService, AudioService>();
services.AddSingleton<ISystemTrayService, SystemTrayService>();
services.AddSingleton<IPerformanceMonitor, PerformanceMonitor>();

// Orchestration
services.AddSingleton<IApplicationOrchestrator, ApplicationOrchestrator>();
```

### Event Flow
1. **TimerService** triggers timer events (EyeRestDue, BreakDue, etc.)
2. **ApplicationOrchestrator** coordinates event handling across services
3. **NotificationService** displays appropriate popups
4. **AudioService** plays notification sounds
5. **SystemTrayService** updates icon state

### Thread Safety
- **UI Thread Operations**: All DispatcherTimer operations must run on UI thread
- **Background Operations**: Heavy computations run on background threads
- **Thread Synchronization**: Use Dispatcher.BeginInvoke for UI updates from background threads

## Performance Specifications

### Requirements
- **Startup Time**: < 3 seconds from launch to ready state
- **Memory Usage**: < 50MB when idle, < 100MB during active use
- **CPU Usage**: < 1% when idle, < 5% during notifications
- **Response Time**: < 100ms for user interactions

### Monitoring
- **PerformanceMonitor** service tracks resource usage
- Automatic garbage collection triggered when approaching memory limits
- Performance metrics logged for debugging and optimization

## Error Handling

### Service-Level Error Handling
- All async methods use try-catch with appropriate logging
- Graceful degradation when services fail (e.g., audio continues without sound)
- Automatic recovery attempts for transient failures

### Application-Level Error Handling
- Unhandled exceptions caught at App level
- User-friendly error messages for recoverable errors
- Comprehensive logging for debugging

## Testing API

### Test Categories
- **Unit Tests**: Individual service testing with mocks
- **Integration Tests**: Service interaction testing
- **Performance Tests**: Resource usage validation
- **E2E Tests**: Complete user workflow testing

### Test Execution
```bash
# Run all tests
dotnet test

# Run by category
dotnet test --filter Category=Unit
dotnet test --filter Category=Integration
dotnet test --filter Category=Performance
dotnet test --filter Category=E2E
```

### UI Test Framework
- **TestStack.White**: WPF UI automation
- **Custom UIAutomationFramework**: Application-specific test utilities
- **Test Runners**: Specialized runners for UI validation