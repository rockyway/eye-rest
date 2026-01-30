using System;
using System.IO;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
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
            Loaded += OnLoaded;
        }

        public AnalyticsDashboardView(AnalyticsDashboardViewModel viewModel) : this()
        {
            DataContext = viewModel;
        }

        private void OnLoaded(object sender, RoutedEventArgs e)
        {
            var sb = new StringBuilder();
            sb.AppendLine($"=== AnalyticsDashboardView Visual Tree Debug ({DateTime.Now}) ===");

            // Find TabControl
            var tabControl = FindVisualChild<TabControl>(this);
            if (tabControl != null)
            {
                sb.AppendLine($"TabControl found: {tabControl.GetType().Name}");
                sb.AppendLine($"  Style: {tabControl.Style?.TargetType?.Name ?? "null"}");
                sb.AppendLine($"  Template: {tabControl.Template?.TargetType?.Name ?? "null"}");
                sb.AppendLine($"  BorderThickness: {tabControl.BorderThickness}");
                sb.AppendLine($"  BorderBrush: {tabControl.BorderBrush}");
                sb.AppendLine($"  Items count: {tabControl.Items.Count}");

                // Log TabControl visual tree
                sb.AppendLine("\n  TabControl Visual Tree:");
                LogVisualTree(tabControl, sb, 2, 4);

                // Check first TabItem
                if (tabControl.Items.Count > 0 && tabControl.Items[0] is TabItem firstTab)
                {
                    sb.AppendLine($"\n  First TabItem: {firstTab.Header}");
                    sb.AppendLine($"    Style: {firstTab.Style?.TargetType?.Name ?? "null"}");
                    sb.AppendLine($"    Template: {firstTab.Template?.TargetType?.Name ?? "null"}");
                    sb.AppendLine("\n    TabItem Visual Tree:");
                    LogVisualTree(firstTab, sb, 4, 5);
                }
            }
            else
            {
                sb.AppendLine("TabControl NOT FOUND!");
                sb.AppendLine("\nFull Visual Tree:");
                LogVisualTree(this, sb, 0, 6);
            }

            var logPath = @"analytics_debug.log";
            File.WriteAllText(logPath, sb.ToString());
            MessageBox.Show($"Debug log written to:\n{logPath}", "Analytics Debug");
        }

        private void LogVisualTree(DependencyObject parent, StringBuilder sb, int indent, int maxDepth)
        {
            if (maxDepth <= 0) return;

            int childCount = VisualTreeHelper.GetChildrenCount(parent);
            for (int i = 0; i < childCount; i++)
            {
                var child = VisualTreeHelper.GetChild(parent, i);
                var prefix = new string(' ', indent * 2);
                var typeName = child.GetType().Name;
                var extra = "";

                if (child is FrameworkElement fe)
                {
                    if (!string.IsNullOrEmpty(fe.Name))
                        extra += $" Name=\"{fe.Name}\"";
                    extra += $" W={fe.ActualWidth:F0} H={fe.ActualHeight:F0}";
                }
                if (child is System.Windows.Shapes.Rectangle rect)
                {
                    extra += $" Fill={rect.Fill}";
                }
                if (child is Border border)
                {
                    extra += $" BorderThickness={border.BorderThickness} BorderBrush={border.BorderBrush}";
                }

                sb.AppendLine($"{prefix}- {typeName}{extra}");
                LogVisualTree(child, sb, indent + 1, maxDepth - 1);
            }
        }

        private T? FindVisualChild<T>(DependencyObject parent) where T : DependencyObject
        {
            for (int i = 0; i < VisualTreeHelper.GetChildrenCount(parent); i++)
            {
                var child = VisualTreeHelper.GetChild(parent, i);
                if (child is T found)
                    return found;
                var result = FindVisualChild<T>(child);
                if (result != null)
                    return result;
            }
            return null;
        }
    }
}