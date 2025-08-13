# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## Project Overview

Eye-rest is a Windows desktop application built with .NET 8 and WPF that provides automated eye rest and break reminders. The application uses MVVM architecture with dependency injection and runs as a system tray application.

**Key Technologies:**
- .NET 8.0 with WPF (Windows Presentation Foundation)
- Microsoft.Extensions.DependencyInjection for IoC container
- Microsoft.Extensions.Logging for application logging
- System.Text.Json for configuration persistence
- SQLite for analytics and reporting data storage
- Windows APIs (user32, kernel32, WTS) for system integration
- xUnit, Moq, and TestStack.White for testing

## Advanced Features (Phase 2 Development)

The application is being enhanced with four major advanced features:

### 1. Smart User Presence Detection
- **Automatic pause/resume**: Detects when user is away (monitor off, locked screen, idle timeout)
- **Session monitoring**: Monitors Windows session state changes and power events
- **Activity detection**: Resumes timers on mouse movement or keyboard activity
- **Progress preservation**: Maintains timer progress during absence periods

### 2. Reporting & Analytics Module
- **Behavior tracking**: Records skip counts, delay usage, and popup interactions
- **Health metrics**: Monitors compliance rates and session adherence
- **Historical data**: Trends analysis with data retention policies
- **Export capabilities**: CSV/JSON export with privacy controls

### 3. Meeting Detection & Smart Pausing
- **Process monitoring**: Detects Microsoft Teams, Zoom, and Webex meetings
- **Auto-pause functionality**: Automatically pauses break timers during meetings
- **Visual indicators**: Shows meeting mode status in system tray
- **Manual override**: User can disable meeting detection per application

### 4. Enhanced Timer Controls
- **Manual pause/resume**: System tray context menu for timer control
- **Pause reminders**: Hourly notifications when timers are paused
- **Safety mechanisms**: Prevents accidental indefinite pausing

## Architecture Overview

### Core Design Patterns
- **MVVM Architecture**: ViewModels handle presentation logic, Services contain business logic
- **Dependency Injection**: All services registered in App.xaml.cs using Microsoft.Extensions.DI
- **Service-Oriented Design**: Modular services for timers, notifications, audio, and configuration
- **Observer Pattern**: Event-driven communication between TimerService and other components
- **Orchestrator Pattern**: ApplicationOrchestrator coordinates service interactions

### Key Services Architecture
- **ApplicationOrchestrator**: Central coordinator that wires up all service events and manages application lifecycle
- **TimerService**: Dual-timer system (eye rest every 20 minutes, breaks every 55 minutes) using DispatcherTimer
- **NotificationService**: Full-screen popup management with multi-monitor support
- **ConfigurationService**: JSON-based settings persistence with change notifications
- **SystemTrayService**: Windows system tray integration with context menus and icon states
- **AudioService**: System sound integration for notification events

### Advanced Services (Phase 2+)
- **UserPresenceService**: Windows API integration for session monitoring and idle detection
- **AnalyticsService**: SQLite-based behavior tracking and health metrics collection
- **MeetingDetectionService**: Process monitoring for Teams/Zoom/Webex meeting detection
- **ReportingService**: Data aggregation and export functionality with privacy controls

### Project Structure
```
EyeRest/
├── Services/              # Business logic services with interfaces
├── ViewModels/           # MVVM presentation logic (MainWindowViewModel, RelayCommand, ViewModelBase)  
├── Views/                # WPF XAML windows (MainWindow, popup windows)
├── Models/               # Configuration DTOs (AppConfiguration)
├── Infrastructure/       # Utilities (WeakEventManager for memory leak prevention)
├── Resources/           # Themes and visual assets
├── EyeRest.Tests/       # Comprehensive test suite
└── App.xaml.cs          # DI container configuration and application entry point
```

## Development Commands

### Build and Run
```bash
# Build the application
dotnet build

# Run the application  
dotnet run

# Build in release mode
dotnet build --configuration Release
```

### Testing
```bash
# Run all tests
dotnet test

# Run specific test categories
dotnet test --filter Category=Unit
dotnet test --filter Category=Integration  
dotnet test --filter Category=Performance
dotnet test --filter Category=E2E

# Run UI tests (requires special test runner)
dotnet run -- RunUITests --build
# OR use the batch file
run-ui-tests.bat
```

### Database Management (Phase 4 - Analytics)
```bash
# Initialize analytics database
dotnet run -- InitializeDatabase

# Migrate database schema
dotnet run -- MigrateDatabase

# Export analytics data
dotnet run -- ExportData --format csv --timerange 30days

# Cleanup old analytics data (privacy compliance)
dotnet run -- CleanupData --older-than 90days
```

### Test Architecture
The project uses a comprehensive testing strategy:
- **Unit Tests**: Services and ViewModels with Moq for mocking
- **Integration Tests**: Service interaction testing 
- **Performance Tests**: Startup time (<3s) and memory usage (<50MB) validation
- **E2E Tests**: Complete application workflow testing with TestStack.White for UI automation
- **UI Validation Tests**: Specialized tests for popup behavior and multi-monitor scenarios

## Key Implementation Details

### Timer System Architecture
- **Dual Timer Design**: EyeRestTimer (20min/20sec) and BreakTimer (55min/5min) with independent lifecycles
- **Thread Safety**: All timer operations must run on UI thread due to DispatcherTimer requirements
- **Event Chain**: TimerService → ApplicationOrchestrator → NotificationService/AudioService
- **State Management**: Timer states tracked through IconService for system tray visual feedback

### Dependency Injection Setup
Services are registered in App.xaml.cs ConfigureServices method:
- All services use interface-based registration for testability
- ApplicationOrchestrator coordinates all service interactions
- Dispatcher is registered as singleton for UI thread operations
- Services have proper lifecycle management (singleton for stateful services)

### Configuration System
- JSON-based configuration stored in %APPDATA%/EyeRest/config.json
- AppConfiguration model with nested sections for different feature areas
- ConfigurationService provides change notifications and validation
- Default values automatically restored if configuration is corrupt

### Performance Requirements
- **Startup Time**: <3 seconds (achieved through lazy initialization and optimized service startup)
- **Memory Usage**: <50MB when idle (monitored by PerformanceMonitor service)
- **System Tray Integration**: Must handle minimize-to-tray instead of closing
- **Multi-Monitor Support**: Full-screen popups work across all connected monitors

### Popup System
- **BasePopupWindow**: Base class for all popup types with common positioning and behavior
- **Multi-Monitor Aware**: Popups appear on all monitors during breaks
- **Break Workflow**: Pre-warning (30sec) → Full break popup (5min) → Success feedback (green screen)
- **User Controls**: Delay (1min/5min) and Skip options with proper event handling

## Common Development Tasks

### Adding New Services
1. Create interface in Services/ folder
2. Implement service with proper error handling and logging
3. Register in App.xaml.cs ConfigureServices method
4. Wire up in ApplicationOrchestrator if coordination needed
5. Add unit tests in EyeRest.Tests/Services/

### Modifying Timer Behavior
- Timer logic is in TimerService.cs with DispatcherTimer implementation
- All timer operations MUST run on UI thread
- Events are wired through ApplicationOrchestrator to maintain loose coupling
- Configuration changes require restart of timer service

### Working with Popups
- Inherit from BasePopupWindow for consistent behavior
- Handle multi-monitor scenarios in ShowPopup methods
- Test popup behavior with E2E tests for proper positioning and user interaction

### Configuration Changes
- Modify AppConfiguration model for new settings
- Update ConfigurationService validation logic
- Add UI elements in MainWindow.xaml with proper data binding
- Test configuration persistence and default value handling

## Document Structure
- **docs/requirements.md**: Complete Product Requirements Document (PRD) 
- **docs/plans/**: Phase-based implementation plans
- **docs/features/**: Detailed task breakdown for each feature
- **EyeRest.Tests/E2E/**: End-to-end test documentation and execution plans

## Important Notes for Development

### Thread Safety
- All UI operations must use Dispatcher.BeginInvoke when called from background threads
- DispatcherTimer requires UI thread initialization - handle in App.xaml.cs startup
- WeakEventManager used to prevent memory leaks in long-running timer scenarios

### Error Handling
- Comprehensive error handling with graceful degradation
- All services implement proper disposal patterns
- Unhandled exceptions caught at application level with user-friendly messages
- Logging service captures all errors for debugging

### Testing Considerations  
- UI tests require special test runner due to WPF/TestStack.White integration
- Performance tests validate startup time and memory constraints
- Mock all external dependencies (file system, system tray) in unit tests
- E2E tests cover complete user workflows including system tray interactions

## Advanced Features Implementation Guide

### Phase 1: Smart User Presence Detection

#### Windows API Integration
- **GetLastInputInfo**: Detect keyboard/mouse idle time for activity monitoring
- **WTSRegisterSessionNotification**: Monitor Windows session state changes (lock/unlock)
- **RegisterPowerSettingNotification**: Monitor power events (monitor off/on, sleep/wake)
- **SetWinEventHook**: Track window focus changes for meeting detection

#### UserPresenceService Implementation
```csharp
public interface IUserPresenceService
{
    event EventHandler<UserPresenceEventArgs> UserPresenceChanged;
    bool IsUserPresent { get; }
    TimeSpan IdleTime { get; }
    UserPresenceState CurrentState { get; }
    Task StartMonitoringAsync();
    Task StopMonitoringAsync();
}

public enum UserPresenceState
{
    Present,           // User actively using computer
    Idle,             // User inactive but session unlocked
    Away,             // Session locked or monitor off
    SystemSleep       // System in sleep/hibernate mode
}
```

#### TimerService Integration
- **Smart Pause Logic**: Pause timers when user becomes away, preserve progress
- **Activity Resume**: Resume timers from previous state when user returns
- **Grace Period**: 30-second delay before considering user "away" to avoid false triggers
- **State Persistence**: Save timer state during absence periods

### Phase 2: Enhanced Timer Controls

#### System Tray Context Menu Extensions
```csharp
// Add to SystemTrayService
contextMenu.Items.Add("Pause Timers", null, OnPauseTimers);
contextMenu.Items.Add("Resume Timers", null, OnResumeTimers);
contextMenu.Items.Add("-"); // Separator
contextMenu.Items.Add("Timer Status", null, OnShowTimerStatus);
```

#### Pause Reminder System
- **Hourly Notifications**: Windows toast notifications when timers paused >1 hour
- **Safety Mechanisms**: Automatic resume after 8 hours to prevent indefinite pausing
- **Visual Indicators**: System tray icon changes to indicate paused state
- **User Confirmation**: Confirmation dialog for manual pause actions

### Phase 3: Meeting Detection & Smart Pausing

#### Process Monitoring Implementation
```csharp
public interface IMeetingDetectionService
{
    event EventHandler<MeetingStateEventArgs> MeetingStateChanged;
    bool IsMeetingActive { get; }
    List<MeetingApplication> DetectedMeetings { get; }
    Task StartMonitoringAsync();
    Task StopMonitoringAsync();
}

public class MeetingApplication
{
    public string ProcessName { get; set; }
    public string WindowTitle { get; set; }
    public DateTime StartTime { get; set; }
    public MeetingType Type { get; set; }
}

public enum MeetingType
{
    Teams,
    Zoom,
    Webex,
    GoogleMeet,
    Unknown
}
```

#### Meeting Detection Logic
- **Process Detection**: Monitor for Teams.exe, Zoom.exe, CiscoWebEx.exe processes
- **Window Title Analysis**: Parse window titles for meeting indicators ("Meeting", "Call", etc.)
- **Audio Detection**: Optional integration with Windows audio sessions API
- **User Override**: Settings to disable detection per application

#### Auto-Pause Integration
- **Timer Coordination**: Automatically pause break timers during detected meetings
- **Visual Feedback**: System tray shows meeting mode status
- **Resume Logic**: Automatically resume timers when meeting ends
- **Manual Override**: Users can manually pause/resume during meetings

### Phase 4: Reporting & Analytics Module

#### Database Schema Design (SQLite)
```sql
-- User sessions and behavior tracking
CREATE TABLE UserSessions (
    Id INTEGER PRIMARY KEY AUTOINCREMENT,
    StartTime DATETIME NOT NULL,
    EndTime DATETIME,
    TotalActiveTime INTEGER, -- milliseconds
    IdleTime INTEGER,
    PresenceChanges INTEGER
);

-- Eye rest and break events
CREATE TABLE RestEvents (
    Id INTEGER PRIMARY KEY AUTOINCREMENT,
    EventType TEXT NOT NULL, -- 'EyeRest', 'Break'
    TriggeredAt DATETIME NOT NULL,
    UserAction TEXT NOT NULL, -- 'Completed', 'Skipped', 'Delayed1Min', 'Delayed5Min'
    Duration INTEGER, -- actual duration in milliseconds
    ConfiguredDuration INTEGER -- configured duration for comparison
);

-- Meeting detection events
CREATE TABLE MeetingEvents (
    Id INTEGER PRIMARY KEY AUTOINCREMENT,
    StartTime DATETIME NOT NULL,
    EndTime DATETIME,
    ApplicationName TEXT NOT NULL,
    MeetingType TEXT,
    TimersPaused BOOLEAN
);

-- User presence state changes
CREATE TABLE PresenceEvents (
    Id INTEGER PRIMARY KEY AUTOINCREMENT,
    Timestamp DATETIME NOT NULL,
    OldState TEXT NOT NULL,
    NewState TEXT NOT NULL,
    IdleDuration INTEGER -- milliseconds before state change
);
```

#### Analytics Service Implementation
```csharp
public interface IAnalyticsService
{
    Task RecordEyeRestEventAsync(RestEventType type, UserAction action, TimeSpan duration);
    Task RecordBreakEventAsync(RestEventType type, UserAction action, TimeSpan duration);
    Task RecordPresenceChangeAsync(UserPresenceState oldState, UserPresenceState newState);
    Task RecordMeetingEventAsync(MeetingApplication meeting, bool timersPaused);
    Task<HealthMetrics> GetHealthMetricsAsync(DateTime startDate, DateTime endDate);
    Task<ComplianceReport> GenerateComplianceReportAsync(int days = 30);
    Task<string> ExportDataAsync(ExportFormat format, DateTime? startDate = null);
}

public class HealthMetrics
{
    public double ComplianceRate { get; set; }          // % of breaks taken vs due
    public int TotalBreaksDue { get; set; }
    public int BreaksCompleted { get; set; }
    public int BreaksSkipped { get; set; }
    public int BreaksDelayed { get; set; }
    public TimeSpan AverageBreakDuration { get; set; }
    public TimeSpan TotalActiveTime { get; set; }
    public int EyeRestsCompleted { get; set; }
    public List<DailyMetric> DailyBreakdown { get; set; }
}
```

#### Reporting Dashboard UI
- **Health Metrics Overview**: Compliance rates, break patterns, eye rest frequency
- **Charts and Visualizations**: Weekly/monthly trend analysis using OxyPlot or LiveCharts
- **Export Functionality**: CSV/JSON export with date range filtering
- **Privacy Controls**: User can clear data, set retention periods
- **Comparative Analysis**: Compare current vs. previous periods

#### Data Privacy and Retention
- **Local Storage Only**: All data stored locally in SQLite database
- **User Control**: Complete data deletion and export capabilities
- **Retention Policies**: Configurable data retention (30/60/90 days, or indefinite)
- **Anonymization**: No personally identifiable information stored
- **GDPR Compliance**: Data portability and right to be forgotten

## Implementation Testing Requirements

### Phase 1 Testing (User Presence)
- **Windows API Mocking**: Mock system calls for unit testing
- **Session State Simulation**: Test lock/unlock scenarios
- **Idle Detection Testing**: Verify accurate idle time calculations
- **Integration Testing**: Timer pause/resume functionality

### Phase 2 Testing (Timer Controls)
- **Context Menu Testing**: UI automation for system tray interactions
- **Notification Testing**: Verify hourly pause reminders
- **State Persistence**: Test pause state across application restarts

### Phase 3 Testing (Meeting Detection)
- **Process Monitoring**: Test detection of meeting applications
- **False Positive Prevention**: Ensure non-meeting processes don't trigger
- **Performance Impact**: Verify minimal CPU/memory usage
- **Integration Testing**: Auto-pause/resume functionality

### Phase 4 Testing (Analytics)
- **Database Testing**: SQLite schema validation and migration testing
- **Data Integrity**: Verify accurate event recording and aggregation
- **Export Testing**: Validate CSV/JSON export formats
- **Performance Testing**: Large dataset handling (1+ years of data)
- **Privacy Testing**: Data deletion and retention policies

## Security and Privacy Considerations

### Data Protection
- **Local Storage**: All analytics data stored locally, never transmitted
- **Encryption**: Optional database encryption for sensitive environments
- **Access Control**: Windows user-level access restrictions
- **Audit Trail**: Log all data access and modification events

### System Integration Security
- **API Permissions**: Minimal required Windows API permissions
- **Process Monitoring**: Read-only process information access
- **Session Monitoring**: Standard Windows session notification APIs
- **Network Isolation**: No network communications for analytics features

### Privacy by Design
- **Data Minimization**: Collect only necessary data for functionality
- **Purpose Limitation**: Data used only for health/productivity insights
- **User Control**: Complete user control over data collection and retention
- **Transparency**: Clear documentation of what data is collected and why