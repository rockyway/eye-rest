using System;
using System.Collections.Generic;
using System.Linq;
using EyeRest.Services.Abstractions;
using ITimer = EyeRest.Services.Abstractions.ITimer;

namespace EyeRest.Tests.Fakes
{
    /// <summary>
    /// Fake timer factory for testing that creates controllable fake timers
    /// Enhanced to support extended idle recovery testing scenarios
    /// </summary>
    public class FakeTimerFactory : ITimerFactory
    {
        private readonly List<FakeTimer> _createdTimers = new();
        private DateTime _currentTime = DateTime.Now;

        /// <summary>
        /// Gets all timers created by this factory for test inspection
        /// </summary>
        public IReadOnlyList<FakeTimer> CreatedTimers => _createdTimers.AsReadOnly();

        /// <summary>
        /// Gets all timers created by this factory as a List for test control
        /// </summary>
        public List<FakeTimer> GetCreatedTimers() => new(_createdTimers);

        public ITimer CreateTimer(TimerPriority priority = TimerPriority.Normal)
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

        /// <summary>
        /// Advances the simulated time and potentially triggers timer events
        /// This simulates time passing for extended idle scenario testing
        /// </summary>
        /// <param name="timeSpan">Amount of time to advance</param>
        public void AdvanceTime(TimeSpan timeSpan)
        {
            _currentTime = _currentTime.Add(timeSpan);
            
            // For simple simulation, fire ticks on enabled timers
            // In real scenario, this would be more sophisticated
            foreach (var timer in _createdTimers.Where(t => t.IsEnabled))
            {
                timer.AdvanceTime(timeSpan);
            }
        }

        /// <summary>
        /// Disables all timers (simulates the bug where timers become disabled after extended idle)
        /// </summary>
        public void DisableAllTimers()
        {
            foreach (var timer in _createdTimers)
            {
                timer.ForceDisable();
            }
        }

        /// <summary>
        /// Nullifies all timers (simulates extreme case where timer objects are lost)
        /// </summary>
        public void NullifyAllTimers()
        {
            // This is handled by the TimerService checking for null timers
            // We simulate by clearing the list which makes timers inaccessible
            _createdTimers.Clear();
        }

        /// <summary>
        /// Gets the health monitor timer for testing health check scenarios
        /// </summary>
        /// <returns>The first timer that could be the health monitor timer, or null</returns>
        public FakeTimer? GetHealthMonitorTimer()
        {
            // Return the last created timer as it's likely the health monitor
            return _createdTimers.LastOrDefault();
        }
    }
}