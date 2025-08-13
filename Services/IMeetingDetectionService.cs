using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace EyeRest.Services
{
    public interface IMeetingDetectionService : IDisposable
    {
        event EventHandler<MeetingStateEventArgs> MeetingStateChanged;
        
        bool IsMeetingActive { get; }
        IReadOnlyList<MeetingApplication> ActiveMeetings { get; }
        
        Task StartMonitoringAsync();
        Task StopMonitoringAsync();
    }

    public class MeetingStateEventArgs : EventArgs
    {
        public bool IsMeetingActive { get; set; }
        public List<MeetingApplication> ActiveMeetings { get; set; } = new List<MeetingApplication>();
        public DateTime StateChangedAt { get; set; }
        public string Reason { get; set; } = string.Empty;
    }
}