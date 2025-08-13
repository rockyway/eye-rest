using System.Windows.Controls;
using EyeRest.ViewModels;

namespace EyeRest.Views
{
    /// <summary>
    /// Interaction logic for AnalyticsDashboardView.xaml
    /// Comprehensive analytics dashboard with data visualization
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