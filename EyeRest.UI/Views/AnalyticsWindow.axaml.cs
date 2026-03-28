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
