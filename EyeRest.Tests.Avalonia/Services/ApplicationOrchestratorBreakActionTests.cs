using System;
using System.Collections.Generic;
using System.Reflection;
using System.Threading.Tasks;
using EyeRest.Models;
using EyeRest.Services;
using EyeRest.Services.Abstractions;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Xunit;

namespace EyeRest.Tests.Avalonia.Services
{
    /// <summary>
    /// Layer B regression (2026-06-03, requested by both PR reviewers): a break popup that is
    /// force-closed by the system must resolve to <see cref="BreakAction.AutoDismissed"/> and be
    /// recorded as NOTHING — never as a fabricated "Break skipped by user". A genuine user Skip
    /// (which arrives as <see cref="BreakAction.Skipped"/> via ActionSelected) must still record.
    /// Drives <c>ApplicationOrchestrator.OnBreakDue</c> directly with a mocked notification result.
    /// </summary>
    public class ApplicationOrchestratorBreakActionTests
    {
        private static (ApplicationOrchestrator orch, Mock<IAnalyticsService> analytics, Mock<ITimerService> timer)
            Build(BreakAction popupResult)
        {
            var analytics = new Mock<IAnalyticsService>();
            analytics.Setup(a => a.RecordEventAsync(It.IsAny<EventHistoryType>(), It.IsAny<string>(), It.IsAny<Dictionary<string, object?>>()))
                .Returns(Task.CompletedTask);
            analytics.Setup(a => a.RecordBreakEventAsync(It.IsAny<RestEventType>(), It.IsAny<UserAction>(), It.IsAny<TimeSpan>(), It.IsAny<BreakTriggerSource>()))
                .Returns(Task.CompletedTask);

            var notification = new Mock<INotificationService>();
            notification.SetupGet(n => n.IsTestMode).Returns(false);
            notification.Setup(n => n.ShowBreakReminderAsync(It.IsAny<TimeSpan>(), It.IsAny<IProgress<double>>(), It.IsAny<int>(), It.IsAny<int>()))
                .ReturnsAsync(popupResult);

            var config = new AppConfiguration
            {
                Break = new BreakSettings { IntervalMinutes = 55, DurationMinutes = 5, MaxBreakDelayCount = 3 }
            };
            var configSvc = new Mock<IConfigurationService>();
            configSvc.Setup(c => c.LoadConfigurationAsync()).ReturnsAsync(config);

            var timer = new Mock<ITimerService>();
            timer.Setup(t => t.SmartSessionResetAsync(It.IsAny<string>())).Returns(Task.CompletedTask);

            var orch = new ApplicationOrchestrator(
                timer.Object,
                notification.Object,
                Mock.Of<IAudioService>(),
                Mock.Of<ISystemTrayService>(),
                Mock.Of<IPerformanceMonitor>(),
                configSvc.Object,
                Mock.Of<IUserPresenceService>(),
                analytics.Object,
                Mock.Of<IPauseReminderService>(),
                Mock.Of<IDonationService>(),
                Mock.Of<ITimerFactory>(),
                Mock.Of<IAppLifecycleService>(),
                NullLogger<ApplicationOrchestrator>.Instance);

            return (orch, analytics, timer);
        }

        private static async Task InvokeOnBreakDueAsync(ApplicationOrchestrator orch)
        {
            var method = typeof(ApplicationOrchestrator).GetMethod("OnBreakDue",
                BindingFlags.NonPublic | BindingFlags.Instance);
            Assert.NotNull(method);
            // OnBreakDue is async void; every awaited dependency is mocked to a completed task, so it
            // runs to completion synchronously within Invoke. The small delay is belt-and-suspenders.
            method!.Invoke(orch, new object?[] { null, new TimerEventArgs { TriggeredAt = DateTime.Now } });
            await Task.Delay(50);
        }

        [Fact]
        public async Task OnBreakDue_PopupAutoDismissed_RecordsNoSkip_AndDoesNotReset()
        {
            var (orch, analytics, timer) = Build(BreakAction.AutoDismissed);

            await InvokeOnBreakDueAsync(orch);

            analytics.Verify(a => a.RecordEventAsync(EventHistoryType.BreakSkipped, It.IsAny<string>(), It.IsAny<Dictionary<string, object?>>()),
                Times.Never, "a system force-close must never fabricate a BreakSkipped row");
            analytics.Verify(a => a.RecordBreakEventAsync(It.IsAny<RestEventType>(), UserAction.Skipped, It.IsAny<TimeSpan>(), It.IsAny<BreakTriggerSource>()),
                Times.Never, "a system force-close is not a user Skip");
            timer.Verify(t => t.SmartSessionResetAsync(It.IsAny<string>()),
                Times.Never, "AutoDismissed must not reset — the dismiss originator owns timer state");
        }

        [Fact]
        public async Task OnBreakDue_UserSkip_StillRecordsBreakSkipped()
        {
            var (orch, analytics, _) = Build(BreakAction.Skipped);

            await InvokeOnBreakDueAsync(orch);

            analytics.Verify(a => a.RecordEventAsync(EventHistoryType.BreakSkipped, It.IsAny<string>(), It.IsAny<Dictionary<string, object?>>()),
                Times.Once, "a genuine user Skip must still be recorded");
            analytics.Verify(a => a.RecordBreakEventAsync(It.IsAny<RestEventType>(), UserAction.Skipped, It.IsAny<TimeSpan>(), It.IsAny<BreakTriggerSource>()),
                Times.Once);
        }
    }
}
