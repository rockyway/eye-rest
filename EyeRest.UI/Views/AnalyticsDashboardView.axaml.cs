using Avalonia.Controls;
using EyeRest.UI.ViewModels;

namespace EyeRest.UI.Views
{
    /// <summary>
    /// Interaction logic for AnalyticsDashboardView.axaml
    /// Comprehensive analytics dashboard with data visualization.
    /// </summary>
    public partial class AnalyticsDashboardView : UserControl
    {
        public AnalyticsDashboardView()
        {
            InitializeComponent();
        }

        public AnalyticsDashboardView(AnalyticsDashboardViewModel viewModel) : this()
        {
            DataContext = viewModel;
        }
    }
}
