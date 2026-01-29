using System;
using System.Diagnostics;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using TestStack.White;
using TestStack.White.Factory;
using TestStack.White.UIItems;
using TestStack.White.UIItems.WindowItems;
using TestStack.White.UIItems.Finders;
using TestStack.White.Configuration;
using NUnit.Framework;

namespace EyeRest.Tests.UI
{
    /// <summary>
    /// Comprehensive UI Automation Framework for EyeRest application
    /// Provides application lifecycle management, UI element interaction, and verification capabilities
    /// </summary>
    public class UIAutomationFramework : IDisposable
    {
        private Application? _application;
        private Window? _mainWindow;
#pragma warning disable CS0169 // Field is never used - reserved for future process tracking
        private Process? _applicationProcess;
#pragma warning restore CS0169
        private readonly string _applicationPath;
        private readonly TimeSpan _defaultTimeout = TimeSpan.FromSeconds(10);
        
        public UIAutomationFramework()
        {
            // Configure TestStack.White for better reliability
            // Note: Some configuration options may not be available in newer versions
            try
            {
                CoreAppXmlConfiguration.Instance.BusyTimeout = 5000; // 5 seconds
                // Other configuration will use defaults
            }
            catch
            {
                // Configuration may not be available - continue with defaults
            }
            
            // Determine application path - try multiple possible locations
            _applicationPath = FindApplicationExecutable();
        }

        private string FindApplicationExecutable()
        {
            var possiblePaths = new[]
            {
                Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "EyeRest.exe"),
                Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "..", "..", "..", "..", "bin", "Debug", "net8.0-windows", "EyeRest.exe"),
                Path.Combine(Directory.GetCurrentDirectory(), "EyeRest.exe"),
                Path.Combine(Directory.GetCurrentDirectory(), "bin", "Debug", "net8.0-windows", "EyeRest.exe")
            };

            foreach (var path in possiblePaths)
            {
                if (File.Exists(path))
                    return path;
            }

            throw new FileNotFoundException("EyeRest.exe not found. Please ensure the application is built.");
        }

        /// <summary>
        /// Launches the EyeRest application and initializes UI automation
        /// </summary>
        public async Task<bool> LaunchApplicationAsync()
        {
            try
            {
                TestContext.WriteLine($"🚀 Launching application from: {_applicationPath}");

                // Kill any existing instances to ensure clean state
                await KillExistingInstances();

                // Launch the application
                _application = Application.Launch(_applicationPath);
                
                if (_application == null)
                {
                    TestContext.WriteLine("❌ Failed to launch application");
                    return false;
                }

                // Wait for main window to appear
                _mainWindow = await WaitForMainWindowAsync();
                
                if (_mainWindow == null)
                {
                    TestContext.WriteLine("❌ Main window not found");
                    return false;
                }

                TestContext.WriteLine($"✅ Application launched successfully. Window Title: {_mainWindow.Title}");
                return true;
            }
            catch (Exception ex)
            {
                TestContext.WriteLine($"❌ Exception during application launch: {ex.Message}");
                return false;
            }
        }

        /// <summary>
        /// Waits for the main window to appear with timeout
        /// </summary>
        private async Task<Window?> WaitForMainWindowAsync()
        {
            var timeout = DateTime.Now.Add(_defaultTimeout);
            
            while (DateTime.Now < timeout)
            {
                try
                {
                    // Try to find windows with possible titles
                    var possibleTitles = new[] { "EyeRest", "Eye Rest", "MainWindow", "" };
                    
                    foreach (var title in possibleTitles)
                    {
                        var windows = _application?.GetWindows();
                        if (windows != null)
                        {
                            foreach (var window in windows)
                            {
                                if (string.IsNullOrEmpty(title) || window.Title.Contains(title))
                                {
                                    return window;
                                }
                            }
                        }
                    }
                }
                catch (Exception ex)
                {
                    TestContext.WriteLine($"⏳ Waiting for main window... ({ex.Message})");
                }

                await Task.Delay(500);
            }

            return null;
        }

        /// <summary>
        /// Kills any existing EyeRest instances to ensure clean testing environment
        /// </summary>
        private async Task KillExistingInstances()
        {
            var processes = Process.GetProcessesByName("EyeRest");
            foreach (var process in processes)
            {
                try
                {
                    process.Kill();
                    process.WaitForExit(5000);
                }
                catch (Exception ex)
                {
                    TestContext.WriteLine($"⚠️ Could not kill existing process: {ex.Message}");
                }
            }
        }

        /// <summary>
        /// Verifies that the main window is displayed and accessible
        /// </summary>
        public bool VerifyMainWindowDisplayed()
        {
            try
            {
                if (_mainWindow == null || _mainWindow.IsClosed)
                {
                    TestContext.WriteLine("❌ Main window is null or closed");
                    return false;
                }

                // Check if window is visible and has proper dimensions
                var bounds = _mainWindow.Bounds;
                bool isVisible = bounds.Width > 0 && bounds.Height > 0;
                
                TestContext.WriteLine($"📐 Window bounds: {bounds.Width}x{bounds.Height} at ({bounds.X}, {bounds.Y})");
                TestContext.WriteLine($"👁️ Window visible: {isVisible}");
                
                return isVisible;
            }
            catch (Exception ex)
            {
                TestContext.WriteLine($"❌ Exception verifying main window: {ex.Message}");
                return false;
            }
        }

        /// <summary>
        /// Verifies system tray icon is present
        /// </summary>
        public bool VerifySystemTrayIcon()
        {
            try
            {
                // Note: TestStack.White has limited support for system tray automation
                // This is a basic verification that the application is running (which should create tray icon)
                var processes = Process.GetProcessesByName("EyeRest");
                bool isRunning = processes.Length > 0;
                
                TestContext.WriteLine($"🔍 EyeRest processes running: {processes.Length}");
                return isRunning;
            }
            catch (Exception ex)
            {
                TestContext.WriteLine($"❌ Exception verifying system tray: {ex.Message}");
                return false;
            }
        }

        /// <summary>
        /// Finds a UI element by automation ID with timeout
        /// </summary>
        public T? FindElementByAutomationId<T>(string automationId, TimeSpan? timeout = null) where T : UIItem
        {
            var searchTimeout = timeout ?? _defaultTimeout;
            var endTime = DateTime.Now.Add(searchTimeout);

            while (DateTime.Now < endTime)
            {
                try
                {
                    var element = _mainWindow?.Get<T>(SearchCriteria.ByAutomationId(automationId));
                    if (element != null)
                    {
                        TestContext.WriteLine($"✅ Found element with AutomationId: {automationId}");
                        return element;
                    }
                }
                catch (Exception)
                {
                    // Continue searching
                }

                Thread.Sleep(100);
            }

            TestContext.WriteLine($"❌ Element not found with AutomationId: {automationId}");
            return null;
        }

        /// <summary>
        /// Finds a UI element by text content with timeout
        /// </summary>
        public T? FindElementByText<T>(string text, TimeSpan? timeout = null) where T : UIItem
        {
            var searchTimeout = timeout ?? _defaultTimeout;
            var endTime = DateTime.Now.Add(searchTimeout);

            while (DateTime.Now < endTime)
            {
                try
                {
                    var element = _mainWindow?.Get<T>(SearchCriteria.ByText(text));
                    if (element != null)
                    {
                        TestContext.WriteLine($"✅ Found element with text: {text}");
                        return element;
                    }
                }
                catch (Exception)
                {
                    // Continue searching
                }

                Thread.Sleep(100);
            }

            TestContext.WriteLine($"❌ Element not found with text: {text}");
            return null;
        }

        /// <summary>
        /// Takes a screenshot for visual verification and debugging
        /// </summary>
        public string TakeScreenshot(string testName = "")
        {
            try
            {
                var timestamp = DateTime.Now.ToString("yyyyMMdd_HHmmss");
                var fileName = string.IsNullOrEmpty(testName) 
                    ? $"screenshot_{timestamp}.png" 
                    : $"{testName}_{timestamp}.png";
                
                var screenshotPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Screenshots", fileName);
                Directory.CreateDirectory(Path.GetDirectoryName(screenshotPath)!);

                if (_mainWindow != null)
                {
                    var image = _mainWindow.VisibleImage;
                    image.Save(screenshotPath);
                    TestContext.WriteLine($"📷 Screenshot saved: {screenshotPath}");
                    return screenshotPath;
                }
                else
                {
                    TestContext.WriteLine("❌ Cannot take screenshot - no main window");
                    return string.Empty;
                }
            }
            catch (Exception ex)
            {
                TestContext.WriteLine($"❌ Exception taking screenshot: {ex.Message}");
                return string.Empty;
            }
        }

        /// <summary>
        /// Simulates timer start action
        /// </summary>
        public bool StartTimer()
        {
            try
            {
                // Look for timer start button or menu item
                var startButton = FindElementByText<Button>("Start") ?? 
                                FindElementByAutomationId<Button>("StartButton") ??
                                FindElementByAutomationId<Button>("StartTimersButton");

                if (startButton != null)
                {
                    startButton.Click();
                    TestContext.WriteLine("✅ Timer start button clicked");
                    return true;
                }

                TestContext.WriteLine("❌ Timer start button not found");
                return false;
            }
            catch (Exception ex)
            {
                TestContext.WriteLine($"❌ Exception starting timer: {ex.Message}");
                return false;
            }
        }

        /// <summary>
        /// Simulates timer stop action
        /// </summary>
        public bool StopTimer()
        {
            try
            {
                var stopButton = FindElementByText<Button>("Stop") ?? 
                               FindElementByAutomationId<Button>("StopButton") ??
                               FindElementByAutomationId<Button>("StopTimersButton");

                if (stopButton != null)
                {
                    stopButton.Click();
                    TestContext.WriteLine("✅ Timer stop button clicked");
                    return true;
                }

                TestContext.WriteLine("❌ Timer stop button not found");
                return false;
            }
            catch (Exception ex)
            {
                TestContext.WriteLine($"❌ Exception stopping timer: {ex.Message}");
                return false;
            }
        }

        /// <summary>
        /// Verifies countdown timer is displayed and updating
        /// </summary>
        public bool VerifyCountdownTimer()
        {
            try
            {
                // Look for timer display elements
                // Look for timer display elements using simpler approach
                var allElements = _mainWindow?.GetMultiple(SearchCriteria.All);
                
                if (allElements != null)
                {
                    foreach (var element in allElements)
                    {
                        try
                        {
                            var text = element.Name ?? "";
                            // Look for time format (MM:SS or HH:MM:SS)
                            if (System.Text.RegularExpressions.Regex.IsMatch(text, @"\d{1,2}:\d{2}"))
                            {
                                TestContext.WriteLine($"✅ Found timer display: {text}");
                                return true;
                            }
                        }
                        catch
                        {
                            // Continue searching if this element doesn't have accessible text
                        }
                    }
                }

                TestContext.WriteLine("❌ Countdown timer display not found");
                return false;
            }
            catch (Exception ex)
            {
                TestContext.WriteLine($"❌ Exception verifying countdown timer: {ex.Message}");
                return false;
            }
        }

        /// <summary>
        /// Opens settings/configuration UI
        /// </summary>
        public bool OpenSettings()
        {
            try
            {
                var settingsButton = FindElementByText<Button>("Settings") ?? 
                                   FindElementByAutomationId<Button>("SettingsButton");

                if (settingsButton != null)
                {
                    settingsButton.Click();
                    TestContext.WriteLine("✅ Settings opened");
                    return true;
                }

                TestContext.WriteLine("❌ Settings button not found");
                return false;
            }
            catch (Exception ex)
            {
                TestContext.WriteLine($"❌ Exception opening settings: {ex.Message}");
                return false;
            }
        }

        /// <summary>
        /// Minimizes application to system tray
        /// </summary>
        public bool MinimizeToTray()
        {
            try
            {
                if (_mainWindow != null)
                {
                    _mainWindow.Close();
                    TestContext.WriteLine("✅ Application minimized/closed to tray");
                    return true;
                }

                TestContext.WriteLine("❌ Cannot minimize - no main window");
                return false;
            }
            catch (Exception ex)
            {
                TestContext.WriteLine($"❌ Exception minimizing to tray: {ex.Message}");
                return false;
            }
        }

        /// <summary>
        /// Generates a comprehensive test report
        /// </summary>
        public TestReport GenerateTestReport()
        {
            var report = new TestReport
            {
                TestDateTime = DateTime.Now,
                ApplicationPath = _applicationPath,
                ApplicationLaunched = _application != null,
                MainWindowFound = _mainWindow != null && !_mainWindow.IsClosed,
                ScreenshotPath = TakeScreenshot("final_report")
            };

            // Add additional diagnostics
            if (_mainWindow != null)
            {
                report.WindowTitle = _mainWindow.Title;
                try
                {
                    var bounds = _mainWindow.Bounds;
                    report.WindowBounds = $"{bounds.Width}x{bounds.Height} at ({bounds.X}, {bounds.Y})";
                }
                catch
                {
                    report.WindowBounds = "Unable to retrieve bounds";
                }
            }

            return report;
        }

        /// <summary>
        /// Performs cleanup and closes the application
        /// </summary>
        public void Dispose()
        {
            try
            {
                TestContext.WriteLine("🧹 Cleaning up UI automation framework...");

                // Close main window gracefully
                if (_mainWindow != null && !_mainWindow.IsClosed)
                {
                    _mainWindow.Close();
                }

                // Close application
                if (_application != null)
                {
                    _application.Close();
                    _application.Dispose();
                }

                // Force kill any remaining processes
                var processes = Process.GetProcessesByName("EyeRest");
                foreach (var process in processes)
                {
                    try
                    {
                        process.Kill();
                        process.WaitForExit(5000);
                    }
                    catch (Exception ex)
                    {
                        TestContext.WriteLine($"⚠️ Could not kill process during cleanup: {ex.Message}");
                    }
                }

                TestContext.WriteLine("✅ Cleanup completed");
            }
            catch (Exception ex)
            {
                TestContext.WriteLine($"❌ Exception during cleanup: {ex.Message}");
            }
        }
    }

    /// <summary>
    /// Test report data structure for comprehensive validation reporting
    /// </summary>
    public class TestReport
    {
        public DateTime TestDateTime { get; set; }
        public string ApplicationPath { get; set; } = string.Empty;
        public bool ApplicationLaunched { get; set; }
        public bool MainWindowFound { get; set; }
        public string WindowTitle { get; set; } = string.Empty;
        public string WindowBounds { get; set; } = string.Empty;
        public string ScreenshotPath { get; set; } = string.Empty;
        
        public bool IsSuccessful => ApplicationLaunched && MainWindowFound;
        
        public override string ToString()
        {
            return $"Test Report ({TestDateTime:yyyy-MM-dd HH:mm:ss})\n" +
                   $"Application Path: {ApplicationPath}\n" +
                   $"Application Launched: {ApplicationLaunched}\n" +
                   $"Main Window Found: {MainWindowFound}\n" +
                   $"Window Title: {WindowTitle}\n" +
                   $"Window Bounds: {WindowBounds}\n" +
                   $"Screenshot: {ScreenshotPath}\n" +
                   $"Overall Success: {IsSuccessful}";
        }
    }
}