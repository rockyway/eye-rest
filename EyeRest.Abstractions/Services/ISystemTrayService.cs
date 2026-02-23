using System;

namespace EyeRest.Services
{
    public interface ISystemTrayService
    {
        void Initialize();
        void ShowTrayIcon();
        void HideTrayIcon();
        void UpdateTrayIcon(TrayIconState state);
        void ShowBalloonTip(string title, string text);
        void UpdateTimerStatus(string status);
        void UpdateTimerDetails(TimeSpan eyeRestRemaining, TimeSpan breakRemaining); // NEW: Update timer details for tooltip
        void SetMeetingMode(bool isInMeeting, string meetingType = "");
        event EventHandler RestoreRequested;
        event EventHandler ExitRequested;
        event EventHandler PauseTimersRequested;
        event EventHandler ResumeTimersRequested;
        event EventHandler PauseForMeetingRequested; // NEW: Manual pause for meeting
        event EventHandler ShowTimerStatusRequested;
        event EventHandler ShowAnalyticsRequested;
    }

    public enum TrayIconState
    {
        Active,
        Paused,
        SmartPaused,
        ManuallyPaused, // NEW: For manual meeting pause
        Break,
        EyeRest,
        MeetingMode,
        UserAway,
        Error
    }
}