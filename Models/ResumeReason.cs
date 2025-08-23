namespace EyeRest.Services
{
    public enum ResumeReason
    {
        Manual,
        SmartDetection,
        UserReturned,
        MeetingEnded,
        AutoResumeAfterDuration, // NEW: For timed manual pause auto-resume
        NewWorkingSession // NEW: For smart session reset after extended away
    }
}