using System;
using System.Threading.Tasks;

namespace EyeRest.Services
{
    public interface IUserPresenceService : IDisposable
    {
        event EventHandler<UserPresenceEventArgs> UserPresenceChanged;
        
        bool IsUserPresent { get; }
        TimeSpan IdleTime { get; }
        UserPresenceState CurrentState { get; }
        
        Task StartMonitoringAsync();
        Task StopMonitoringAsync();
    }

    public class UserPresenceEventArgs : EventArgs
    {
        public UserPresenceState PreviousState { get; set; }
        public UserPresenceState CurrentState { get; set; }
        public DateTime StateChangedAt { get; set; }
        public TimeSpan IdleDuration { get; set; }
    }

    public enum UserPresenceState
    {
        Present,        // User actively using computer
        Idle,          // User inactive but session unlocked  
        Away,          // Session locked or monitor off
        SystemSleep    // System in sleep/hibernate mode
    }
}