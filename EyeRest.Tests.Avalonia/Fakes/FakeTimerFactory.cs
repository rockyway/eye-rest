using System.Collections.Generic;
using EyeRest.Services.Abstractions;
using ITimer = EyeRest.Services.Abstractions.ITimer;

namespace EyeRest.Tests.Avalonia.Fakes
{
    /// <summary>
    /// Fake timer factory for testing that tracks all created timers,
    /// allowing tests to access and control them via FireTick().
    /// </summary>
    public class FakeTimerFactory : ITimerFactory
    {
        private readonly List<FakeTimer> _createdTimers = new();

        public ITimer CreateTimer(TimerPriority priority = TimerPriority.Normal)
        {
            var timer = new FakeTimer();
            _createdTimers.Add(timer);
            return timer;
        }

        /// <summary>
        /// Returns all timers created by this factory in creation order.
        /// After StartAsync, order is: [0] eyeRest, [1] break, [2] eyeRestWarning,
        /// [3] breakWarning, [4] healthMonitor
        /// </summary>
        public List<FakeTimer> GetCreatedTimers() => _createdTimers;

        public void Reset() => _createdTimers.Clear();
    }
}
