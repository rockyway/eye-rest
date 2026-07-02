using System.Runtime.InteropServices;
using Avalonia.Controls;
using Avalonia.Input;
using EyeRest.UI.ViewModels;

namespace EyeRest.UI.Views
{
    public partial class AnalyticsWindow : Window
    {
        public AnalyticsWindow()
        {
            InitializeComponent();

            // On Windows, hide system chrome since we have custom caption buttons.
            if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            {
                ExtendClientAreaChromeHints = Avalonia.Platform.ExtendClientAreaChromeHints.NoChrome;
            }
            // On Linux, X11 WMs don't honor ExtendClientAreaToDecorationsHint and stack
            // a duplicate title bar — drop server-side decorations; the custom title bar
            // provides drag, minimize, and close.
            else if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
            {
                SystemDecorations = SystemDecorations.None;
            }
        }

        public AnalyticsWindow(AnalyticsDashboardViewModel viewModel) : this()
        {
            DataContext = viewModel;
        }

        private void TitleBar_PointerPressed(object? sender, PointerPressedEventArgs e)
        {
            if (e.GetCurrentPoint(this).Properties.IsLeftButtonPressed)
                BeginMoveDrag(e);
        }

        private void MinimizeButton_Click(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
            => WindowState = WindowState.Minimized;

        private void CloseButton_Click(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
            => Close();
    }
}
