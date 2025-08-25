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
            
            // X button just minimizes to tray - no confirmation needed since all settings auto-save
            // No unsaved changes are possible since everything saves in real-time
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