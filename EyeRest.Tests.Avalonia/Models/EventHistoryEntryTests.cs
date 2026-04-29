using System.Collections.Generic;
using EyeRest.Models;
using Xunit;

namespace EyeRest.Tests.Avalonia.Models
{
    public class EventHistoryEntryTests
    {
        [Fact]
        public void EventTypeText_BreakShownWithManualSource_AppendsManualBadge()
        {
            var entry = new EventHistoryEntry
            {
                EventType = EventHistoryType.BreakShown,
                Metadata = new Dictionary<string, object?> { ["triggerSource"] = "Manual" }
            };

            Assert.Equal("Break (Manual)", entry.EventTypeText);
        }

        [Fact]
        public void EventTypeText_BreakShownWithAutomaticSource_NoBadge()
        {
            var entry = new EventHistoryEntry
            {
                EventType = EventHistoryType.BreakShown,
                Metadata = new Dictionary<string, object?> { ["triggerSource"] = "Automatic" }
            };

            Assert.Equal("Break", entry.EventTypeText);
        }

        [Fact]
        public void EventTypeText_BreakShownWithoutSourceMetadata_NoBadge()
        {
            var entry = new EventHistoryEntry
            {
                EventType = EventHistoryType.BreakShown,
                Metadata = new Dictionary<string, object?>()
            };

            Assert.Equal("Break", entry.EventTypeText);
        }

        [Fact]
        public void EventTypeText_NonBreakEventWithManualMetadata_NoBadge()
        {
            // Defensive: badge only applies to break-related types.
            var entry = new EventHistoryEntry
            {
                EventType = EventHistoryType.EyeRestShown,
                Metadata = new Dictionary<string, object?> { ["triggerSource"] = "Manual" }
            };

            Assert.Equal("Eye Rest", entry.EventTypeText);
        }

        [Fact]
        public void EventTypeText_BreakSkippedAndCompletedAndDelayed_AppendBadgeForManual()
        {
            foreach (var type in new[]
            {
                EventHistoryType.BreakCompleted,
                EventHistoryType.BreakSkipped,
                EventHistoryType.BreakDelayed,
            })
            {
                var entry = new EventHistoryEntry
                {
                    EventType = type,
                    Metadata = new Dictionary<string, object?> { ["triggerSource"] = "Manual" }
                };
                Assert.EndsWith("(Manual)", entry.EventTypeText);
            }
        }
    }
}
