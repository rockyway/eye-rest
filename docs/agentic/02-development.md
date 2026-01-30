# Phase 2: Development Guide

> Load this document when adding services, working with timers, popups, or modifying existing functionality.

---

## Adding New Services

### Step-by-Step Process

1. **Create Interface** in `Services/Abstractions/`
   ```csharp
   public interface IMyService
   {
       void DoSomething();
       event EventHandler<MyEventArgs> SomethingHappened;
   }
   ```

2. **Implement Service** in `Services/Implementation/`
   ```csharp
   public class MyService : IMyService, IDisposable
   {
       private readonly ILogger<MyService> _logger;

       public MyService(ILogger<MyService> logger)
       {
           _logger = logger;
       }

       public event EventHandler<MyEventArgs>? SomethingHappened;

       public void DoSomething()
       {
           try
           {
               // Implementation
               OnSomethingHappened(new MyEventArgs());
           }
           catch (Exception ex)
           {
               _logger.LogError(ex, "Failed to do something");
               throw;
           }
       }

       protected virtual void OnSomethingHappened(MyEventArgs e)
       {
           SomethingHappened?.Invoke(this, e);
       }

       public void Dispose()
       {
           // Cleanup resources
       }
   }
   ```

3. **Register in DI Container** (`App.xaml.cs`)
   ```csharp
   private void ConfigureServices(IServiceCollection services)
   {
       // ... existing registrations
       services.AddSingleton<IMyService, MyService>();
   }
   ```

4. **Wire up in Orchestrator** (if coordination needed)
   ```csharp
   // In ApplicationOrchestrator constructor
   _myService.SomethingHappened += OnSomethingHappened;

   // In Dispose
   _myService.SomethingHappened -= OnSomethingHappened;
   ```

5. **Add Unit Tests** in `EyeRest.Tests/Services/`
   ```csharp
   public class MyServiceTests
   {
       [Fact]
       public void DoSomething_WhenCalled_RaisesEvent()
       {
           // Arrange
           var mockLogger = new Mock<ILogger<MyService>>();
           var sut = new MyService(mockLogger.Object);
           var eventRaised = false;
           sut.SomethingHappened += (_, _) => eventRaised = true;

           // Act
           sut.DoSomething();

           // Assert
           Assert.True(eventRaised);
       }
   }
   ```

---

## Working with Timers

### DispatcherTimer Requirements
- **MUST run on UI thread** - Initialize in App.xaml.cs startup
- Timer tick events fire on UI thread
- Cannot access from background threads

### Timer Patterns

```csharp
public class MyTimerService : IDisposable
{
    private readonly DispatcherTimer _timer;

    public MyTimerService(Dispatcher dispatcher)
    {
        // Create timer on UI thread
        dispatcher.Invoke(() =>
        {
            _timer = new DispatcherTimer
            {
                Interval = TimeSpan.FromMinutes(20)
            };
            _timer.Tick += OnTimerTick;
        });
    }

    public void Start()
    {
        // Safe to call from any thread - DispatcherTimer handles marshalling
        _timer.Start();
    }

    private void OnTimerTick(object? sender, EventArgs e)
    {
        // Already on UI thread
        RaiseTimerDueEvent();
    }

    public void Dispose()
    {
        _timer.Stop();
        _timer.Tick -= OnTimerTick;
    }
}
```

### Event Chain
```
TimerService → ApplicationOrchestrator → NotificationService/AudioService
```

Events are wired through ApplicationOrchestrator to maintain loose coupling.

---

## Working with Popups

### Popup Hierarchy
- **BasePopupWindow**: Base class with common functionality
- **EyeRestPopup**: 20-second eye rest reminder
- **BreakPopup**: 5-minute break with user controls
- **WarningPopups**: Pre-notification warnings (30 seconds before)

### Creating New Popup Types

1. **Inherit from BasePopupWindow**
   ```xaml
   <local:BasePopupWindow x:Class="EyeRest.Views.MyPopup"
       xmlns:local="clr-namespace:EyeRest.Views">
       <!-- Content -->
   </local:BasePopupWindow>
   ```

2. **Handle Multi-Monitor Scenarios**
   ```csharp
   public void ShowOnAllMonitors()
   {
       foreach (var screen in System.Windows.Forms.Screen.AllScreens)
       {
           var popup = CreatePopupForScreen(screen);
           popup.Show();
       }
   }
   ```

3. **Implement User Controls**
   ```csharp
   private void DelayButton_Click(object sender, RoutedEventArgs e)
   {
       // Raise event for orchestrator to handle
       DelayRequested?.Invoke(this, new DelayEventArgs(TimeSpan.FromMinutes(5)));
       Close();
   }
   ```

4. **Test with E2E Tests**
   - Verify proper positioning on each monitor
   - Test user interactions (delay, skip, complete)
   - Validate countdown timer accuracy

---

## Configuration Changes

### Adding New Settings

1. **Modify AppConfiguration Model**
   ```csharp
   public class AppConfiguration
   {
       public MyFeatureSettings MyFeature { get; set; } = new();
   }

   public class MyFeatureSettings
   {
       public bool Enabled { get; set; } = true;
       public int Threshold { get; set; } = 10;
   }
   ```

2. **Update ConfigurationService Validation**
   ```csharp
   private void ValidateConfiguration(AppConfiguration config)
   {
       if (config.MyFeature.Threshold < 1 || config.MyFeature.Threshold > 100)
       {
           config.MyFeature.Threshold = 10; // Reset to default
       }
   }
   ```

3. **Add UI in MainWindow.xaml**
   ```xaml
   <CheckBox Content="Enable My Feature"
             IsChecked="{Binding Configuration.MyFeature.Enabled}"/>
   <Slider Value="{Binding Configuration.MyFeature.Threshold}"
           Minimum="1" Maximum="100"/>
   ```

4. **Test Persistence**
   - Configuration saves correctly
   - Default values restore on corruption
   - Change notifications fire properly

---

## Error Handling Patterns

### Service-Level Error Handling
```csharp
public async Task DoWorkAsync()
{
    try
    {
        await PerformOperation();
    }
    catch (SpecificException ex)
    {
        _logger.LogWarning(ex, "Known issue occurred, using fallback");
        await UseFallbackBehavior();
    }
    catch (Exception ex)
    {
        _logger.LogError(ex, "Unexpected error in DoWorkAsync");
        // Graceful degradation - don't crash the app
        NotifyUserOfError(ex.Message);
    }
}
```

### UI Thread Error Handling
```csharp
private void SafeUIOperation(Action action)
{
    try
    {
        _dispatcher.Invoke(action);
    }
    catch (TaskCanceledException)
    {
        // Application shutting down, ignore
    }
    catch (Exception ex)
    {
        _logger.LogError(ex, "UI operation failed");
    }
}
```

---

## Common Gotchas

### Thread Safety
- Never access DispatcherTimer from background thread
- Use `Dispatcher.BeginInvoke` for UI updates from background
- WeakEventManager for long-lived subscriptions

### Memory Leaks
- Always unsubscribe events in Dispose
- Use WeakEventManager for timer events
- Dispose services properly

### System Tray
- Minimize to tray instead of closing (intercept Close event)
- Update icon state on timer/pause changes
- Handle system sleep/wake properly

### Multi-Monitor
- Test popup positioning on secondary monitors
- Handle monitor disconnect gracefully
- Consider different DPI settings per monitor
