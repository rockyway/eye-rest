using System.Threading.Tasks;
using Xunit;
using Xunit.Abstractions;

namespace EyeRest.Tests.E2E
{
    /// <summary>
    /// Main E2E test execution class that runs the complete test plan
    /// </summary>
    public class ExecuteE2ETests
    {
        private readonly ITestOutputHelper _output;

        public ExecuteE2ETests(ITestOutputHelper output)
        {
            _output = output;
        }

        [Fact]
        public async Task ExecuteCompleteE2ETestPlan()
        {
            _output.WriteLine("🎯 EXECUTING COMPLETE E2E TEST PLAN FOR EYE-REST APPLICATION");
            _output.WriteLine("================================================================");

            var testRunner = new E2ETestRunner(_output);
            var report = await testRunner.ExecuteTestPlanAsync();

            // Assert overall success
            Assert.True(report.SuccessRate >= 80, 
                $"E2E test success rate {report.SuccessRate:F1}% is below acceptable threshold of 80%");

            // Assert critical performance requirements
            Assert.True(report.PerformanceMetrics.StartupTimePassed, 
                "Startup time requirement (<3 seconds) not met");
            
            Assert.True(report.PerformanceMetrics.MemoryUsagePassed, 
                "Memory usage requirement (<50MB) not met");

            // Assert no critical failures
            var criticalFailures = report.TestResults.Where(r => 
                r.Status == TestStatus.Failed && 
                (r.TestName.Contains("TC001") || // Application startup
                 r.TestName.Contains("TC027") || // Startup time
                 r.TestName.Contains("TC028"))); // Memory usage

            Assert.Empty(criticalFailures);

            _output.WriteLine($"\n🎉 E2E TEST PLAN EXECUTION COMPLETED SUCCESSFULLY!");
            _output.WriteLine($"📊 Final Score: {report.SuccessRate:F1}% ({report.PassedTests}/{report.TotalTests} tests passed)");
        }
    }
}