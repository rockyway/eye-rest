using System;
using System.Threading.Tasks;
using Xunit.Abstractions;

namespace EyeRest.Tests.Integration
{
    /// <summary>
    /// Simple runner to demonstrate the 2-day simulation test execution time
    /// </summary>
    public class TwoDaySimulationRunner
    {
        public static async Task<TimeSpan> RunSimulationTest()
        {
            var startTime = DateTime.Now;
            
            // Create a mock test output helper
            var output = new TestOutputHelper();
            
            try
            {
                // Create and run the simulation test
                using var test = new TwoDayUserSimulationTests(output);
                await test.TwoDayUserSimulation_ShouldHandleAllScenarios();
                
                var endTime = DateTime.Now;
                var duration = endTime - startTime;
                
                Console.WriteLine($"✅ 2-Day Simulation Test completed successfully!");
                Console.WriteLine($"Execution time: {duration.TotalSeconds:F2} seconds");
                Console.WriteLine($"Virtual time simulated: 2 days (48 hours)");
                Console.WriteLine($"Time acceleration factor: {(48 * 3600) / duration.TotalSeconds:F0}x");
                
                return duration;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"❌ Test failed: {ex.Message}");
                throw;
            }
        }
    }
    
    /// <summary>
    /// Mock implementation of ITestOutputHelper for standalone testing
    /// </summary>
    public class TestOutputHelper : ITestOutputHelper
    {
        public void WriteLine(string message)
        {
            Console.WriteLine($"[TEST] {message}");
        }

        public void WriteLine(string format, params object[] args)
        {
            Console.WriteLine($"[TEST] {string.Format(format, args)}");
        }
    }
}