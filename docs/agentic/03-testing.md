# Phase 3: Testing Guide

> Load this document when writing tests, understanding coverage requirements, or running test suites.

---

## Testing Strategy Overview

| Category | Purpose | Framework | Location |
|----------|---------|-----------|----------|
| Unit | Individual component testing | xUnit + Moq | `EyeRest.Tests/Services/`, `EyeRest.Tests/ViewModels/` |
| Integration | Service interaction testing | xUnit | `EyeRest.Tests/Integration/` |
| Performance | Startup time, memory validation | xUnit + custom | `EyeRest.Tests/Performance/` |
| E2E | Complete workflow testing | xUnit + TestStack.White | `EyeRest.Tests/E2E/` |
| UI | WPF UI automation | TestStack.White | `EyeRest.Tests/UI/` |

---

## Running Tests

```bash
# Run all tests
dotnet test

# Run specific categories
dotnet test --filter Category=Unit
dotnet test --filter Category=Integration
dotnet test --filter Category=Performance
dotnet test --filter Category=E2E

# Run UI tests (requires special runner)
run-ui-tests.bat
# OR
dotnet run -- RunUITests --build

# Run with verbose output
dotnet test --logger "console;verbosity=detailed"

# Run specific test class
dotnet test --filter "FullyQualifiedName~TimerServiceTests"
```

---

## Unit Testing

### Service Tests

```csharp
public class ConfigurationServiceTests
{
    private readonly Mock<ILogger<ConfigurationService>> _mockLogger;
    private readonly Mock<IFileSystem> _mockFileSystem;
    private readonly ConfigurationService _sut;

    public ConfigurationServiceTests()
    {
        _mockLogger = new Mock<ILogger<ConfigurationService>>();
        _mockFileSystem = new Mock<IFileSystem>();
        _sut = new ConfigurationService(_mockLogger.Object, _mockFileSystem.Object);
    }

    [Fact]
    public async Task LoadAsync_WhenFileExists_ReturnsConfiguration()
    {
        // Arrange
        var json = """{"EyeRest": {"IntervalMinutes": 20}}""";
        _mockFileSystem.Setup(x => x.ReadAllTextAsync(It.IsAny<string>()))
            .ReturnsAsync(json);

        // Act
        var config = await _sut.LoadAsync();

        // Assert
        Assert.NotNull(config);
        Assert.Equal(20, config.EyeRest.IntervalMinutes);
    }

    [Fact]
    public async Task LoadAsync_WhenFileCorrupt_ReturnsDefaults()
    {
        // Arrange
        _mockFileSystem.Setup(x => x.ReadAllTextAsync(It.IsAny<string>()))
            .ReturnsAsync("invalid json {{{");

        // Act
        var config = await _sut.LoadAsync();

        // Assert
        Assert.NotNull(config);
        Assert.Equal(20, config.EyeRest.IntervalMinutes); // Default value
    }
}
```

### ViewModel Tests

```csharp
public class MainWindowViewModelTests
{
    private readonly Mock<IConfigurationService> _mockConfig;
    private readonly Mock<ITimerService> _mockTimer;
    private readonly MainWindowViewModel _sut;

    public MainWindowViewModelTests()
    {
        _mockConfig = new Mock<IConfigurationService>();
        _mockTimer = new Mock<ITimerService>();
        _sut = new MainWindowViewModel(_mockConfig.Object, _mockTimer.Object);
    }

    [Fact]
    public void SaveCommand_WhenExecuted_SavesConfiguration()
    {
        // Arrange
        _sut.Configuration.EyeRest.IntervalMinutes = 25;

        // Act
        _sut.SaveCommand.Execute(null);

        // Assert
        _mockConfig.Verify(x => x.SaveAsync(It.IsAny<AppConfiguration>()), Times.Once);
    }

    [Fact]
    public void PropertyChanged_WhenConfigurationChanges_RaisesEvent()
    {
        // Arrange
        var propertyChangedRaised = false;
        _sut.PropertyChanged += (_, e) =>
        {
            if (e.PropertyName == nameof(_sut.Configuration))
                propertyChangedRaised = true;
        };

        // Act
        _sut.Configuration = new AppConfiguration();

        // Assert
        Assert.True(propertyChangedRaised);
    }
}
```

---

## Integration Testing

```csharp
[Trait("Category", "Integration")]
public class TimerNotificationIntegrationTests
{
    [Fact]
    public async Task TimerService_WhenEyeRestDue_NotificationServiceShowsPopup()
    {
        // Arrange
        var services = new ServiceCollection();
        ConfigureTestServices(services);
        var provider = services.BuildServiceProvider();

        var timerService = provider.GetRequiredService<ITimerService>();
        var notificationService = provider.GetRequiredService<INotificationService>();

        var popupShown = new TaskCompletionSource<bool>();
        notificationService.PopupShown += (_, _) => popupShown.TrySetResult(true);

        // Act
        timerService.TriggerEyeRestDue(); // Test helper method

        // Assert
        var shown = await popupShown.Task.WaitAsync(TimeSpan.FromSeconds(5));
        Assert.True(shown);
    }
}
```

---

## Performance Testing

### Startup Performance

```csharp
[Trait("Category", "Performance")]
public class StartupPerformanceTests
{
    [Fact]
    public void Application_StartsWithin3Seconds()
    {
        // Arrange
        var stopwatch = Stopwatch.StartNew();

        // Act
        var app = new TestableApp();
        app.InitializeServices();
        stopwatch.Stop();

        // Assert
        Assert.True(stopwatch.ElapsedMilliseconds < 3000,
            $"Startup took {stopwatch.ElapsedMilliseconds}ms, expected <3000ms");
    }
}
```

### Memory Usage

```csharp
[Trait("Category", "Performance")]
public class MemoryUsageTests
{
    [Fact]
    public void IdleApplication_UsesLessThan50MB()
    {
        // Arrange
        var app = new TestableApp();
        app.InitializeServices();

        // Force GC to get accurate reading
        GC.Collect();
        GC.WaitForPendingFinalizers();
        GC.Collect();

        // Act
        var memoryMB = Process.GetCurrentProcess().PrivateMemorySize64 / (1024 * 1024);

        // Assert
        Assert.True(memoryMB < 50,
            $"Memory usage is {memoryMB}MB, expected <50MB");
    }
}
```

---

## E2E Testing

### TestStack.White Setup

```csharp
[Trait("Category", "E2E")]
public class ApplicationEndToEndTests : IDisposable
{
    private Application _application;
    private Window _mainWindow;

    public ApplicationEndToEndTests()
    {
        var appPath = Path.Combine(TestContext.OutputDirectory, "EyeRest.exe");
        _application = Application.Launch(appPath);
        _mainWindow = _application.GetWindow("Eye-rest Settings", InitializeOption.NoCache);
    }

    [Fact]
    public void SettingsWindow_WhenSaveClicked_PersistsChanges()
    {
        // Arrange
        var intervalSlider = _mainWindow.Get<Slider>("EyeRestIntervalSlider");
        var saveButton = _mainWindow.Get<Button>("SaveButton");

        // Act
        intervalSlider.Value = 25;
        saveButton.Click();

        // Close and reopen
        _mainWindow.Close();
        _mainWindow = _application.GetWindow("Eye-rest Settings");
        intervalSlider = _mainWindow.Get<Slider>("EyeRestIntervalSlider");

        // Assert
        Assert.Equal(25, intervalSlider.Value);
    }

    public void Dispose()
    {
        _application?.Close();
    }
}
```

---

## Mocking Patterns

### Common Mocks

```csharp
// Mock Dispatcher
var mockDispatcher = new Mock<IDispatcher>();
mockDispatcher.Setup(x => x.Invoke(It.IsAny<Action>()))
    .Callback<Action>(action => action());
mockDispatcher.Setup(x => x.BeginInvoke(It.IsAny<Action>()))
    .Callback<Action>(action => action());

// Mock File System
var mockFileSystem = new Mock<IFileSystem>();
mockFileSystem.Setup(x => x.FileExists(It.IsAny<string>())).Returns(true);
mockFileSystem.Setup(x => x.ReadAllTextAsync(It.IsAny<string>()))
    .ReturnsAsync("{\"valid\": \"json\"}");

// Mock Logger (usually just create mock, no setup needed)
var mockLogger = new Mock<ILogger<MyService>>();
```

### Verifying Interactions

```csharp
// Verify method was called
_mockService.Verify(x => x.DoSomething(), Times.Once);

// Verify with specific arguments
_mockService.Verify(x => x.DoSomething(It.Is<int>(i => i > 0)), Times.AtLeastOnce);

// Verify never called
_mockService.Verify(x => x.DangerousOperation(), Times.Never);
```

---

## Test Coverage Requirements

| Component | Target Coverage |
|-----------|----------------|
| Services | ≥80% |
| ViewModels | ≥70% |
| New Code | ≥80% |
| Modified Code | ≥80% |

### What to Test
- All public methods
- Service layer fully covered
- ViewModel commands and properties
- Edge cases and error paths
- Event handling

### What to Skip
- Pure XAML files (no code-behind logic)
- Simple property wrappers
- Auto-generated code
- Third-party library code

---

## Test Naming Convention

```
MethodName_StateUnderTest_ExpectedBehavior
```

Examples:
- `Start_WhenNotRunning_StartsTimer`
- `LoadAsync_WhenFileCorrupt_ReturnsDefaults`
- `SaveCommand_WhenExecuted_SavesConfiguration`

---

## Running UI Tests

UI tests require special setup due to WPF/TestStack.White integration.

```bash
# Build first
dotnet build

# Run via batch file
run-ui-tests.bat

# Or via dotnet
dotnet run -- RunUITests --build
```

**Note**: UI tests may fail in CI environments without display. Use headless testing or skip in CI.

---

## Troubleshooting Tests

### Common Issues

1. **DispatcherTimer tests fail**
   - Solution: Mock the Dispatcher properly
   - Use `mockDispatcher.Setup(x => x.Invoke(...)).Callback<Action>(a => a())`

2. **Async tests timeout**
   - Solution: Use `Task.WaitAsync(TimeSpan)` with reasonable timeout
   - Check for deadlocks from sync-over-async

3. **UI tests can't find elements**
   - Solution: Ensure elements have `Name` property set in XAML
   - Use `InitializeOption.NoCache` when getting windows

4. **Memory tests fail intermittently**
   - Solution: Force GC before measuring
   - Allow some variance (±5MB)
