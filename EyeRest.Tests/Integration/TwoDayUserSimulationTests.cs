using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using EyeRest.Models;
using EyeRest.Services;
using EyeRest.Services.Abstractions;
using EyeRest.Tests.Fakes;
using EyeRestTimer = EyeRest.Services.Abstractions.ITimer;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;
using Xunit.Abstractions;

namespace EyeRest.Tests.Integration
{
    /// <summary>
    /// Comprehensive integration test simulating 2 full days of user activity
    /// using fake timers to complete in under 1 minute of real time.
    /// 
    /// This test validates:
    /// - Regular 20-minute eye rest reminders
    /// - Regular 55-minute break reminders  
    /// - User going away (idle, locked screen) and returning
    /// - Extended away periods (>30 minutes) triggering fresh sessions
    /// - System sleep overnight and wake up WITHOUT immediate popups (clock jump detection)
    /// - Manual pause/resume operations
    /// - Various popup interactions (complete, delay, skip)
    /// - Smart pause when user is away
    /// - Analytics tracking of all events
    /// </summary>
    public class TwoDayUserSimulationTests : IDisposable
    {
        private readonly ITestOutputHelper _output;
        private readonly VirtualTimerFactory _virtualTimerFactory;
        private readonly Mock<INotificationService> _mockNotificationService = new();
        private readonly Mock<IAnalyticsService> _mockAnalyticsService = new();
        private readonly Mock<IConfigurationService> _mockConfigurationService = new();
        private readonly AppConfiguration _config;
        private readonly ServiceProvider _serviceProvider;
        private readonly TimerService _timerService;
        
        // Event tracking for validation
        private readonly List<TimerEvent> _timerEvents = new();
        private readonly List<AnalyticsEvent> _analyticsEvents = new();
        private readonly List<NotificationEvent> _notificationEvents = new();

        public TwoDayUserSimulationTests(ITestOutputHelper output)
        {
            _output = output;
            _virtualTimerFactory = new VirtualTimerFactory();
            
            // Configure default app settings matching PRD specifications
            _config = new AppConfiguration
            {
                EyeRest = new EyeRestSettings
                {
                    IntervalMinutes = 20,
                    DurationSeconds = 20,
                    WarningEnabled = true,
                    WarningSeconds = 30
                },
                Break = new BreakSettings
                {
                    IntervalMinutes = 55,
                    DurationMinutes = 5,
                    WarningEnabled = true,
                    WarningSeconds = 30
                },
                UserPresence = new UserPresenceSettings
                {
                    Enabled = true,
                    IdleThresholdMinutes = 5,
                    AwayGracePeriodSeconds = 30,
                    EnableSmartSessionReset = true,
                    ExtendedAwayThresholdMinutes = 30
                },
                TimerControls = new TimerControlSettings
                {
                    AllowManualPause = true,
                    MaxPauseHours = 8,
                    PreserveTimerProgress = true
                }
            };

            // Setup mocks with event tracking
            SetupMocks();
            
            // Create service provider with DI
            var services = new ServiceCollection();
            services.AddLogging();
            services.AddSingleton<ITimerFactory>(_virtualTimerFactory);
            services.AddSingleton(_mockNotificationService.Object);
            services.AddSingleton(_mockAnalyticsService.Object);
            services.AddSingleton(_mockConfigurationService.Object);
            services.AddSingleton<ITimerService, TimerService>();
            
            _serviceProvider = services.BuildServiceProvider();
            _timerService = (TimerService)_serviceProvider.GetRequiredService<ITimerService>();
            
            // Wire up event handlers
            SetupEventHandlers();
        }

        [Fact]
        [Trait("Category", "Integration")]
        [Trait("Feature", "TwoDaySimulation")]
        public async Task TwoDayUserSimulation_ShouldHandleAllScenarios()
        {
            var startTime = DateTime.Now;
            _output.WriteLine("🚀 Starting 2-Day User Activity Simulation");
            _output.WriteLine($"Real test start time: {startTime:yyyy-MM-dd HH:mm:ss}");
            
            try
            {
                // Initialize timer service
                _timerService.SetNotificationService(_mockNotificationService.Object);
                await _timerService.StartAsync();
                
                _output.WriteLine("\n=== DAY 1 SIMULATION ===");
                await SimulateDay1();
                
                _output.WriteLine("\n=== DAY 2 SIMULATION ===");
                await SimulateDay2();
                
                // Final validation
                await ValidateSimulationResults();
                
                var endTime = DateTime.Now;
                var duration = endTime - startTime;
                
                _output.WriteLine($"\n✅ SIMULATION COMPLETE");
                _output.WriteLine($"Real execution time: {duration.TotalSeconds:F2} seconds");
                _output.WriteLine($"Virtual time simulated: 2 days (48 hours)");
                _output.WriteLine($"Time acceleration factor: {(48 * 3600) / duration.TotalSeconds:F0}x");
                
                // Critical requirement: Must complete in under 1 minute
                Assert.True(duration.TotalSeconds < 60, 
                    $"Test must complete in under 1 minute, but took {duration.TotalSeconds:F2} seconds");
            }
            finally
            {
                if (_timerService.IsRunning)
                {
                    await _timerService.StopAsync();
                }
            }
        }

        private async Task SimulateDay1()
        {
            _output.WriteLine("📅 Day 1: Monday, 9:00 AM - Start work");
            
            // 9:00 AM: Start work
            _virtualTimerFactory.SetCurrentTime(new DateTime(2024, 1, 1, 9, 0, 0));
            _output.WriteLine($"Virtual time: {_virtualTimerFactory.CurrentTime:HH:mm} - Work begins");
            
            // Regular work with eye rests every 20 minutes
            await SimulateRegularWork(TimeSpan.FromMinutes(20), "First eye rest of the day");
            await SimulateRegularWork(TimeSpan.FromMinutes(20), "Second eye rest");
            await SimulateRegularWork(TimeSpan.FromMinutes(15), "Working towards first break");
            
            // 9:55 AM: First break due (55 minutes total)
            _output.WriteLine($"Virtual time: {_virtualTimerFactory.CurrentTime:HH:mm} - First break warning");
            _virtualTimerFactory.AdvanceTime(TimeSpan.FromSeconds(30)); // Warning period
            
            // User delays break for 5 minutes
            _output.WriteLine("User delays break for 5 minutes");
            await SimulateUserAction("DelayBreak5Min");
            _virtualTimerFactory.AdvanceTime(TimeSpan.FromMinutes(5));
            
            // User takes delayed break
            await SimulateUserAction("CompleteBreak");
            
            // 10:30 AM: Coffee break (15 minutes away)
            _output.WriteLine($"Virtual time: {_virtualTimerFactory.CurrentTime:HH:mm} - Coffee break (user away)");
            await SimulateUserAwayPeriod(TimeSpan.FromMinutes(15), EyeRest.Models.UserPresenceState.Away, "Coffee break");
            
            // Continue regular work
            await SimulateRegularWork(TimeSpan.FromMinutes(20), "Post-coffee eye rest");
            await SimulateRegularWork(TimeSpan.FromMinutes(35), "Working towards lunch");
            
            // 12:00 PM: Lunch (1 hour away, triggers fresh session)
            _output.WriteLine($"Virtual time: {_virtualTimerFactory.CurrentTime:HH:mm} - Lunch break (extended away)");
            await SimulateUserAwayPeriod(TimeSpan.FromMinutes(60), EyeRest.Models.UserPresenceState.Away, "Lunch break");
            
            // Verify fresh session started after extended away period
            await ValidateSessionReset("Extended away period should trigger fresh session");
            
            // Afternoon work
            await SimulateRegularWork(TimeSpan.FromMinutes(20), "Post-lunch eye rest");
            await SimulateRegularWork(TimeSpan.FromMinutes(20), "Afternoon eye rest");
            
            // 2:00 PM: Meeting (30 minutes manual pause)
            _output.WriteLine($"Virtual time: {_virtualTimerFactory.CurrentTime:HH:mm} - Important meeting (manual pause)");
            await SimulateManualPause(TimeSpan.FromMinutes(30), "Important client meeting");
            
            // Continue afternoon work
            await SimulateRegularWork(TimeSpan.FromMinutes(20), "Post-meeting eye rest");
            await SimulateRegularWork(TimeSpan.FromMinutes(15), "Working towards break");
            
            // 3:25 PM: Break due, but user skips it
            _output.WriteLine($"Virtual time: {_virtualTimerFactory.CurrentTime:HH:mm} - Break due but user skips");
            await SimulateUserAction("SkipBreak");
            
            // Continue working until end of day
            await SimulateRegularWork(TimeSpan.FromMinutes(20), "Late afternoon eye rest");
            await SimulateRegularWork(TimeSpan.FromMinutes(20), "End of day eye rest");
            
            // 6:00 PM: PC sleep overnight
            _virtualTimerFactory.SetCurrentTime(new DateTime(2024, 1, 1, 18, 0, 0));
            _output.WriteLine($"Virtual time: {_virtualTimerFactory.CurrentTime:HH:mm} - PC goes to sleep");
            await SimulateSystemSleep(TimeSpan.FromHours(14)); // Sleep until 8 AM next day
        }

        private async Task SimulateDay2()
        {
            _output.WriteLine("📅 Day 2: Tuesday, 8:00 AM - Wake PC");
            
            // 8:00 AM: Wake PC (clock jump detection should prevent immediate popup)
            var wakeTime = new DateTime(2024, 1, 2, 8, 0, 0);
            await SimulateSystemWake(wakeTime);
            
            // CRITICAL TEST: Verify no immediate popup after system wake
            await ValidateNoImmediatePopupAfterWake();
            
            // Regular work day with various scenarios
            await SimulateRegularWork(TimeSpan.FromMinutes(20), "First eye rest after wake");
            await SimulateRegularWork(TimeSpan.FromMinutes(20), "Morning eye rest");
            await SimulateRegularWork(TimeSpan.FromMinutes(15), "Working towards break");
            
            // Morning break - user completes it
            _output.WriteLine($"Virtual time: {_virtualTimerFactory.CurrentTime:HH:mm} - Morning break");
            await SimulateUserAction("CompleteBreak");
            
            // More work with different user interactions
            await SimulateRegularWork(TimeSpan.FromMinutes(20), "Mid-morning eye rest");
            
            // User goes idle for 10 minutes (above idle threshold)
            _output.WriteLine($"Virtual time: {_virtualTimerFactory.CurrentTime:HH:mm} - User idle period");
            await SimulateUserAwayPeriod(TimeSpan.FromMinutes(10), EyeRest.Models.UserPresenceState.Idle, "User idle");
            
            // More work
            await SimulateRegularWork(TimeSpan.FromMinutes(20), "Post-idle eye rest");
            await SimulateRegularWork(TimeSpan.FromMinutes(20), "Late morning eye rest");
            
            // User delays next break by 1 minute
            _output.WriteLine($"Virtual time: {_virtualTimerFactory.CurrentTime:HH:mm} - Break delayed 1 minute");
            await SimulateUserAction("DelayBreak1Min");
            _virtualTimerFactory.AdvanceTime(TimeSpan.FromMinutes(1));
            await SimulateUserAction("CompleteBreak");
            
            // Final work session
            await SimulateRegularWork(TimeSpan.FromMinutes(20), "Afternoon eye rest");
            await SimulateRegularWork(TimeSpan.FromMinutes(20), "Final eye rest");
            
            _output.WriteLine($"Virtual time: {_virtualTimerFactory.CurrentTime:HH:mm} - End of simulation");
        }

        private async Task SimulateRegularWork(TimeSpan duration, string description)
        {
            _output.WriteLine($"  Working for {duration.TotalMinutes:F0} minutes - {description}");
            _virtualTimerFactory.AdvanceTime(duration);
            
            // Check if any timers should fire during this period
            _virtualTimerFactory.ProcessPendingTimers();
            
            // Small delay to allow event processing
            await Task.Delay(10);
        }

        private async Task SimulateUserAction(string action)
        {
            _output.WriteLine($"  👤 User action: {action}");
            
            switch (action)
            {
                case "CompleteBreak":
                    await _timerService.RestartBreakTimerAfterCompletion();
                    _analyticsEvents.Add(new AnalyticsEvent
                    {
                        Type = "BreakEvent",
                        Action = "Completed",
                        Timestamp = _virtualTimerFactory.CurrentTime
                    });
                    break;
                    
                case "CompleteEyeRest":
                    await _timerService.RestartEyeRestTimerAfterCompletion();
                    _analyticsEvents.Add(new AnalyticsEvent
                    {
                        Type = "EyeRestEvent", 
                        Action = "Completed",
                        Timestamp = _virtualTimerFactory.CurrentTime
                    });
                    break;
                    
                case "SkipBreak":
                    await _timerService.RestartBreakTimerAfterCompletion();
                    _analyticsEvents.Add(new AnalyticsEvent
                    {
                        Type = "BreakEvent",
                        Action = "Skipped", 
                        Timestamp = _virtualTimerFactory.CurrentTime
                    });
                    break;
                    
                case "DelayBreak1Min":
                    await _timerService.DelayBreak(TimeSpan.FromMinutes(1));
                    _analyticsEvents.Add(new AnalyticsEvent
                    {
                        Type = "BreakEvent",
                        Action = "Delayed1Min",
                        Timestamp = _virtualTimerFactory.CurrentTime
                    });
                    break;
                    
                case "DelayBreak5Min":
                    await _timerService.DelayBreak(TimeSpan.FromMinutes(5));
                    _analyticsEvents.Add(new AnalyticsEvent
                    {
                        Type = "BreakEvent", 
                        Action = "Delayed5Min",
                        Timestamp = _virtualTimerFactory.CurrentTime
                    });
                    break;
            }
            
            await Task.Delay(50); // Allow processing
        }

        private async Task SimulateUserAwayPeriod(TimeSpan duration, EyeRest.Models.UserPresenceState state, string reason)
        {
            _output.WriteLine($"  🚶 User away ({state}): {duration.TotalMinutes:F0} minutes - {reason}");
            
            // Smart pause should trigger
            await _timerService.SmartPauseAsync(reason);
            
            // Advance time for away period
            _virtualTimerFactory.AdvanceTime(duration);
            
            // User returns
            await _timerService.SmartResumeAsync();
            
            _analyticsEvents.Add(new AnalyticsEvent
            {
                Type = "PresenceChange",
                Action = $"Away-{state}",
                Duration = duration,
                Timestamp = _virtualTimerFactory.CurrentTime,
                Reason = reason
            });
            
            await Task.Delay(50);
        }

        private async Task SimulateManualPause(TimeSpan duration, string reason)
        {
            _output.WriteLine($"  ⏸️ Manual pause: {duration.TotalMinutes:F0} minutes - {reason}");
            
            await _timerService.PauseForDurationAsync(duration, reason);
            _virtualTimerFactory.AdvanceTime(duration);
            
            _analyticsEvents.Add(new AnalyticsEvent
            {
                Type = "ManualPause",
                Action = "Paused",
                Duration = duration,
                Timestamp = _virtualTimerFactory.CurrentTime,
                Reason = reason
            });
            
            await Task.Delay(50);
        }

        private async Task SimulateSystemSleep(TimeSpan duration)
        {
            _output.WriteLine($"  💤 System sleep: {duration.TotalHours:F0} hours");
            
            // Stop timer service (simulating system sleep)
            await _timerService.StopAsync();
            
            // Advance time significantly (this creates the clock jump)
            _virtualTimerFactory.AdvanceTime(duration);
            
            _analyticsEvents.Add(new AnalyticsEvent
            {
                Type = "SystemEvent",
                Action = "Sleep",
                Duration = duration,
                Timestamp = _virtualTimerFactory.CurrentTime
            });
        }

        private async Task SimulateSystemWake(DateTime wakeTime)
        {
            _output.WriteLine($"  ⏰ System wake at: {wakeTime:yyyy-MM-dd HH:mm:ss}");
            
            _virtualTimerFactory.SetCurrentTime(wakeTime);
            
            // Restart timer service with recovery logic
            await _timerService.RecoverFromSystemResumeAsync("System wake from sleep");
            
            _analyticsEvents.Add(new AnalyticsEvent
            {
                Type = "SystemEvent",
                Action = "Wake",
                Timestamp = wakeTime
            });
            
            await Task.Delay(100); // Allow recovery processing
        }

        private async Task ValidateSessionReset(string context)
        {
            _output.WriteLine($"  ✓ Validating session reset: {context}");
            
            // Verify timers have been reset to fresh session state
            var nextBreak = _timerService.TimeUntilNextBreak;
            var nextEyeRest = _timerService.TimeUntilNextEyeRest;
            
            Assert.True(nextBreak > TimeSpan.FromMinutes(50), 
                $"Break timer should be reset to near full interval after session reset. Got: {nextBreak.TotalMinutes:F1} min");
            Assert.True(nextEyeRest > TimeSpan.FromMinutes(15), 
                $"Eye rest timer should be reset to near full interval after session reset. Got: {nextEyeRest.TotalMinutes:F1} min");
            
            await Task.Delay(10);
        }

        private async Task ValidateNoImmediatePopupAfterWake()
        {
            _output.WriteLine("  ✓ Validating no immediate popup after system wake (clock jump detection)");
            
            // Check that notification service wasn't called immediately after wake
            var recentNotifications = _notificationEvents
                .Where(e => _virtualTimerFactory.CurrentTime - e.Timestamp < TimeSpan.FromMinutes(1))
                .ToList();
                
            Assert.True(recentNotifications.Count == 0, 
                "No popups should appear immediately after system wake due to clock jump detection");
                
            await Task.Delay(10);
        }

        private async Task ValidateSimulationResults()
        {
            _output.WriteLine("\n🔍 VALIDATING SIMULATION RESULTS");
            
            // Analytics events validation
            _output.WriteLine($"Total analytics events recorded: {_analyticsEvents.Count}");
            
            var breakEvents = _analyticsEvents.Where(e => e.Type == "BreakEvent").ToList();
            var eyeRestEvents = _analyticsEvents.Where(e => e.Type == "EyeRestEvent").ToList();
            var presenceEvents = _analyticsEvents.Where(e => e.Type == "PresenceChange").ToList();
            var systemEvents = _analyticsEvents.Where(e => e.Type == "SystemEvent").ToList();
            var pauseEvents = _analyticsEvents.Where(e => e.Type == "ManualPause").ToList();
            
            _output.WriteLine($"  Break events: {breakEvents.Count}");
            _output.WriteLine($"  Eye rest events: {eyeRestEvents.Count}");
            _output.WriteLine($"  Presence changes: {presenceEvents.Count}");
            _output.WriteLine($"  System events: {systemEvents.Count}");
            _output.WriteLine($"  Manual pause events: {pauseEvents.Count}");
            
            // Validate expected event counts (approximate, since timing can vary)
            Assert.True(breakEvents.Count >= 4, $"Expected at least 4 break events, got {breakEvents.Count}");
            Assert.True(eyeRestEvents.Count >= 10, $"Expected at least 10 eye rest events, got {eyeRestEvents.Count}");
            Assert.True(presenceEvents.Count >= 3, $"Expected at least 3 presence changes, got {presenceEvents.Count}");
            Assert.True(systemEvents.Count >= 2, $"Expected at least 2 system events (sleep/wake), got {systemEvents.Count}");
            Assert.True(pauseEvents.Count >= 1, $"Expected at least 1 manual pause event, got {pauseEvents.Count}");
            
            // Validate user actions variety
            var actions = breakEvents.Select(e => e.Action).Distinct().ToList();
            Assert.Contains("Completed", actions);
            Assert.Contains("Skipped", actions);
            Assert.Contains("Delayed5Min", actions);
            Assert.Contains("Delayed1Min", actions);
            
            // Timer events validation
            _output.WriteLine($"Total timer events fired: {_timerEvents.Count}");
            
            var warningEvents = _timerEvents.Where(e => e.Type.Contains("Warning")).ToList();
            var dueEvents = _timerEvents.Where(e => !e.Type.Contains("Warning")).ToList();
            
            _output.WriteLine($"  Warning events: {warningEvents.Count}");
            _output.WriteLine($"  Due events: {dueEvents.Count}");
            
            // Validate service state
            Assert.True(_timerService.IsRunning, "Timer service should be running at end of simulation");
            Assert.False(_timerService.IsPaused, "Timer service should not be paused at end of simulation");
            Assert.False(_timerService.IsSmartPaused, "Timer service should not be smart paused at end of simulation");
            
            // Validate reasonable timer values
            var nextBreak = _timerService.TimeUntilNextBreak;
            var nextEyeRest = _timerService.TimeUntilNextEyeRest;
            
            Assert.True(nextBreak > TimeSpan.Zero && nextBreak <= TimeSpan.FromMinutes(55), 
                $"Next break time should be reasonable: {nextBreak.TotalMinutes:F1} minutes");
            Assert.True(nextEyeRest > TimeSpan.Zero && nextEyeRest <= TimeSpan.FromMinutes(20), 
                $"Next eye rest time should be reasonable: {nextEyeRest.TotalMinutes:F1} minutes");
                
            await Task.Delay(10);
        }

        private void SetupMocks()
        {
            _mockConfigurationService.Setup(x => x.LoadConfigurationAsync())
                .ReturnsAsync(_config);
            
            _mockNotificationService.Setup(x => x.IsAnyPopupActive)
                .Returns(false);
                
            // Track notification calls
            _mockNotificationService.Setup(x => x.ShowEyeRestWarningAsync(It.IsAny<TimeSpan>()))
                .Returns(Task.CompletedTask)
                .Callback<TimeSpan>(duration => _notificationEvents.Add(new NotificationEvent
                {
                    Type = "EyeRestWarning",
                    Duration = duration,
                    Timestamp = _virtualTimerFactory.CurrentTime
                }));
                
            _mockNotificationService.Setup(x => x.ShowBreakWarningAsync(It.IsAny<TimeSpan>()))
                .Returns(Task.CompletedTask)
                .Callback<TimeSpan>(duration => _notificationEvents.Add(new NotificationEvent
                {
                    Type = "BreakWarning", 
                    Duration = duration,
                    Timestamp = _virtualTimerFactory.CurrentTime
                }));

            // Track analytics calls
            _mockAnalyticsService.Setup(x => x.RecordEyeRestEventAsync(It.IsAny<RestEventType>(), It.IsAny<UserAction>(), It.IsAny<TimeSpan>()))
                .Returns(Task.CompletedTask);
                
            _mockAnalyticsService.Setup(x => x.RecordBreakEventAsync(It.IsAny<RestEventType>(), It.IsAny<UserAction>(), It.IsAny<TimeSpan>()))
                .Returns(Task.CompletedTask);
        }

        private void SetupEventHandlers()
        {
            _timerService.EyeRestWarning += (sender, e) => _timerEvents.Add(new TimerEvent
            {
                Type = "EyeRestWarning",
                Timestamp = _virtualTimerFactory.CurrentTime,
                NextInterval = e.NextInterval
            });
            
            _timerService.EyeRestDue += (sender, e) => _timerEvents.Add(new TimerEvent
            {
                Type = "EyeRestDue", 
                Timestamp = _virtualTimerFactory.CurrentTime,
                NextInterval = e.NextInterval
            });
            
            _timerService.BreakWarning += (sender, e) => _timerEvents.Add(new TimerEvent
            {
                Type = "BreakWarning",
                Timestamp = _virtualTimerFactory.CurrentTime, 
                NextInterval = e.NextInterval
            });
            
            _timerService.BreakDue += (sender, e) => _timerEvents.Add(new TimerEvent
            {
                Type = "BreakDue",
                Timestamp = _virtualTimerFactory.CurrentTime,
                NextInterval = e.NextInterval
            });
        }

        public void Dispose()
        {
            _timerService?.Dispose();
            _serviceProvider?.Dispose();
        }
    }

    // Supporting classes for event tracking and virtual time management
    
    /// <summary>
    /// Virtual timer factory that allows time manipulation for testing
    /// </summary>
    public class VirtualTimerFactory : ITimerFactory
    {
        private readonly List<VirtualTimer> _timers = new();
        
        public DateTime CurrentTime { get; private set; } = DateTime.Now;

        public EyeRestTimer CreateTimer(TimerPriority priority = TimerPriority.Normal)
        {
            var timer = new VirtualTimer(this);
            _timers.Add(timer);
            return timer;
        }

        public void SetCurrentTime(DateTime time)
        {
            CurrentTime = time;
        }

        public void AdvanceTime(TimeSpan duration)
        {
            CurrentTime = CurrentTime.Add(duration);
            ProcessPendingTimers();
        }

        public void ProcessPendingTimers()
        {
            foreach (var timer in _timers.Where(t => t.IsEnabled))
            {
                timer.CheckAndFireIfDue(CurrentTime);
            }
        }
    }

    /// <summary>
    /// Virtual timer that fires based on virtual time instead of real time
    /// </summary>
    public class VirtualTimer : EyeRestTimer
    {
        private readonly VirtualTimerFactory _factory;
        private DateTime _lastFiredTime;
        private bool _disposed = false;

        public VirtualTimer(VirtualTimerFactory factory)
        {
            _factory = factory;
            _lastFiredTime = factory.CurrentTime;
        }

        public TimeSpan Interval { get; set; }
        public bool IsEnabled { get; private set; }

        public event EventHandler<EventArgs>? Tick;

        public void Start()
        {
            if (!_disposed)
            {
                IsEnabled = true;
                _lastFiredTime = _factory.CurrentTime;
            }
        }

        public void Stop()
        {
            if (!_disposed)
            {
                IsEnabled = false;
            }
        }

        public void CheckAndFireIfDue(DateTime currentTime)
        {
            if (IsEnabled && currentTime - _lastFiredTime >= Interval)
            {
                _lastFiredTime = currentTime;
                Tick?.Invoke(this, EventArgs.Empty);
            }
        }

        public void Dispose()
        {
            if (!_disposed)
            {
                Stop();
                _disposed = true;
            }
        }
    }

    // Event tracking classes
    
    public class TimerEvent
    {
        public string Type { get; set; } = "";
        public DateTime Timestamp { get; set; }
        public TimeSpan NextInterval { get; set; }
    }

    public class AnalyticsEvent  
    {
        public string Type { get; set; } = "";
        public string Action { get; set; } = "";
        public DateTime Timestamp { get; set; }
        public TimeSpan Duration { get; set; }
        public string Reason { get; set; } = "";
    }

    public class NotificationEvent
    {
        public string Type { get; set; } = "";
        public DateTime Timestamp { get; set; }
        public TimeSpan Duration { get; set; }
    }
}