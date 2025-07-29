using System;
using System.Drawing;
using System.Windows;
using System.Windows.Forms;

namespace EyeRest.Services
{
    public class TrayService
    {
        private NotifyIcon? _notifyIcon;
        private Window? _mainWindow;
        private readonly IconService _iconService;

        public event EventHandler? ShowRequested;
        public event EventHandler? ExitRequested;

        public TrayService(IconService iconService)
        {
            _iconService = iconService;
        }

        public void Initialize(Window mainWindow)
        {
            _mainWindow = mainWindow;
            _mainWindow.StateChanged += OnWindowStateChanged;

            _notifyIcon = new NotifyIcon();
            _notifyIcon.Icon = _iconService.GetApplicationIcon();
            
            _notifyIcon.Text = "EyeRest";
            _notifyIcon.DoubleClick += OnNotifyIconDoubleClick;

            _notifyIcon.ContextMenuStrip = new ContextMenuStrip();
            _notifyIcon.ContextMenuStrip.Items.Add("Show", null, OnShowClicked);
            _notifyIcon.ContextMenuStrip.Items.Add("Exit", null, OnExitClicked);

            _notifyIcon.Visible = true;
        }

        private void OnWindowStateChanged(object sender, EventArgs e)
        {
            if (_mainWindow?.WindowState == WindowState.Minimized)
            {
                _mainWindow.Hide();
            }
        }

        private void OnNotifyIconDoubleClick(object sender, EventArgs e)
        {
            ShowRequested?.Invoke(this, EventArgs.Empty);
        }

        private void OnShowClicked(object sender, EventArgs e)
        {
            ShowRequested?.Invoke(this, EventArgs.Empty);
        }

        private void OnExitClicked(object sender, EventArgs e)
        {
            ExitRequested?.Invoke(this, EventArgs.Empty);
        }

        private void ShowMainWindow()
        {
            _mainWindow?.Show();
            _mainWindow!.WindowState = WindowState.Normal;
            _mainWindow!.Activate();
        }

        public void Shutdown()
        {
            _notifyIcon?.Dispose();
        }
    }
}
