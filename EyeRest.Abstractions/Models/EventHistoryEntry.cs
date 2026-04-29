using System;
using System.Collections.Generic;
using System.Text.Json;

namespace EyeRest.Models
{
    public class EventHistoryEntry
    {
        public int Id { get; set; }
        public DateTime Timestamp { get; set; }
        public EventHistoryType EventType { get; set; }
        public string Description { get; set; } = string.Empty;
        public Dictionary<string, object?> Metadata { get; set; } = new();
        public int? SessionId { get; set; }

        // Display helpers for DataGrid binding
        public string TimestampText => Timestamp.ToString("MM/dd/yyyy HH:mm:ss");
        public string EventTypeText
        {
            get
            {
                var label = FormatEventType(EventType);
                if (Metadata.TryGetValue("triggerSource", out var src)
                    && src is not null
                    && string.Equals(src.ToString(), "Manual", StringComparison.OrdinalIgnoreCase)
                    && (EventType == EventHistoryType.BreakShown
                        || EventType == EventHistoryType.BreakCompleted
                        || EventType == EventHistoryType.BreakSkipped
                        || EventType == EventHistoryType.BreakDelayed))
                {
                    return $"{label} (Manual)";
                }
                return label;
            }
        }

        // Serialize/deserialize helpers for DB round-trip
        public string MetadataJson
        {
            get => Metadata.Count > 0 ? JsonSerializer.Serialize(Metadata) : "{}";
            set
            {
                try
                {
                    Metadata = string.IsNullOrEmpty(value)
                        ? new()
                        : JsonSerializer.Deserialize<Dictionary<string, object?>>(value) ?? new();
                }
                catch
                {
                    Metadata = new();
                }
            }
        }

        private static string FormatEventType(EventHistoryType type) => type switch
        {
            EventHistoryType.EyeRestWarning => "Eye Rest Warning",
            EventHistoryType.EyeRestShown => "Eye Rest",
            EventHistoryType.EyeRestCompleted => "Eye Rest Done",
            EventHistoryType.BreakWarning => "Break Warning",
            EventHistoryType.BreakShown => "Break",
            EventHistoryType.BreakCompleted => "Break Done",
            EventHistoryType.BreakSkipped => "Break Skipped",
            EventHistoryType.BreakDelayed => "Break Delayed",
            EventHistoryType.Paused => "Paused",
            EventHistoryType.Resumed => "Resumed",
            EventHistoryType.UserIdle => "User Idle",
            EventHistoryType.UserReturned => "User Returned",
            EventHistoryType.MeetingModeOn => "Meeting Mode",
            EventHistoryType.MeetingModeOff => "Meeting Ended",
            EventHistoryType.SettingsChanged => "Settings Changed",
            EventHistoryType.SessionReset => "Session Reset",
            _ => type.ToString()
        };
    }
}
