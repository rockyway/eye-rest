using System;
using System.Linq;
using EyeRest.Models;
using Xunit;

namespace EyeRest.Tests.Avalonia.Services
{
    /// <summary>
    /// Tests for the EyeRest overlay + popup position feature (2026-05-21).
    /// Covers spec defaults, enum cardinality, and round-trip integrity.
    /// </summary>
    public class EyeRestOverlayAndPositionTests
    {
        [Fact]
        public void EyeRestSettings_NewFields_HaveExpectedDefaults()
        {
            var settings = new EyeRestSettings();

            Assert.True(settings.OverlayEnabled);
            Assert.Equal(50, settings.OverlayOpacityPercent);
            Assert.Equal(PopupPosition.TopRight, settings.PopupPosition);
        }

        [Fact]
        public void EyeRestSettings_PreservesExistingDefaults()
        {
            // Sanity check that adding new fields didn't disturb existing defaults.
            var settings = new EyeRestSettings();

            Assert.Equal(20, settings.IntervalMinutes);
            Assert.Equal(20, settings.DurationSeconds);
            Assert.True(settings.WarningEnabled);
            Assert.Equal(15, settings.WarningSeconds);
        }

        [Fact]
        public void PopupPosition_HasNineDistinctValues()
        {
            var values = Enum.GetValues<PopupPosition>();

            Assert.Equal(9, values.Length);
            Assert.Equal(values.Length, values.Distinct().Count());
        }

        [Fact]
        public void PopupPosition_IncludesAllCornerCenterAndEdgePositions()
        {
            var values = Enum.GetValues<PopupPosition>().ToHashSet();

            Assert.Contains(PopupPosition.Center, values);
            Assert.Contains(PopupPosition.TopLeft, values);
            Assert.Contains(PopupPosition.TopCenter, values);
            Assert.Contains(PopupPosition.TopRight, values);
            Assert.Contains(PopupPosition.LeftCenter, values);
            Assert.Contains(PopupPosition.RightCenter, values);
            Assert.Contains(PopupPosition.BottomLeft, values);
            Assert.Contains(PopupPosition.BottomCenter, values);
            Assert.Contains(PopupPosition.BottomRight, values);
        }

        [Theory]
        [InlineData(PopupPosition.Center)]
        [InlineData(PopupPosition.TopLeft)]
        [InlineData(PopupPosition.TopCenter)]
        [InlineData(PopupPosition.TopRight)]
        [InlineData(PopupPosition.LeftCenter)]
        [InlineData(PopupPosition.RightCenter)]
        [InlineData(PopupPosition.BottomLeft)]
        [InlineData(PopupPosition.BottomCenter)]
        [InlineData(PopupPosition.BottomRight)]
        public void EyeRestSettings_PopupPosition_RoundTripsAllValues(PopupPosition position)
        {
            var settings = new EyeRestSettings { PopupPosition = position };
            Assert.Equal(position, settings.PopupPosition);
        }

        [Theory]
        [InlineData(0)]
        [InlineData(50)]
        [InlineData(100)]
        public void EyeRestSettings_OverlayOpacity_AcceptsValidRange(int opacity)
        {
            var settings = new EyeRestSettings { OverlayOpacityPercent = opacity };
            Assert.Equal(opacity, settings.OverlayOpacityPercent);
        }
    }
}
