using System;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.Themes.Fluent;
using EyeRest.Models;
using EyeRest.UI.Views;
using FluentAssertions;
using Xunit;

[assembly: AvaloniaTestApplication(typeof(EyeRest.Tests.Avalonia.Views.HeadlessTestApp))]

namespace EyeRest.Tests.Avalonia.Views
{
    public class HeadlessTestApp : Application
    {
        public override void Initialize() => Styles.Add(new FluentTheme());

        public static AppBuilder BuildAvaloniaApp() =>
            AppBuilder.Configure<HeadlessTestApp>().UseHeadless(new AvaloniaHeadlessPlatformOptions());
    }

    /// <summary>
    /// Verifies Standard / Compact / Minimal layouts load (style selectors resolve) and get
    /// strictly smaller. Sizes are measured with unlimited space, like SizeToContent.
    /// </summary>
    public class PopupDisplayModeTests
    {
        private static Size Measure(Control c)
        {
            var w = new Window { Content = c, SizeToContent = SizeToContent.WidthAndHeight };
            w.Show();
            c.Measure(Size.Infinity);
            var size = c.DesiredSize;
            w.Close();
            return size;
        }

        [AvaloniaTheory]
        [InlineData(typeof(EyeRestPopup))]
        [InlineData(typeof(BreakPopup))]
        public void Reminder_GetsSmaller_PerMode(Type type)
        {
            Size Size(PopupDisplayMode m)
            {
                var c = (Control)Activator.CreateInstance(type)!;
                if (c is EyeRestPopup e) e.ApplyDisplayMode(m);
                if (c is BreakPopup b) b.ApplyDisplayMode(m);
                return Measure(c);
            }

            var std = Size(PopupDisplayMode.Standard);
            var cmp = Size(PopupDisplayMode.Compact);
            var min = Size(PopupDisplayMode.Minimal);

            cmp.Width.Should().BeLessThan(std.Width);
            cmp.Height.Should().BeLessThan(std.Height);
            min.Width.Should().BeLessThan(cmp.Width);
            min.Height.Should().BeLessThan(cmp.Height);
        }

        [AvaloniaFact]
        public void BreakPopup_Standard_KeepsFixedSize()
        {
            var b = new BreakPopup();
            b.ApplyDisplayMode(PopupDisplayMode.Standard);
            var s = Measure(b);
            s.Width.Should().BeApproximately(580, 1);
            s.Height.Should().BeApproximately(640, 1);
        }

        [AvaloniaTheory]
        [InlineData(typeof(EyeRestWarningPopup))]
        [InlineData(typeof(BreakWarningPopup))]
        public void Warning_Compact_IsSmallerThanStandard_AndMinimalShowsStrip(Type type)
        {
            Control Make(PopupDisplayMode m)
            {
                var c = (Control)Activator.CreateInstance(type)!;
                if (c is EyeRestWarningPopup e) { e.ApplyDisplayMode(m); e.StartCountdown(60); }
                if (c is BreakWarningPopup b) { b.ApplyDisplayMode(m); b.StartCountdown(TimeSpan.FromSeconds(60)); }
                return c;
            }

            var std = Measure(Make(PopupDisplayMode.Standard));
            var cmp = Measure(Make(PopupDisplayMode.Compact));
            var min = Measure(Make(PopupDisplayMode.Minimal));

            cmp.Height.Should().BeLessThan(std.Height);
            min.Height.Should().BeLessThan(cmp.Height);
        }
    }
}
