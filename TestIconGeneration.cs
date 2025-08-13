using System;
using System.Drawing;
using System.IO;
using EyeRest.Services;
using Microsoft.Extensions.Logging;

namespace EyeRest.Tests
{
    /// <summary>
    /// Test utility to verify icon generation for all states
    /// </summary>
    public class TestIconGeneration
    {
        public static void Main()
        {
            // Create a simple logger
            var loggerFactory = LoggerFactory.Create(builder => builder.AddConsole());
            var logger = loggerFactory.CreateLogger<IconService>();
            
            // Create IconService
            var iconService = new IconService(logger);
            
            // Test each icon state
            var states = Enum.GetValues<TrayIconState>();
            
            Console.WriteLine("Testing icon generation for all states:");
            Console.WriteLine("=====================================");
            
            foreach (var state in states)
            {
                try
                {
                    Console.Write($"Testing {state}...");
                    var icon = iconService.GetIconForState(state);
                    
                    if (icon != null)
                    {
                        // Save icon to file for visual inspection
                        var fileName = $"test_icon_{state}.ico";
                        using (var fs = new FileStream(fileName, FileMode.Create))
                        {
                            icon.Save(fs);
                        }
                        Console.WriteLine($" ✓ Success - saved as {fileName}");
                    }
                    else
                    {
                        Console.WriteLine(" ✗ Failed - null icon returned");
                    }
                }
                catch (Exception ex)
                {
                    Console.WriteLine($" ✗ Error: {ex.Message}");
                }
            }
            
            // Test caching
            Console.WriteLine("\nTesting icon caching:");
            Console.WriteLine("====================");
            
            var icon1 = iconService.GetIconForState(TrayIconState.Active);
            var icon2 = iconService.GetIconForState(TrayIconState.Active);
            
            if (ReferenceEquals(icon1, icon2))
            {
                Console.WriteLine("✓ Icons are properly cached (same reference)");
            }
            else
            {
                Console.WriteLine("✗ Icons are not cached (different references)");
            }
            
            // Cleanup
            iconService.Dispose();
            
            Console.WriteLine("\nTest complete! Check the generated .ico files.");
        }
    }
}