using System;

namespace EyeRest.Services
{
    public interface ISystemTrayService
    {
        void Initialize();
        void ShowTrayIcon();
        void HideTrayIcon();
        void UpdateTrayIcon(TrayIconState state);
        void ShowBalloonTip(string title, string text);
        event EventHandler RestoreRequested;
        event EventHandler ExitRequested;
    }

    public enum TrayIconState
    {
        Active,
        Paused,
        Break,
        Error
    }
}