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
}