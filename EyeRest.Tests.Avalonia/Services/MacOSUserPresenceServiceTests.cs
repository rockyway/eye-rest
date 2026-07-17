using System;
using System.Collections.Generic;
using System.Linq;
using EyeRest.Services; // UserPresenceState / UserPresenceEventArgs / IConfigurationService all live here
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Xunit;

namespace EyeRest.Tests.Avalonia.Services
{
    /// <summary>
    /// Regression tests for the macOS idle/away/return detector.
    ///
    /// Root cause (2026-06-02): <see cref="MacOSUserPresenceService"/> read idle time from the
    /// <c>kCGEventSourceStateCombinedSessionState</c> source, which is reset by non-HID session
    /// events — notably NotificationCenter display-wakes. While the user was physically away,
    /// notifications kept lighting up the display, resetting the reported idle time, so the app
    /// emitted phantom "user returned" events and ran full eye-rest cycles. The fix queries the
    /// HID-system source (hardware input only).
    /// </summary>
    public class MacOSUserPresenceServiceTests
    {
        private static MacOSUserPresenceService CreateService(Func<TimeSpan> idleProvider)
            => new(NullLogger<MacOSUserPresenceService>.Instance, Mock.Of<IConfigurationService>(), idleProvider);

        /// <summary>
        /// Guards the actual fix: the idle source must be the HID-system source (hardware input
        /// only), never the combined-session source that display-wakes can reset. This is the
        /// assertion that fails if anyone reverts the source constant.
        /// </summary>
        [Fact]
        public void IdleSource_IsHidHardwareOnly_NotCombinedSession()
        {
            // 1 == kCGEventSourceStateHIDSystemState (hardware input only)
            // 0 == kCGEventSourceStateCombinedSessionState (reset by display-wakes — the bug)
            MacOSUserPresenceService.IdleEventSourceStateId.Should().Be(1);
        }

        /// <summary>
        /// The core behavioral regression: a user who never produces real HID input must never be
        /// reported as "returned". With the HID source, idle climbs monotonically even when the
        /// display wakes for notifications — so presence must progress Present → Idle → Away and
        /// stay there.
        /// </summary>
        [Fact]
        public void UserStaysAway_AcrossManyPolls_NeverFlipsBackToPresent()
        {
            var events = new List<UserPresenceEventArgs>();
            var idle = TimeSpan.Zero;
            var sut = CreateService(() => idle);
            sut.UserPresenceChanged += (_, e) => events.Add(e);

            // Idle keeps growing — display-wakes/notifications are not hardware input, so the
            // HID idle counter never resets. (Default thresholds: idle 15min, away 25min.)
            foreach (var minutes in new[] { 16, 26, 31, 36, 45, 60, 75 })
            {
                idle = TimeSpan.FromMinutes(minutes);
                sut.EvaluatePresence();
            }

            events.Select(e => e.CurrentState).Should()
                .ContainInOrder(UserPresenceState.Idle, UserPresenceState.Away);
            events.Should().NotContain(
                e => e.CurrentState == UserPresenceState.Present,
                "a user with no keyboard/mouse activity must never be reported as returned");
        }

        /// <summary>
        /// Complements the regression test: genuine hardware input (idle resets to ~0) after an
        /// away period must still be detected as a real return — the fix must not suppress it.
        /// </summary>
        [Fact]
        public void RealInputAfterAway_IsDetectedAsReturn()
        {
            var events = new List<UserPresenceEventArgs>();
            var idle = TimeSpan.Zero;
            var sut = CreateService(() => idle);
            sut.UserPresenceChanged += (_, e) => events.Add(e);

            idle = TimeSpan.FromMinutes(30); // away
            sut.EvaluatePresence();
            idle = TimeSpan.FromSeconds(1);  // real key/mouse activity
            sut.EvaluatePresence();

            events.Select(e => e.CurrentState).Should()
                .ContainInOrder(UserPresenceState.Away, UserPresenceState.Present);
        }
    }
}
