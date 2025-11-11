# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## Project Overview

Eye-rest is a Windows desktop application built with .NET 8 and WPF that provides automated eye rest and break reminders. The application uses MVVM architecture with dependency injection and runs as a system tray application.

**Key Technologies:**
- .NET 8.0 with WPF (Windows Presentation Foundation)
- Microsoft.Extensions.DependencyInjection for IoC container
- Microsoft.Extensions.Logging for application logging
- System.Text.Json for configuration persistence
- Windows APIs (user32, kernel32, WTS) for system integration
- xUnit, Moq, and TestStack.White for testing

## Architecture Overview

### Core Design Patterns
- **MVVM Architecture**: ViewModels handle presentation logic, Services contain business logic
- **Dependency Injection**: All services registered in App.xaml.cs using Microsoft.Extensions.DI
- **Service-Oriented Design**: Modular services for timers, notifications, audio, and configuration
- **Observer Pattern**: Event-driven communication between TimerService and other components
- **Orchestrator Pattern**: ApplicationOrchestrator coordinates service interactions

### Key Services
- **ApplicationOrchestrator**: Central coordinator that manages service interactions and application lifecycle
- **TimerService**: Dual-timer system (eye rest every 20 minutes, breaks every 55 minutes) using DispatcherTimer
- **NotificationService**: Full-screen popup management with multi-monitor support
- **ConfigurationService**: JSON-based settings persistence with change notifications
- **AudioService**: System sound integration for notification events

### Project Structure
```
EyeRest/
├── Services/              # Business logic services with interfaces
│   ├── Abstractions/     # Service interfaces
│   ├── Implementation/   # Service implementations
│   └── Timer/           # Timer-specific service components
├── ViewModels/           # MVVM presentation logic (MainWindowViewModel, RelayCommand, ViewModelBase)
├── Views/                # WPF XAML windows (MainWindow, popup windows)
├── Models/               # Configuration DTOs (AppConfiguration)
├── Infrastructure/       # Utilities (WeakEventManager for memory leak prevention)
├── Resources/           # Themes and visual assets
├── Converters/          # WPF value converters
├── EyeRest.Tests/       # Comprehensive test suite
├── docs/                # Documentation and requirements
└── App.xaml.cs          # DI container configuration and application entry point
```

### Application Data Locations
- **Configuration**: `%APPDATA%\EyeRest\config.json`
- **Application Logs**: `%APPDATA%\EyeRest\logs\eyerest.log`

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

### Testing
The project uses a comprehensive testing strategy:
- **Unit Tests**: Services and ViewModels with Moq for mocking
- **Integration Tests**: Service interaction testing
- **Performance Tests**: Startup time (<3s) and memory usage (<50MB) validation
- **E2E Tests**: Complete application workflow testing with TestStack.White for UI automation

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

### Working with Timers
- Timer logic is in TimerService.cs with DispatcherTimer implementation
- All timer operations MUST run on UI thread
- Events are wired through ApplicationOrchestrator to maintain loose coupling

### Working with Popups
- Inherit from BasePopupWindow for consistent behavior
- Handle multi-monitor scenarios in ShowPopup methods
- Test popup behavior with E2E tests for proper positioning and user interaction

### Configuration Changes
- Modify AppConfiguration model for new settings
- Update ConfigurationService validation logic
- Add UI elements in MainWindow.xaml with proper data binding
- Test configuration persistence and default value handling

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

### Summary report
- Give output detail after every completion