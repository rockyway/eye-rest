using System.Windows;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using EyeRest.Services;
using EyeRest.ViewModels;

namespace EyeRest.Views
{
    public partial class MainWindow : Window
    {
        public bool IsClosing { get; set; }

        public MainWindow(MainWindowViewModel viewModel, IconService iconService)
        {
            InitializeComponent();
            DataContext = viewModel;
            
            // Set window icon programmatically
            SetWindowIcon(iconService);
            
            // Add closing event handler
            Closing += MainWindow_Closing;
            
            // CRITICAL FIX: Load configuration immediately when window loads to minimize wrong value display time
            Loaded += async (s, e) =>
            {
                if (DataContext is MainWindowViewModel vm)
                {
                    await vm.LoadConfigurationImmediatelyAsync();
                }
            };
        }

        private void SetWindowIcon(IconService iconService)
        {
            try
            {
                var icon = iconService.GetApplicationIcon();
                var iconBitmap = icon.ToBitmap();
                var hBitmap = iconBitmap.GetHbitmap();
                
                var wpfBitmap = Imaging.CreateBitmapSourceFromHBitmap(
                    hBitmap,
                    System.IntPtr.Zero,
                    System.Windows.Int32Rect.Empty,
                    BitmapSizeOptions.FromEmptyOptions());
                
                Icon = wpfBitmap;
                
                // Clean up
                iconBitmap.Dispose();
                DeleteObject(hBitmap);
            }
            catch
            {
                // If icon setting fails, just continue without an icon
            }
        }

        [System.Runtime.InteropServices.DllImport("gdi32.dll")]
        public static extern bool DeleteObject(System.IntPtr hObject);

        private void MainWindow_Closing(object? sender, System.ComponentModel.CancelEventArgs e)
        {
            // Don't show confirmation if app is already closing programmatically
            if (IsClosing) return;
            
            // X button should just minimize to tray without confirmation
            // Only check for unsaved changes, no general exit confirmation
            if (DataContext is MainWindowViewModel viewModel && viewModel.HasUnsavedChanges)
            {
                var result = MessageBox.Show(
                    "You have unsaved changes. Do you want to save them before minimizing?",
                    "Unsaved Changes",
                    MessageBoxButton.YesNoCancel,
                    MessageBoxImage.Question);
                
                switch (result)
                {
                    case MessageBoxResult.Yes:
                        // Save and continue closing (which will minimize to tray)
                        viewModel.SaveCommand.Execute(null);
                        break;
                    case MessageBoxResult.No:
                        // Don't save, continue closing (which will minimize to tray)
                        break;
                    case MessageBoxResult.Cancel:
                        // Cancel closing
                        e.Cancel = true;
                        return;
                }
            }
            
            // No general confirmation needed - X button just minimizes to tray
        }

        public void UpdateCountdown()
        {
            // Method stub for countdown updates
            // Implementation will be provided by the view model
            if (DataContext is MainWindowViewModel viewModel)
            {
                viewModel.UpdateCountdown();
            }
        }
    }
}