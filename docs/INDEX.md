# Eye-rest Documentation Index

## 📚 Project Overview
Eye-rest is a Windows desktop application built with .NET 8 and WPF that provides automated eye rest and break reminders to promote healthy computer usage habits.

**Quick Links**:
- [📖 README](../README.md) - Project overview and setup
- [⚙️ CLAUDE.md](../CLAUDE.md) - Development guidance for Claude Code
- [📋 Product Requirements](requirements.md) - Complete PRD and specifications

---

## 🏗️ Architecture & Design

### Core Documentation
- [📐 API Reference](API_REFERENCE.md) - Complete interface and service documentation
- [🗂️ Project Structure](PROJECT_STRUCTURE.md) - Detailed codebase organization
- [⚡ Performance Specs](#performance-specifications) - Resource usage and timing requirements

### Key Architectural Patterns
- **MVVM Architecture**: Clean separation between UI, presentation, and business logic
- **Dependency Injection**: Microsoft.Extensions.DI for service management
- **Event-Driven Design**: Timer events coordinate all application behavior
- **Service-Oriented Architecture**: Modular services with focused responsibilities

---

## 🔧 Development Guide

### Getting Started
```bash
# Clone and build
git clone <repository>
cd eye-rest
dotnet build

# Run application
dotnet run

# Run tests
dotnet test
```

### Development Commands
| Command | Purpose |
|---------|---------|
| `dotnet build` | Build application |
| `dotnet run` | Run application |
| `dotnet test` | Run all tests |
| `dotnet test --filter Category=Unit` | Run unit tests only |
| `dotnet test --filter Category=E2E` | Run end-to-end tests |
| `run-ui-tests.bat` | Run UI automation tests |

### Project Structure Quick Reference
```
EyeRest/
├── Services/          # Business logic (ITimerService, INotificationService, etc.)
├── ViewModels/        # MVVM presentation logic
├── Views/             # WPF XAML windows and popups
├── Models/            # Configuration DTOs (AppConfiguration)
├── Infrastructure/    # Utilities (WeakEventManager)
├── Resources/         # Themes and visual assets
└── EyeRest.Tests/     # Comprehensive test suite
```

---

## 🎯 Core Services Reference

### Timer System
- **[ITimerService](API_REFERENCE.md#itimerservice)**: Dual-timer system (eye rest + breaks)
- **Events**: EyeRestDue, BreakDue, EyeRestWarning, BreakWarning
- **Threading**: All operations must run on UI thread (DispatcherTimer)

### Notification System
- **[INotificationService](API_REFERENCE.md#inotificationservice)**: Full-screen popup management
- **Multi-Monitor**: Popups span all connected monitors during breaks
- **User Controls**: Delay (1min/5min) and Skip options

### Configuration System
- **[IConfigurationService](API_REFERENCE.md#iconfigurationservice)**: JSON-based settings persistence
- **Location**: `%APPDATA%/EyeRest/config.json`
- **Models**: [AppConfiguration](API_REFERENCE.md#appconfiguration) with nested settings

### System Integration
- **[ISystemTrayService](API_REFERENCE.md#isystemtrayservice)**: Windows system tray integration
- **[IAudioService](API_REFERENCE.md#iaudioservice)**: Sound notifications
- **[IPerformanceMonitor](API_REFERENCE.md#iperformancemonitor)**: Resource usage monitoring

---

## 🧪 Testing Strategy

### Test Architecture Overview
| Test Type | Location | Framework | Purpose |
|-----------|----------|-----------|---------|
| **Unit Tests** | `EyeRest.Tests/Services/` | xUnit + Moq | Individual service testing |
| **Integration** | `EyeRest.Tests/Integration/` | xUnit | Service interaction testing |
| **Performance** | `EyeRest.Tests/Performance/` | xUnit | Resource usage validation |
| **E2E Tests** | `EyeRest.Tests/E2E/` | xUnit + TestStack.White | Complete workflow testing |
| **UI Tests** | `EyeRest.Tests/UI/` | TestStack.White | WPF UI automation |

### Running Tests
```bash
# All tests
dotnet test

# By category
dotnet test --filter Category=Unit
dotnet test --filter Category=Integration  
dotnet test --filter Category=Performance
dotnet test --filter Category=E2E

# UI tests (special runner required)
dotnet run -- RunUITests --build
# OR
run-ui-tests.bat
```

### Test Coverage Areas
- ✅ **Service Logic**: All business logic services with mocks
- ✅ **Timer Behavior**: Countdown, events, state management
- ✅ **Configuration**: Settings persistence and validation
- ✅ **Performance**: Startup time (<3s) and memory usage (<50MB)
- ✅ **UI Workflows**: Complete user interaction scenarios
- ✅ **System Integration**: Tray, audio, multi-monitor behavior

---

## 📊 Performance Specifications

### Resource Requirements
| Metric | Target | Monitoring |
|--------|--------|------------|
| **Startup Time** | < 3 seconds | StartupPerformanceTests |
| **Memory Usage** | < 50MB idle, < 100MB active | PerformanceMonitor service |
| **CPU Usage** | < 1% idle, < 5% during notifications | PerformanceMonitor service |
| **Response Time** | < 100ms for user interactions | UI automation tests |

### Optimization Strategies
- **Lazy Loading**: Services initialized on-demand
- **Resource Reuse**: Popup windows cached and reused
- **Weak Events**: Prevents memory leaks in timer scenarios
- **Background Processing**: Heavy operations on background threads
- **Auto GC**: Triggered when approaching memory limits

---

## 🎨 User Interface Components

### Window Hierarchy
```
MainWindow (Settings UI)
├── BasePopupWindow (Base class)
    ├── EyeRestPopup (20-second eye rest)
    ├── EyeRestWarningPopup (Pre-eye rest warning)
    ├── BreakPopup (5-minute break with controls)
    └── BreakWarningPopup (Pre-break warning)
```

### UI Design Patterns
- **Multi-Monitor Aware**: Full-screen popups across all monitors
- **User Controls**: Delay/Skip buttons with proper event handling
- **Visual Feedback**: Progress bars, color changes, icon states
- **Accessibility**: WCAG compliance, keyboard navigation

---

## 🔗 Configuration Reference

### Settings Structure
```json
{
  "eyeRest": {
    "intervalMinutes": 20,
    "durationSeconds": 20,
    "startSoundEnabled": true,
    "endSoundEnabled": true,
    "warningEnabled": true,
    "warningSeconds": 30
  },
  "break": {
    "intervalMinutes": 10,
    "durationMinutes": 2,
    "warningEnabled": true,
    "warningSeconds": 30
  },
  "audio": {
    "enabled": true,
    "customSoundPath": null,
    "volume": 50
  },
  "application": {
    "startWithWindows": false,
    "minimizeToTray": true,
    "showInTaskbar": false
  }
}
```

### Configuration Management
- **Persistence**: Automatic save on change
- **Validation**: Default values restored if corrupt
- **Change Notifications**: ConfigurationChanged event
- **Location**: `%APPDATA%/EyeRest/config.json`

---

## 🛠️ Development Workflows

### Adding New Services
1. Create interface in `Services/` folder
2. Implement service with error handling and logging
3. Register in `App.xaml.cs` DI container
4. Wire up in `ApplicationOrchestrator` if coordination needed
5. Add unit tests in `EyeRest.Tests/Services/`

### Modifying Timer Behavior
- Timer logic in `TimerService.cs` with DispatcherTimer
- **Critical**: All timer operations MUST run on UI thread
- Events coordinated through `ApplicationOrchestrator`
- Configuration changes require timer service restart

### Working with Popups
- Inherit from `BasePopupWindow` for consistent behavior
- Handle multi-monitor scenarios in ShowPopup methods
- Test behavior with E2E tests for positioning and interaction

### Configuration Changes
- Modify `AppConfiguration` model for new settings
- Update `ConfigurationService` validation logic
- Add UI elements in `MainWindow.xaml` with data binding
- Test persistence and default value handling

---

## 🐛 Troubleshooting Guide

### Common Issues
| Issue | Symptoms | Solution |
|-------|----------|----------|
| **Timer Not Starting** | No notifications, inactive tray icon | Check UI thread initialization in App.xaml.cs |
| **Popup Not Showing** | Timer events fire but no UI | Verify NotificationService registration and multi-monitor setup |
| **Configuration Lost** | Settings reset to defaults | Check file permissions in %APPDATA%/EyeRest/ |
| **High Memory Usage** | > 100MB usage | Check WeakEventManager usage, GC triggering |
| **UI Tests Failing** | TestStack.White errors | Ensure WPF application accessibility enabled |

### Debugging Steps
1. **Check Logging**: Review application logs for error patterns
2. **Performance Metrics**: Use PerformanceMonitor for resource usage
3. **Event Flow**: Verify timer events reaching ApplicationOrchestrator
4. **UI Thread**: Confirm DispatcherTimer operations on correct thread
5. **Service Registration**: Validate DI container configuration

---

## 📝 Contributing Guidelines

### Code Quality Standards
- **Error Handling**: Comprehensive try-catch with logging
- **Thread Safety**: Proper Dispatcher usage for UI operations
- **Memory Management**: Dispose pattern and weak event handlers
- **Testing**: Unit tests for all new services and features
- **Documentation**: Update API docs for interface changes

### Development Environment
- **.NET 8 SDK**: Required for building and running
- **Visual Studio 2022** or **VS Code**: Recommended IDEs
- **Windows 10/11**: Required for system tray and WPF testing
- **TestStack.White**: For UI automation testing

---

## 📚 Additional Resources

### External Documentation
- [Microsoft WPF Documentation](https://docs.microsoft.com/en-us/dotnet/desktop/wpf/)
- [.NET 8 Documentation](https://docs.microsoft.com/en-us/dotnet/core/)
- [TestStack.White Documentation](https://github.com/TestStack/White)
- [Microsoft.Extensions.DependencyInjection](https://docs.microsoft.com/en-us/dotnet/core/extensions/dependency-injection)

### Project-Specific Guides
- [E2E Test Plan](../EyeRest.Tests/E2E/E2ETestPlan.md) - Detailed E2E testing strategy
- [UI Testing Framework](../UI_Testing_Framework_Documentation.md) - Custom UI test utilities
- [Manual Verification Checklist](../MANUAL_VERIFICATION_CHECKLIST.md) - QA validation steps

---

*Last Updated: Generated by /sc:index command*  
*For questions or contributions, see project README.md*