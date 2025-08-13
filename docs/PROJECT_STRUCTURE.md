# Eye-rest Project Structure

## Overview
This document provides a comprehensive breakdown of the Eye-rest application structure, organized by functional areas and architectural layers.

## Root Directory Structure

```
EyeRest/
├── 📁 Services/              # Business logic and system integration services
├── 📁 ViewModels/           # MVVM presentation logic with data binding
├── 📁 Views/                # WPF XAML windows and user controls
├── 📁 Models/               # Data models and configuration DTOs
├── 📁 Infrastructure/       # Utilities and framework components
├── 📁 Resources/           # Themes, icons, and visual assets
├── 📁 Converters/          # WPF value converters for data binding
├── 📁 EyeRest.Tests/       # Comprehensive test suite
├── 📁 docs/                # Project documentation
├── 📁 bin/                 # Build output directory
├── 📁 obj/                 # Intermediate build files
├── 📄 App.xaml              # Application entry point and DI configuration
├── 📄 App.xaml.cs           # Application startup and service registration
├── 📄 EyeRest.csproj        # Project file with dependencies
├── 📄 EyeRest.sln           # Visual Studio solution file
├── 📄 app.manifest          # Windows application manifest
├── 📄 CLAUDE.md             # Claude Code development guidance
└── 📄 README.md             # Project overview and setup instructions
```

## Core Application Layer

### Services/ - Business Logic Layer
**Purpose**: Encapsulates all business logic and system integration

```
Services/
├── 🔌 IApplicationOrchestrator    # Service coordination interface
├── ⚙️ ApplicationOrchestrator.cs  # Central service coordinator
├── 🔌 ITimerService.cs           # Timer management interface
├── ⏰ TimerService.cs             # Dual-timer implementation
├── 🔌 INotificationService.cs    # Popup notification interface
├── 🖥️ NotificationService.cs     # Full-screen popup manager
├── 🔌 IConfigurationService.cs   # Settings persistence interface
├── ⚙️ ConfigurationService.cs    # JSON configuration manager
├── 🔌 IAudioService.cs           # Audio notification interface
├── 🔊 AudioService.cs            # System sound integration
├── 🔌 ISystemTrayService.cs      # System tray interface
├── 🖥️ SystemTrayService.cs       # Windows tray integration
├── 📊 IPerformanceMonitor.cs     # Performance monitoring interface
├── 📈 PerformanceMonitor.cs      # Resource usage monitor
├── 🔌 IStartupManager.cs         # Windows startup interface
├── 🚀 StartupManager.cs          # Startup registration manager
├── 🔌 ILoggingService.cs         # Logging interface
├── 📝 LoggingService.cs          # Application logging service
├── 🎨 IconService.cs             # System tray icon management
└── 🖥️ TrayService.cs             # Legacy tray service
```

**Key Service Responsibilities**:
- **ApplicationOrchestrator**: Coordinates all service interactions and event handling
- **TimerService**: Manages dual-timer system with DispatcherTimer
- **NotificationService**: Creates and manages full-screen popups across monitors
- **ConfigurationService**: Handles JSON-based settings persistence
- **SystemTrayService**: Integrates with Windows system tray
- **PerformanceMonitor**: Tracks memory and CPU usage

### ViewModels/ - Presentation Logic Layer
**Purpose**: MVVM presentation logic with data binding support

```
ViewModels/
├── 🎯 MainWindowViewModel.cs     # Main window presentation logic
├── 🔧 ViewModelBase.cs           # Base class with INotifyPropertyChanged
└── ⚡ RelayCommand.cs             # Command pattern implementation
```

**Architecture Pattern**: MVVM (Model-View-ViewModel)
- **ViewModelBase**: Provides INotifyPropertyChanged implementation
- **RelayCommand**: Implements ICommand for button click handling
- **MainWindowViewModel**: Handles main window state and user interactions

### Views/ - User Interface Layer
**Purpose**: WPF XAML windows and user controls

```
Views/
├── 🪟 MainWindow.xaml            # Main application window
├── 📄 MainWindow.xaml.cs         # Main window code-behind
├── 🔧 BasePopupWindow.xaml       # Base class for all popups
├── 📄 BasePopupWindow.xaml.cs    # Base popup code-behind
├── 👁️ EyeRestPopup.xaml          # Eye rest reminder popup
├── 📄 EyeRestPopup.xaml.cs       # Eye rest popup code-behind
├── ⚠️ EyeRestWarningPopup.xaml   # Eye rest warning popup
├── 📄 EyeRestWarningPopup.xaml.cs # Eye rest warning code-behind
├── 🛑 BreakPopup.xaml            # Break reminder popup
├── 📄 BreakPopup.xaml.cs         # Break popup code-behind
├── ⚠️ BreakWarningPopup.xaml     # Break warning popup
└── 📄 BreakWarningPopup.xaml.cs  # Break warning code-behind
```

**Popup Hierarchy**:
- **BasePopupWindow**: Common popup functionality (positioning, multi-monitor)
- **EyeRestPopup**: 20-second eye rest reminder
- **BreakPopup**: 5-minute break reminder with user controls
- **Warning Popups**: Pre-notification warnings (30 seconds before)

### Models/ - Data Layer
**Purpose**: Configuration DTOs and data structures

```
Models/
└── 📊 AppConfiguration.cs        # Configuration data models
    ├── EyeRestSettings          # Eye rest configuration
    ├── BreakSettings           # Break configuration
    ├── AudioSettings           # Audio configuration
    └── ApplicationSettings     # Application behavior settings
```

## Supporting Infrastructure

### Infrastructure/ - Framework Components
**Purpose**: Utilities and cross-cutting concerns

```
Infrastructure/
└── 🔄 WeakEventManager.cs        # Memory leak prevention utility
```

**Key Components**:
- **WeakEventManager**: Prevents memory leaks in long-running timer scenarios

### Resources/ - Visual Assets
**Purpose**: Themes, icons, and visual resources

```
Resources/
├── 📁 Themes/
│   └── DefaultTheme.xaml         # Application theme definitions
└── 🎨 app.ico                    # Application icon
```

### Converters/ - Data Binding Support
**Purpose**: WPF value converters for UI data binding

```
Converters/
└── 🔄 BooleanToVisibilityConverter.cs  # Bool to Visibility conversion
```

## Test Architecture

### EyeRest.Tests/ - Comprehensive Test Suite
**Purpose**: Multi-layered testing strategy

```
EyeRest.Tests/
├── 📁 Services/                  # Service unit tests
│   ├── ConfigurationServiceTests.cs
│   ├── TimerServiceTests.cs
│   └── AudioServiceTests.cs
├── 📁 ViewModels/               # ViewModel unit tests
│   └── MainWindowViewModelTests.cs
├── 📁 Integration/              # Service integration tests
│   ├── TimerNotificationIntegrationTests.cs
│   └── ComprehensiveFunctionalityTests.cs
├── 📁 Performance/              # Performance validation tests
│   ├── StartupPerformanceTests.cs
│   └── MemoryUsageTests.cs
├── 📁 E2E/                      # End-to-end workflow tests
│   ├── E2ETestSuite.cs
│   ├── E2ETestRunner.cs
│   ├── CountdownTimerTests.cs
│   ├── AutoStartFunctionalityTests.cs
│   └── IconIntegrationTests.cs
├── 📁 UI/                       # UI automation tests
│   ├── UIAutomationFramework.cs
│   ├── UITestRunner.cs
│   └── ComprehensiveUITests.cs
├── 📁 EndToEnd/                 # Complete application tests
│   └── ApplicationEndToEndTests.cs
├── 📄 TestConfiguration.cs      # Test configuration utilities
└── 📄 RunUITests.cs             # UI test execution entry point
```

**Test Strategy**:
- **Unit Tests**: Individual service and ViewModel testing with Moq
- **Integration Tests**: Cross-service interaction validation
- **Performance Tests**: Startup time and memory usage validation
- **E2E Tests**: Complete user workflow testing with TestStack.White
- **UI Tests**: Specialized WPF UI automation testing

## Documentation Structure

### docs/ - Project Documentation
**Purpose**: Comprehensive project documentation and planning

```
docs/
├── 📄 requirements.md           # Product Requirements Document (PRD)
├── 📄 API_REFERENCE.md          # Complete API documentation
├── 📄 PROJECT_STRUCTURE.md      # This document
├── 📁 plans/                    # Implementation phase plans
└── 📁 features/                 # Feature breakdown documentation
```

## Build and Configuration Files

### Project Configuration
```
📄 EyeRest.csproj               # Main project file (.NET 8, WPF)
📄 EyeRest.sln                  # Visual Studio solution
📄 EyeRest.Tests.csproj         # Test project file (xUnit, Moq, TestStack.White)
📄 app.manifest                 # Windows application manifest (DPI awareness)
```

### Build Scripts and Utilities
```
📄 run-ui-tests.bat            # UI test execution batch script
📄 RunFunctionalTests.cs       # Functional test runner
📄 RunUIValidationTest.cs      # UI validation test runner
```

## Architectural Patterns

### Dependency Injection
- **Container**: Microsoft.Extensions.DependencyInjection
- **Registration**: All services registered in `App.xaml.cs`
- **Lifetime**: Singleton for stateful services, Transient for ViewModels

### Event-Driven Architecture
1. **TimerService** → Events → **ApplicationOrchestrator**
2. **ApplicationOrchestrator** → Coordination → **NotificationService**, **AudioService**
3. **SystemTrayService** → User Events → **MainWindow**

### MVVM Pattern
- **Models**: Configuration DTOs in Models/
- **Views**: XAML files in Views/
- **ViewModels**: Presentation logic in ViewModels/
- **Data Binding**: Two-way binding with INotifyPropertyChanged

### Service-Oriented Design
- **Interface Segregation**: Each service has focused interface
- **Single Responsibility**: Services have single, well-defined purpose
- **Loose Coupling**: Services communicate through events and interfaces

## Performance Characteristics

### Memory Usage
- **Target**: < 50MB idle, < 100MB active
- **Monitoring**: PerformanceMonitor service tracks usage
- **Optimization**: Lazy loading, resource reuse, weak event handlers

### Startup Performance
- **Target**: < 3 seconds from launch to ready
- **Strategy**: Service initialization on UI thread, lazy loading
- **Measurement**: StartupPerformanceTests validate timing

### Multi-Monitor Support
- **Full-Screen Popups**: Span all connected monitors during breaks
- **Positioning**: BasePopupWindow handles multi-monitor scenarios
- **Testing**: UI tests validate behavior across monitor configurations

## Thread Safety Model

### UI Thread Operations
- **DispatcherTimer**: All timer operations on UI thread
- **UI Updates**: Use Dispatcher.BeginInvoke from background threads
- **WPF Binding**: Automatic UI thread marshalling

### Background Operations
- **Heavy Computations**: Performance monitoring, file I/O
- **Thread Synchronization**: Proper use of async/await patterns
- **Resource Cleanup**: Dispose pattern for background resources