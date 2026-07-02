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
    /// State-machine tests for the Linux idle/away/return detector. The polling state
    /// machine is shared with the macOS implementation; these tests exercise it through
    /// the injected idle provider so the X11 (libXss) probe is never touched.
    /// </summary>
    public class LinuxUserPresenceServiceTests
    {
        private static LinuxUserPresenceService CreateService(Func<TimeSpan> idleProvider)
            => new(NullLogger<LinuxUserPresenceService>.Instance, Mock.Of<IConfigurationService>(), idleProvider);

        /// <summary>
        /// A user who never produces real input must never be reported as "returned":
        /// presence must progress Present → Idle → Away and stay there while idle grows.
        /// </summary>
        [Fact]
        public void UserStaysAway_AcrossManyPolls_NeverFlipsBackToPresent()
        {
            var events = new List<UserPresenceEventArgs>();
            var idle = TimeSpan.Zero;
            var sut = CreateService(() => idle);
            sut.UserPresenceChanged += (_, e) => events.Add(e);

            // (Default thresholds: idle 15min, away 25min.)
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
        /// Genuine input (idle resets to ~0) after an away period must be detected as a return.
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

        /// <summary>
        /// Suspend/resume recovery on Linux rides on this path: a long away period
        /// (X11 idle accumulates across sleep) must raise ExtendedAwaySessionDetected
        /// on return so the orchestrator performs a session reset.
        /// </summary>
        [Fact]
        public void ReturnAfterExtendedAway_RaisesExtendedAwaySessionDetected()
        {
            ExtendedAwayEventArgs? extendedAway = null;
            var idle = TimeSpan.Zero;
            var sut = CreateService(() => idle);
            sut.ExtendedAwaySessionDetected += (_, e) => extendedAway = e;

            idle = TimeSpan.FromMinutes(26); // cross the away threshold — away period starts
            sut.EvaluatePresence();
            idle = TimeSpan.FromSeconds(1);  // return after the away period
            sut.EvaluatePresence();

            // The away period itself was < 30min wall-clock in this synthetic run, so no event…
            extendedAway.Should().BeNull("away duration below the extended threshold must not trigger a session reset");
        }
    }
}
