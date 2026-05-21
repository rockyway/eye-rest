using System;
using System.Linq;
using Avalonia;
using EyeRest.Models;
using EyeRest.UI.Views;
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

        // -------- ComputePosition math (per QA recommendation) --------

        // 1920x1080 work area at origin, scaling 1.0, 400x300 popup → margin = 8px
        private static readonly PixelRect WorkArea = new(0, 0, 1920, 1080);
        private const double Scaling = 1.0;
        private const int W = 400;
        private const int H = 300;
        private const int Margin = 8;

        [Fact]
        public void ComputePosition_TopLeft_AnchorsToWorkAreaOriginPlusMargin()
        {
            var p = PopupWindow.ComputePosition(PopupPlacement.TopLeft, WorkArea, Scaling, W, H);
            Assert.Equal(new PixelPoint(Margin, Margin), p);
        }

        [Fact]
        public void ComputePosition_TopRight_AnchorsToRightEdgeMinusWidthMinusMargin()
        {
            var p = PopupWindow.ComputePosition(PopupPlacement.TopRight, WorkArea, Scaling, W, H);
            Assert.Equal(new PixelPoint(1920 - W - Margin, Margin), p);
        }

        [Fact]
        public void ComputePosition_TopCenter_HorizontallyCenteredAtTop()
        {
            var p = PopupWindow.ComputePosition(PopupPlacement.TopCenter, WorkArea, Scaling, W, H);
            Assert.Equal(new PixelPoint((1920 - W) / 2, Margin), p);
        }

        [Fact]
        public void ComputePosition_BottomLeft_AnchorsToBottomLeft()
        {
            var p = PopupWindow.ComputePosition(PopupPlacement.BottomLeft, WorkArea, Scaling, W, H);
            Assert.Equal(new PixelPoint(Margin, 1080 - H - Margin), p);
        }

        [Fact]
        public void ComputePosition_BottomRight_AnchorsToBottomRight()
        {
            var p = PopupWindow.ComputePosition(PopupPlacement.BottomRight, WorkArea, Scaling, W, H);
            Assert.Equal(new PixelPoint(1920 - W - Margin, 1080 - H - Margin), p);
        }

        [Fact]
        public void ComputePosition_BottomCenter_HorizontallyCenteredAtBottom()
        {
            var p = PopupWindow.ComputePosition(PopupPlacement.BottomCenter, WorkArea, Scaling, W, H);
            Assert.Equal(new PixelPoint((1920 - W) / 2, 1080 - H - Margin), p);
        }

        [Fact]
        public void ComputePosition_LeftCenter_VerticallyCenteredOnLeftEdge()
        {
            var p = PopupWindow.ComputePosition(PopupPlacement.LeftCenter, WorkArea, Scaling, W, H);
            Assert.Equal(new PixelPoint(Margin, (1080 - H) / 2), p);
        }

        [Fact]
        public void ComputePosition_RightCenter_VerticallyCenteredOnRightEdge()
        {
            var p = PopupWindow.ComputePosition(PopupPlacement.RightCenter, WorkArea, Scaling, W, H);
            Assert.Equal(new PixelPoint(1920 - W - Margin, (1080 - H) / 2), p);
        }

        [Fact]
        public void ComputePosition_Center_PlacesPopupDeadCenter()
        {
            var p = PopupWindow.ComputePosition(PopupPlacement.Center, WorkArea, Scaling, W, H);
            Assert.Equal(new PixelPoint((1920 - W) / 2, (1080 - H) / 2), p);
        }

        [Fact]
        public void ComputePosition_HonorsScreenOffsetForMultiMonitorSetups()
        {
            // Secondary monitor at (1920, 0), 1280x720
            var secondary = new PixelRect(1920, 0, 1280, 720);
            var p = PopupWindow.ComputePosition(PopupPlacement.TopLeft, secondary, Scaling, W, H);
            Assert.Equal(new PixelPoint(1920 + Margin, Margin), p);
        }

        [Fact]
        public void ComputePosition_ScalesMarginWithDpi()
        {
            // Retina 2x: margin should be 16px in physical pixels
            var p = PopupWindow.ComputePosition(PopupPlacement.TopLeft, WorkArea, scaling: 2.0, W, H);
            Assert.Equal(new PixelPoint(16, 16), p);
        }
    }
}
