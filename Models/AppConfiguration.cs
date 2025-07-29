namespace EyeRest.Models
{
    public class AppConfiguration
    {
        public EyeRestSettings EyeRest { get; set; } = new();
        public BreakSettings Break { get; set; } = new();
        public AudioSettings Audio { get; set; } = new();
        public ApplicationSettings Application { get; set; } = new();
    }

    public class EyeRestSettings
    {
        public int IntervalMinutes { get; set; } = 20;
        public int DurationSeconds { get; set; } = 20;
        public bool StartSoundEnabled { get; set; } = true;
        public bool EndSoundEnabled { get; set; } = true;
        public bool WarningEnabled { get; set; } = true;
        public int WarningSeconds { get; set; } = 30;
    }

    public class BreakSettings
    {
        public int IntervalMinutes { get; set; } = 55;  // FIXED: Correct PRD default (55 minutes)
        public int DurationMinutes { get; set; } = 5;   // FIXED: Correct PRD default (5 minutes)
        public bool WarningEnabled { get; set; } = true;
        public int WarningSeconds { get; set; } = 30;
        public int OverlayOpacityPercent { get; set; } = 50; // Screen overlay opacity (0-100%)
    }

    public class AudioSettings
    {
        public bool Enabled { get; set; } = true;
        public string? CustomSoundPath { get; set; }
        public int Volume { get; set; } = 50;
    }

    public class ApplicationSettings
    {
        public bool StartWithWindows { get; set; } = false;
        public bool MinimizeToTray { get; set; } = true;
        public bool ShowInTaskbar { get; set; } = false;
    }
}