using System;
using System.Threading.Tasks;

namespace EyeRest.Services
{
    public interface ITimerService : System.ComponentModel.INotifyPropertyChanged
    {
        event EventHandler<TimerEventArgs> EyeRestWarning;
        event EventHandler<TimerEventArgs> EyeRestDue;
        event EventHandler<TimerEventArgs> BreakWarning;
        event EventHandler<TimerEventArgs> BreakDue;
        
        bool IsRunning { get; }
        TimeSpan TimeUntilNextEyeRest { get; }
        TimeSpan TimeUntilNextBreak { get; }
        string NextEventDescription { get; }
        bool IsBreakDelayed { get; }
        TimeSpan DelayRemaining { get; }
        
        Task StartAsync();
        Task StopAsync();
        Task ResetEyeRestTimer();
        Task ResetBreakTimer();
        Task DelayBreak(TimeSpan delay);
        Task RestartEyeRestTimerAfterCompletion();
        Task RestartBreakTimerAfterCompletion();
    }

    public class TimerEventArgs : EventArgs
    {
        public DateTime TriggeredAt { get; set; }
        public TimeSpan NextInterval { get; set; }
        public TimerType Type { get; set; }
    }

    public enum TimerType
    {
        EyeRestWarning,
        EyeRest,
        BreakWarning,
        Break
    }
}