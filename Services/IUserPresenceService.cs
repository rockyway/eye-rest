using System;
using System.Threading.Tasks;

namespace EyeRest.Services
{
    public interface IUserPresenceService : IDisposable
    {
        event EventHandler<UserPresenceEventArgs> UserPresenceChanged;
        event EventHandler<ExtendedAwayEventArgs> ExtendedAwaySessionDetected; // NEW: For smart session reset
        
        bool IsUserPresent { get; }
        TimeSpan IdleTime { get; }
        UserPresenceState CurrentState { get; }
        TimeSpan TotalAwayTime { get; } // NEW: Track total time away

        Task StartMonitoringAsync();
        Task StopMonitoringAsync();

        // NEW: Timer recovery integration
        void SetTimerService(ITimerService timerService);

        /// <summary>
        /// Get the duration of the last away period (for extended idle detection)
        /// Returns TimeSpan.Zero if user was not away or data not available
        /// </summary>
        TimeSpan GetLastAwayDuration();
    }

    public class UserPresenceEventArgs : EventArgs
    {
        public UserPresenceState PreviousState { get; set; }
        public UserPresenceState CurrentState { get; set; }
        public DateTime StateChangedAt { get; set; }
        public TimeSpan IdleDuration { get; set; }
    }

    public class ExtendedAwayEventArgs : EventArgs
    {
        public TimeSpan TotalAwayTime { get; set; }
        public DateTime AwayStartTime { get; set; }
        public DateTime ReturnTime { get; set; }
        public UserPresenceState AwayState { get; set; } // Away, SystemSleep, etc.
    }

    public enum UserPresenceState
    {
        Present,        // User actively using computer
        Idle,          // User inactive but session unlocked  
        Away,          // Session locked or monitor off
        SystemSleep    // System in sleep/hibernate mode
    }
}