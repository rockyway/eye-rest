using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using EyeRest.Models;

namespace EyeRest.Services
{
    public interface IMeetingDetectionService : IDisposable
    {
        event EventHandler<MeetingStateEventArgs> MeetingStateChanged;
        
        bool IsMeetingActive { get; }
        IReadOnlyList<MeetingApplication> ActiveMeetings { get; }
        List<MeetingApplication> DetectedMeetings { get; }
        MeetingDetectionSettings Settings { get; set; }
        
        Task StartMonitoringAsync();
        Task StopMonitoringAsync();
        Task RefreshMeetingStateAsync();
    }

    public class MeetingStateEventArgs : EventArgs
    {
        public bool IsMeetingActive { get; set; }
        public List<MeetingApplication> ActiveMeetings { get; set; } = new List<MeetingApplication>();
        public DateTime StateChangedAt { get; set; }
        public string Reason { get; set; } = string.Empty;
    }
}