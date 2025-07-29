# Eye-rest Application

A Windows desktop application built with .NET 8 and WPF that provides automated eye rest and break reminders to promote healthy computer usage habits.

## Features

### Core Functionality
- **Eye Rest Reminders**: Automated 20-second eye rest reminders every 20 minutes (configurable)
- **Break Reminders**: 5-minute break reminders every 55 minutes (configurable) with pre-break warnings
- **Audio Notifications**: Optional sound alerts for reminder start/end events
- **System Tray Integration**: Runs unobtrusively in system tray with context menu
- **Multi-Monitor Support**: Full-screen popups work across multiple monitors
- **Stretching Resources**: Quick access to exercise websites during breaks

### User Experience
- **Intuitive Settings UI**: Easy-to-use configuration interface
- **Visual Feedback**: Friendly cartoon character graphics and progress animations
- **Break Controls**: Delay (1 or 5 minutes) or skip break options
- **Startup Integration**: Optional Windows startup registration
- **High DPI Support**: Scales properly on high-resolution displays

### Performance & Reliability
- **Fast Startup**: Application starts in under 3 seconds
- **Low Memory Usage**: Stays under 50MB memory consumption
- **Error Recovery**: Automatic recovery from timer failures
- **Comprehensive Logging**: Detailed logging with automatic cleanup
- **Performance Monitoring**: Built-in memory and CPU usage tracking

## Project Structure

```
EyeRest/
├── Models/                 # Data models and configuration DTOs
├── ViewModels/            # MVVM presentation logic with data binding
├── Views/                 # WPF XAML windows and user controls
├── Services/              # Business logic and system integration services
├── Infrastructure/        # Utilities, weak event management, performance monitoring
├── Resources/            # Themes, icons, and visual assets
├── EyeRest.Tests/        # Comprehensive test suite
├── App.xaml              # Application entry point with DI configuration
└── EyeRest.csproj        # Project file with .NET 8 and package references
```

## Architecture

### Design Patterns
- **MVVM Architecture**: Clean separation between UI, presentation logic, and business logic
- **Dependency Injection**: Microsoft.Extensions.DependencyInjection for service management
- **Service-Oriented Design**: Modular services for timers, notifications, audio, and configuration
- **Observer Pattern**: Event-driven communication between services
- **Repository Pattern**: Configuration persistence with JSON serialization

### Key Services
- **TimerService**: Dual-timer system for eye rest and break reminders
- **NotificationService**: Full-screen popup management with multi-monitor support
- **ConfigurationService**: Settings persistence with validation and change notifications
- **AudioService**: System sound integration with custom audio support
- **SystemTrayService**: Windows system tray integration with context menus
- **ApplicationOrchestrator**: Coordinates all services and handles application lifecycle

### Performance Optimizations
- **Lazy Loading**: Services initialized on-demand for fast startup
- **Resource Reuse**: Popup windows cached and reused to minimize memory allocation
- **Weak Event Handlers**: Prevents memory leaks in long-running timer scenarios
- **Background Processing**: Heavy operations run on background threads
- **Automatic Garbage Collection**: Triggered when memory usage approaches limits

## Requirements

- .NET 8.0 Desktop Runtime
- Windows 10 (version 1903+) or Windows 11
- 100MB disk space
- Audio device (optional, for sound notifications)

## Installation & Usage

### Build and Run
```bash
# Clone the repository
git clone <repository-url>
cd eye-rest

# Build the application
dotnet build

# Run the application
dotnet run
```

### Testing
```bash
# Run all tests
dotnet test

# Run specific test categories
dotnet test --filter Category=Unit
dotnet test --filter Category=Integration
dotnet test --filter Category=Performance
```

### Configuration
The application stores configuration in `%APPDATA%/EyeRest/config.json`. Settings include:
- Eye rest interval and duration
- Break interval and duration
- Audio preferences
- Application startup behavior

### System Tray Usage
- **Double-click**: Open settings window
- **Right-click**: Access context menu (Open App, Exit)
- **Icon Colors**: Green (active), Yellow (paused), Blue (break), Red (error)

## Development

### Adding New Features
1. Create service interface in `Services/`
2. Implement service with proper error handling and logging
3. Register service in `App.xaml.cs` DI container
4. Add unit tests in `EyeRest.Tests/`
5. Update `ApplicationOrchestrator` if needed for service coordination

### Testing Strategy
- **Unit Tests**: Individual service and ViewModel testing with mocks
- **Integration Tests**: Service interaction and workflow testing
- **Performance Tests**: Startup time and memory usage validation
- **End-to-End Tests**: Complete application workflow verification

### Code Quality
- Comprehensive error handling with graceful degradation
- Extensive logging for debugging and monitoring
- Memory leak prevention through proper disposal patterns
- Performance monitoring with automatic optimization triggers

## License

[Add your license information here]

## Contributing

[Add contribution guidelines here]