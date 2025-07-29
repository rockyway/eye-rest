using System;
using System.ComponentModel;
using System.Drawing;
using System.Windows.Forms;
using Microsoft.Extensions.Logging;

namespace EyeRest.Services
{
    public class SystemTrayService : ISystemTrayService, IDisposable
    {
        private readonly ILogger<SystemTrayService> _logger;
        private readonly IconService _iconService;
        private NotifyIcon? _notifyIcon;

        public event EventHandler? RestoreRequested;
        public event EventHandler? ExitRequested;

        public SystemTrayService(ILogger<SystemTrayService> logger, IconService iconService)
        {
            _logger = logger;
            _iconService = iconService;
        }

        public void Initialize()
        {
            _notifyIcon = new NotifyIcon();
            _notifyIcon.DoubleClick += (s, e) => RestoreRequested?.Invoke(this, e);

            var contextMenu = new ContextMenuStrip();
            contextMenu.Items.Add("Restore", null, (s, e) => RestoreRequested?.Invoke(this, e));
            contextMenu.Items.Add("Exit", null, (s, e) => ExitRequested?.Invoke(this, e));
            
            _notifyIcon.ContextMenuStrip = contextMenu;

            _logger.LogInformation("System tray service initialized.");
        }

        public void ShowTrayIcon()
        {
            if (_notifyIcon == null) return;
            try
            {
                _notifyIcon.Icon = _iconService.GetApplicationIcon();
                _notifyIcon.Text = "EyeRest Application";
                _notifyIcon.Visible = true;
                _logger.LogInformation("System tray icon is now visible.");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to show the system tray icon.");
            }
        }

        public void HideTrayIcon()
        {
            if (_notifyIcon != null)
            {
                _notifyIcon.Visible = false;
                _logger.LogInformation("System tray icon hidden.");
            }
        }

        public void UpdateTrayIcon(TrayIconState state)
        {
            if (_notifyIcon == null) return;
            _notifyIcon.Text = $"EyeRest - {state}";
            _logger.LogInformation($"Tray icon text updated to: {state}");
        }

        public void ShowBalloonTip(string title, string text)
        {
            if (_notifyIcon != null)
            {
                _notifyIcon.ShowBalloonTip(5000, title, text, ToolTipIcon.Info);
                _logger.LogInformation($"Showing balloon tip: {title}");
            }
        }


        public void Dispose()
        {
            _notifyIcon?.Dispose();
            _notifyIcon = null;
            _logger.LogInformation("System tray service disposed.");
        }
    }
}