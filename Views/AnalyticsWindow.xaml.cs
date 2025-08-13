using System.Linq;
using System.Windows;
using EyeRest.ViewModels;

namespace EyeRest.Views
{
    public partial class AnalyticsWindow : Window
    {
        public AnalyticsWindow(AnalyticsDashboardViewModel viewModel)
        {
            InitializeComponent();
            DataContext = viewModel;
            
            // Apply the current application theme to this window
            ApplyCurrentTheme();
        }

        public void ApplyCurrentTheme()
        {
            try
            {
                System.Diagnostics.Debug.WriteLine("🎨 Analytics Window: Starting aggressive theme application");
                
                // Clear existing resources to ensure clean state
                this.Resources.MergedDictionaries.Clear();
                
                // Copy all merged dictionaries from Application.Current.Resources
                foreach (var dictionary in Application.Current.Resources.MergedDictionaries)
                {
                    this.Resources.MergedDictionaries.Add(dictionary);
                }
                
                // Copy direct resources from application
                foreach (var key in Application.Current.Resources.Keys)
                {
                    if (!this.Resources.Contains(key))
                    {
                        this.Resources[key] = Application.Current.Resources[key];
                    }
                }
                
                // Debug: Check if critical theme resources are available
                var backgroundBrush = this.TryFindResource("BackgroundBrush");
                var textPrimaryBrush = this.TryFindResource("TextPrimaryBrush");
                var controlBackgroundBrush = this.TryFindResource("ControlBackgroundBrush");
                
                System.Diagnostics.Debug.WriteLine($"🎨 BackgroundBrush found: {backgroundBrush != null}");
                System.Diagnostics.Debug.WriteLine($"🎨 TextPrimaryBrush found: {textPrimaryBrush != null}");
                System.Diagnostics.Debug.WriteLine($"🎨 ControlBackgroundBrush found: {controlBackgroundBrush != null}");
                
                // Force UserControl to inherit theme resources
                if (this.Content is FrameworkElement userControl)
                {
                    // Clear UserControl resources and inherit from window
                    userControl.Resources.MergedDictionaries.Clear();
                    
                    // Copy all window resources to UserControl
                    foreach (var dictionary in this.Resources.MergedDictionaries)
                    {
                        userControl.Resources.MergedDictionaries.Add(dictionary);
                    }
                    
                    // Force refresh all child elements recursively
                    RefreshVisualTree(userControl);
                    
                    System.Diagnostics.Debug.WriteLine("🎨 UserControl theme resources applied and refreshed");
                }
                
                // Apply window background
                if (backgroundBrush is System.Windows.Media.Brush brush)
                {
                    this.Background = brush;
                    System.Diagnostics.Debug.WriteLine("🎨 Window background applied from theme");
                }
                
                // Force window visual refresh with multiple techniques
                this.InvalidateVisual();
                this.InvalidateMeasure();
                this.InvalidateArrange();
                this.UpdateLayout();
                
                // Force complete style re-evaluation by temporarily changing property
                var originalOpacity = this.Opacity;
                this.Opacity = 0.99;
                this.Dispatcher.BeginInvoke(new System.Action(() => {
                    this.Opacity = originalOpacity;
                    // Force another refresh after opacity change
                    RefreshVisualTree(this);
                }), System.Windows.Threading.DispatcherPriority.Background);
                
                System.Diagnostics.Debug.WriteLine("🎨 Analytics Window: Theme application completed successfully");
            }
            catch (System.Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"🎨 Analytics Window: Theme application error: {ex.Message}");
                System.Diagnostics.Debug.WriteLine($"🎨 Stack trace: {ex.StackTrace}");
                
                // Apply fallback based on current theme detection
                var isDarkMode = Application.Current.Resources.MergedDictionaries
                    .Any(dict => dict.Source?.ToString().Contains("DarkTheme") == true);
                    
                this.Background = new System.Windows.Media.SolidColorBrush(
                    isDarkMode 
                        ? System.Windows.Media.Color.FromRgb(18, 18, 18)
                        : System.Windows.Media.Color.FromRgb(250, 250, 250));
            }
        }
        
        /// <summary>
        /// Recursively refresh the visual tree to force DynamicResource re-evaluation
        /// </summary>
        private void RefreshVisualTree(DependencyObject element)
        {
            if (element == null) return;
            
            // Force the element to re-evaluate its visual state
            if (element is FrameworkElement fe)
            {
                fe.InvalidateVisual();
                fe.InvalidateMeasure();
                fe.InvalidateArrange();
                fe.UpdateLayout();
                
                // Force style re-evaluation
                var style = fe.Style;
                fe.Style = null;
                fe.Style = style;
            }
            
            // Recursively refresh all children
            int childCount = System.Windows.Media.VisualTreeHelper.GetChildrenCount(element);
            for (int i = 0; i < childCount; i++)
            {
                var child = System.Windows.Media.VisualTreeHelper.GetChild(element, i);
                RefreshVisualTree(child);
            }
        }
    }
}