using System;
using System.Diagnostics;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;

namespace EyeRest.Diagnostics
{
    /// <summary>
    /// Diagnostic tool to check Teams meeting detection
    /// </summary>
    public class DiagnosticTeamsDetection
    {
        [DllImport("user32.dll", CharSet = CharSet.Unicode)]
        private static extern int GetWindowText(IntPtr hWnd, StringBuilder strText, int maxCount);

        [DllImport("user32.dll")]
        private static extern int GetWindowTextLength(IntPtr hWnd);

        public static void Main()
        {
            Console.WriteLine("=== Teams Meeting Detection Diagnostic ===");
            Console.WriteLine("Looking for Teams-related processes...\n");

            var teamsProcessNames = new[] { "ms-teams", "teams", "msteams", "teams2", "msteamsupdate", "msedgewebview2" };
            var processes = Process.GetProcesses();
            int foundCount = 0;

            foreach (var process in processes)
            {
                try
                {
                    var processName = process.ProcessName.ToLowerInvariant();
                    
                    // Check if this is a Teams-related process
                    if (teamsProcessNames.Any(name => processName.Contains(name)))
                    {
                        var windowTitle = GetWindowTitle(process.MainWindowHandle);
                        
                        Console.WriteLine($"Found: {process.ProcessName} (PID: {process.Id})");
                        Console.WriteLine($"  Window Title: '{windowTitle}'");
                        Console.WriteLine($"  Has Main Window: {process.MainWindowHandle != IntPtr.Zero}");
                        
                        // Check for meeting indicators
                        bool isMeeting = false;
                        if (windowTitle.Contains("Meeting with", StringComparison.OrdinalIgnoreCase))
                        {
                            Console.WriteLine("  ✅ MEETING DETECTED: 'Meeting with' found in title");
                            isMeeting = true;
                        }
                        else if (windowTitle.Contains("Teams", StringComparison.OrdinalIgnoreCase))
                        {
                            Console.WriteLine("  ⚠️ Teams window found, checking for call indicators...");
                            
                            var callIndicators = new[] { "Meeting", "Call", "Join", "Present", "Share" };
                            foreach (var indicator in callIndicators)
                            {
                                if (windowTitle.Contains(indicator, StringComparison.OrdinalIgnoreCase))
                                {
                                    Console.WriteLine($"  ✅ Call indicator found: '{indicator}'");
                                    isMeeting = true;
                                    break;
                                }
                            }
                        }
                        
                        if (!isMeeting && processName == "msedgewebview2")
                        {
                            Console.WriteLine("  ℹ️ WebView2 process - requires Teams in window title");
                        }
                        
                        Console.WriteLine();
                        foundCount++;
                    }
                }
                catch (Exception ex)
                {
                    // Skip processes we can't access
                }
                finally
                {
                    process.Dispose();
                }
            }

            if (foundCount == 0)
            {
                Console.WriteLine("❌ No Teams-related processes found");
                Console.WriteLine("\nPossible reasons:");
                Console.WriteLine("1. Teams is not running");
                Console.WriteLine("2. Teams is running but minimized to system tray");
                Console.WriteLine("3. Permission issues accessing process information");
            }
            else
            {
                Console.WriteLine($"Found {foundCount} Teams-related processes");
                Console.WriteLine("\nIf meeting detection isn't working:");
                Console.WriteLine("1. Make sure the Teams meeting window is visible (not minimized)");
                Console.WriteLine("2. Check that you're actually in a meeting (not just Teams open)");
                Console.WriteLine("3. Try restarting the EyeRest application");
            }

            Console.WriteLine("\nPress any key to exit...");
            Console.ReadKey();
        }

        private static string GetWindowTitle(IntPtr windowHandle)
        {
            try
            {
                if (windowHandle == IntPtr.Zero) return string.Empty;
                
                var titleLength = GetWindowTextLength(windowHandle);
                if (titleLength == 0) return string.Empty;
                
                var title = new StringBuilder(titleLength + 1);
                GetWindowText(windowHandle, title, title.Capacity);
                
                return title.ToString();
            }
            catch
            {
                return string.Empty;
            }
        }
    }
}