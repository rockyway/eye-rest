using System.Diagnostics;
using Microsoft.Extensions.Logging;

namespace EyeRest.Services
{
    /// <summary>
    /// Shared helper that posts desktop notifications via <c>notify-send</c>
    /// (org.freedesktop.Notifications). Present on stock GNOME/Cinnamon/KDE
    /// desktops (libnotify-bin). Failures degrade to a log entry — notifications
    /// are supplementary; the Avalonia popups are the primary UX.
    /// </summary>
    internal static class LinuxNotifications
    {
        private const string AppName = "Blink Twice EyeRest";

        internal static void Send(ILogger logger, string title, string body)
        {
            try
            {
                var psi = new ProcessStartInfo
                {
                    FileName = "notify-send",
                    UseShellExecute = false,
                    CreateNoWindow = true,
                };
                psi.ArgumentList.Add($"--app-name={AppName}");
                psi.ArgumentList.Add(title);
                psi.ArgumentList.Add(body);

                using var process = Process.Start(psi);
                // notify-send returns immediately after posting over DBus;
                // don't block the caller waiting on it.
                logger.LogDebug("Posted Linux notification: {Title}", title);
            }
            catch (Exception ex)
            {
                logger.LogWarning(ex, "Failed to post Linux notification: {Title}", title);
            }
        }
    }
}
