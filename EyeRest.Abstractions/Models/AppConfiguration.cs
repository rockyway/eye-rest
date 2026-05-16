using System.Collections.Generic;
using System.ComponentModel;

namespace EyeRest.Models
{
    public class AppConfiguration : INotifyPropertyChanged
    {
        public EyeRestSettings EyeRest { get; set; } = new();
        public BreakSettings Break { get; set; } = new();
        public AudioSettings Audio { get; set; } = new();
        public ApplicationSettings Application { get; set; } = new();
        public UserPresenceSettings UserPresence { get; set; } = new();
        public MeetingDetectionSettings MeetingDetection { get; set; } = new();
        public AnalyticsSettings Analytics { get; set; } = new();
        public TimerControlSettings TimerControls { get; set; } = new();
        public DonationSettings Donation { get; set; } = new();
        public ConfigMetadata Meta { get; set; } = new();

        public event PropertyChangedEventHandler? PropertyChanged;
        
        protected virtual void OnPropertyChanged([System.Runtime.CompilerServices.CallerMemberName] string? propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }

    public class EyeRestSettings
    {
        public int IntervalMinutes { get; set; } = 20;
        public int DurationSeconds { get; set; } = 20;
        public AudioChannelConfig StartAudio { get; set; } = new();
        public AudioChannelConfig EndAudio { get; set; } = new();
        public bool WarningEnabled { get; set; } = true;
        public int WarningSeconds { get; set; } = 15;
    }

    public class BreakSettings
    {
        public int IntervalMinutes { get; set; } = 55;  // FIXED: Correct PRD default (55 minutes)
        public int DurationMinutes { get; set; } = 5;   // FIXED: Correct PRD default (5 minutes)
        public AudioChannelConfig StartAudio { get; set; } = new();
        public AudioChannelConfig EndAudio { get; set; } = new();
        public bool WarningEnabled { get; set; } = true;
        public int WarningSeconds { get; set; } = 30;
        public int OverlayOpacityPercent { get; set; } = 50; // Screen overlay opacity (0-100%)
        public bool RequireConfirmationAfterBreak { get; set; } = true; // Keep popup open until user confirms completion
        public bool ResetTimersOnBreakConfirmation { get; set; } = true; // Start fresh session after break confirmation
        public int MaxBreakDelayCount { get; set; } = 3; // Maximum consecutive delays before break is forced (0 = unlimited)
    }

    public class AudioSettings
    {
        public bool Enabled { get; set; } = true;
        public int Volume { get; set; } = 50;

        // BL-002 recent-items: shared across all 4 channels by field type. Capped at
        // 10 entries each, most-recent first, deduped. The recent-button flyout
        // beside each File/URL field reads from these lists.
        public List<string> RecentFilePaths { get; set; } = new();
        public List<string> RecentUrls { get; set; } = new();
    }

    public enum ThemeMode
    {
        Auto = 0,
        Light = 1,
        Dark = 2
    }

    public class ApplicationSettings
    {
        public bool StartWithWindows { get; set; } = false;
        public bool MinimizeToTray { get; set; } = true;
        public bool StartMinimized { get; set; } = false;
        public bool ShowTrayNotifications { get; set; } = true;
        public bool ShowInTaskbar { get; set; } = false;
        public ThemeMode ThemeMode { get; set; } = ThemeMode.Auto;

        /// <summary>
        /// Legacy property for backward compatibility with existing config files.
        /// </summary>
        [System.Text.Json.Serialization.JsonIgnore]
        public bool IsDarkMode
        {
            get => ThemeMode == ThemeMode.Dark;
            set => ThemeMode = value ? ThemeMode.Dark : ThemeMode.Light;
        }
    }

    public class UserPresenceSettings
    {
        public bool Enabled { get; set; } = true;
        public int IdleThresholdMinutes { get; set; } = 5;
        public int AwayGracePeriodSeconds { get; set; } = 30;
        public bool AutoPauseOnAway { get; set; } = true;
        public bool AutoResumeOnReturn { get; set; } = true;
        public bool MonitorSessionChanges { get; set; } = true;
        public bool MonitorPowerEvents { get; set; } = true;
        public int MonitoringIntervalSeconds { get; set; } = 15;
        
        // UI-specific presence settings
        public bool PauseOnScreenLock { get; set; } = true;
        public bool PauseOnMonitorOff { get; set; } = true;
        public bool PauseOnIdle { get; set; } = true;
        public int IdleTimeoutMinutes { get; set; } = 15;
        
        // NEW: Extended away period smart reset settings
        public bool EnableSmartSessionReset { get; set; } = true;
        public int ExtendedAwayThresholdMinutes { get; set; } = 30; // Reset timers after 30+ min away
        public bool ShowSessionResetNotification { get; set; } = true;
    }

    public class TimerControlSettings
    {
        public bool AllowManualPause { get; set; } = true;
        public bool ShowPauseReminders { get; set; } = true;
        public int PauseReminderIntervalHours { get; set; } = 1;
        public int MaxPauseHours { get; set; } = 8;
        public bool ShowPauseInSystemTray { get; set; } = true;
        public bool ConfirmManualPause { get; set; } = true;
        public bool PreserveTimerProgress { get; set; } = true;
    }

    public class AnalyticsSettings
    {
        public bool Enabled { get; set; } = true;
        public int DataRetentionDays { get; set; } = 90;
        public bool TrackBreakEvents { get; set; } = true;
        public bool TrackPresenceChanges { get; set; } = true;
        public bool TrackMeetingEvents { get; set; } = true;
        public bool TrackUserSessions { get; set; } = true;
        public bool AllowDataExport { get; set; } = true;
        public string ExportFormat { get; set; } = "JSON";
        public bool AutoCleanupOldData { get; set; } = true;
        public int DatabaseMaintenanceIntervalDays { get; set; } = 7;
        public bool AutoOpenDashboard { get; set; } = false;
    }

    public class MeetingDetectionSettings
    {
        public bool Enabled { get; set; } = true;
        
        /// <summary>
        /// Selected meeting detection method
        /// </summary>
        public MeetingDetectionMethod DetectionMethod { get; set; } = MeetingDetectionMethod.WindowBased;
        
        /// <summary>
        /// Enable fallback to alternative detection method if primary fails
        /// </summary>
        public bool EnableFallbackDetection { get; set; } = true;
        
        /// <summary>
        /// Enable detailed logging of detection activity for debugging
        /// </summary>
        public bool LogDetectionActivity { get; set; } = false;
        
        // Application-specific detection settings
        public bool EnableTeamsDetection { get; set; } = true;
        public bool EnableZoomDetection { get; set; } = true;
        public bool EnableWebexDetection { get; set; } = true;
        public bool EnableGoogleMeetDetection { get; set; } = true;
        public bool EnableSkypeDetection { get; set; } = true;
        
        // Timer control settings
        public bool AutoPauseTimers { get; set; } = true;
        public bool ShowMeetingModeIndicator { get; set; } = true;
        
        // Window-based detection settings
        public int WindowPollingIntervalSeconds { get; set; } = 10;
        public List<string> CustomProcessNames { get; set; } = new();
        public List<string> ExcludedWindowTitles { get; set; } = new();
        
        // Network-based detection settings
        public int NetworkPollingIntervalSeconds { get; set; } = 10;
        public bool IncludeIPv6Monitoring { get; set; } = true;
        public int MinimumUdpEndpointsForMeeting { get; set; } = 2;
        public int MeetingDetectionTimeoutSeconds { get; set; } = 30;
        public bool IncludePrivateNetworkAddresses { get; set; } = false;
        public List<string> ExcludedNetworkAddresses { get; set; } = new List<string>
        {
            "127.0.0.1", "::1", "0.0.0.0", "::"
        };
        public List<string> ExcludedPortRanges { get; set; } = new List<string>
        {
            "1-1023", "5353", "53"
        };
        
        /// <summary>
        /// Legacy property for backward compatibility - maps to WindowPollingIntervalSeconds
        /// </summary>
        public int MonitoringIntervalSeconds
        {
            get => WindowPollingIntervalSeconds;
            set => WindowPollingIntervalSeconds = value;
        }
    }

    public class ConfigMetadata
    {
        public string? LastSavedBy { get; set; }
        public string? LastSavedAt { get; set; }
        public string? AppVersion { get; set; }
        public string? BuildTimestamp { get; set; }
        public int SaveCount { get; set; }
        public int ProcessId { get; set; }
        public string? ExecutablePath { get; set; }

        // BL-002: config schema version. 1 = pre-BL002 (legacy bool toggles + global
        // CustomSoundPath). 2 = BL002 per-channel AudioChannelConfig. Defaults to 1 so
        // pre-existing configs without this field migrate on next load.
        public int SchemaVersion { get; set; } = 1;
    }
}