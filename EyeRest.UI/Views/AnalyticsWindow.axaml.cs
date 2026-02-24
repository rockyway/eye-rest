using Avalonia.Controls;
using EyeRest.UI.ViewModels;

namespace EyeRest.UI.Views
{
    /// <summary>
    /// Analytics window that hosts the AnalyticsDashboardView.
    /// Cross-platform Avalonia port - no WPF theme management needed
    /// since Avalonia's DynamicResource system handles theme propagation.
    /// </summary>
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
    }
}
