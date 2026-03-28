namespace EyeRest.Models
{
    public enum RestEventType
    {
        EyeRest,
        Break
    }

    public enum UserAction
    {
        Completed,
        Skipped,
        Delayed1Min,
        Delayed5Min,
        Closed
    }

    public enum UserPresenceState
    {
        Present,
        Away,
        Idle,
        Locked,
        Unknown
    }

    public enum EventHistoryType
    {
        EyeRestWarning,
        EyeRestShown,
        EyeRestCompleted,
        BreakWarning,
        BreakShown,
        BreakCompleted,
        BreakSkipped,
        BreakDelayed,
        Paused,
        Resumed,
        UserIdle,
        UserReturned,
        MeetingModeOn,
        MeetingModeOff,
        SettingsChanged,
        SessionReset
    }
}