using System.Collections.Generic;
using System.Windows.Threading;
using EyeRest.Services.Abstractions;
using ITimer = EyeRest.Services.Abstractions.ITimer;

namespace EyeRest.Tests.Fakes
{
    /// <summary>
    /// Fake timer factory for testing that creates controllable fake timers
    /// </summary>
    public class FakeTimerFactory : ITimerFactory
    {
        private readonly List<FakeTimer> _createdTimers = new();

        /// <summary>
        /// Gets all timers created by this factory for test inspection
        /// </summary>
        public IReadOnlyList<FakeTimer> CreatedTimers => _createdTimers.AsReadOnly();

        /// <summary>
        /// Gets all timers created by this factory as a List for test control
        /// </summary>
        public List<FakeTimer> GetCreatedTimers() => new(_createdTimers);

        public ITimer CreateTimer(DispatcherPriority priority = DispatcherPriority.Normal)
        {
            var fakeTimer = new FakeTimer();
            _createdTimers.Add(fakeTimer);
            return fakeTimer;
        }

        /// <summary>
        /// Fires tick events on all enabled timers created by this factory
        /// </summary>
        public void FireAllTimers()
        {
            foreach (var timer in _createdTimers)
            {
                if (timer.IsEnabled)
                {
                    timer.FireTick();
                }
            }
        }

        /// <summary>
        /// Fires multiple tick events on all enabled timers
        /// </summary>
        /// <param name="count">Number of ticks to fire on each timer</param>
        public void FireAllTimers(int count)
        {
            foreach (var timer in _createdTimers)
            {
                if (timer.IsEnabled)
                {
                    timer.FireTicks(count);
                }
            }
        }

        /// <summary>
        /// Clears the list of created timers (for test cleanup)
        /// </summary>
        public void Clear()
        {
            foreach (var timer in _createdTimers)
            {
                timer.Dispose();
            }
            _createdTimers.Clear();
        }

        /// <summary>
        /// Resets the factory by disposing and clearing all created timers
        /// </summary>
        public void Reset()
        {
            Clear();
        }
    }
}