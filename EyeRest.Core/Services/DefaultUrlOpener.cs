using System.Diagnostics;

namespace EyeRest.Services
{
    /// <summary>
    /// BL-002: opens URLs in the user's default browser via Process.Start with
    /// UseShellExecute=true. The OS routes <c>http://</c> / <c>https://</c> handlers
    /// — Windows hands to the default browser; macOS routes through LaunchServices.
    /// </summary>
    public sealed class DefaultUrlOpener : IUrlOpener
    {
        public void Open(string url)
        {
            if (string.IsNullOrWhiteSpace(url)) return;
            try
            {
                using var _ = Process.Start(new ProcessStartInfo(url) { UseShellExecute = true });
            }
            catch
            {
                // Best-effort: if the OS can't dispatch the URL (no default handler, malformed,
                // or process spawn denied) we swallow rather than crash the popup flow. Logging
                // is intentionally avoided here to keep the surface dependency-free; M5
                // integration audit may add a logger if useful.
            }
        }
    }
}
