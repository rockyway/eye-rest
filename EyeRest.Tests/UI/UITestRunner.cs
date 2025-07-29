using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using NUnit.Framework;
using NUnit.Framework.Api;
using NUnit.Framework.Interfaces;

namespace EyeRest.Tests.UI
{
    /// <summary>
    /// Automated UI test execution framework with comprehensive reporting
    /// Provides test discovery, execution, and detailed reporting capabilities
    /// </summary>
    public class UITestRunner
    {
        private readonly List<UITestResult> _testResults = new();
        private readonly Stopwatch _totalExecutionTime = new();
        private readonly string _reportOutputPath;

        public UITestRunner()
        {
            _reportOutputPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "TestReports");
            Directory.CreateDirectory(_reportOutputPath);
        }

        /// <summary>
        /// Executes all UI tests and generates comprehensive reports
        /// </summary>
        public async Task<UITestExecutionSummary> ExecuteAllUITestsAsync()
        {
            Console.WriteLine("🚀 Starting comprehensive UI test execution...");
            _totalExecutionTime.Start();

            var summary = new UITestExecutionSummary
            {
                ExecutionStartTime = DateTime.Now,
                TestResults = new List<UITestResult>()
            };

            try
            {
                // Discover and execute UI tests
                await ExecuteTestsInClass<ComprehensiveUITests>();

                _totalExecutionTime.Stop();
                summary.ExecutionEndTime = DateTime.Now;
                summary.TotalExecutionTime = _totalExecutionTime.Elapsed;
                summary.TestResults = _testResults.ToList();

                // Generate reports
                await GenerateHtmlReportAsync(summary);
                GenerateConsoleReport(summary);

                Console.WriteLine($"✅ UI test execution completed in {_totalExecutionTime.Elapsed:mm\\:ss}");
                return summary;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"❌ Error during UI test execution: {ex.Message}");
                summary.ExecutionError = ex.Message;
                return summary;
            }
        }

        /// <summary>
        /// Executes tests in a specific test class using reflection
        /// </summary>
        private async Task ExecuteTestsInClass<T>() where T : class, new()
        {
            var testClass = typeof(T);
            var testMethods = testClass.GetMethods()
                .Where(m => m.GetCustomAttribute<TestAttribute>() != null)
                .ToArray();

            Console.WriteLine($"📋 Found {testMethods.Length} UI tests in {testClass.Name}");

            foreach (var testMethod in testMethods)
            {
                await ExecuteIndividualTest<T>(testMethod);
            }
        }

        /// <summary>
        /// Executes an individual test method with proper setup/teardown
        /// </summary>
        private async Task ExecuteIndividualTest<T>(MethodInfo testMethod) where T : class, new()
        {
            var testResult = new UITestResult
            {
                TestName = testMethod.Name,
                TestClass = typeof(T).Name,
                StartTime = DateTime.Now
            };

            Console.WriteLine($"🧪 Executing: {testResult.TestName}");

            try
            {
                var testInstance = new T();
                var testStopwatch = Stopwatch.StartNew();

                // Execute SetUp if present
                var setupMethod = typeof(T).GetMethod("SetUp");
                if (setupMethod != null)
                {
                    if (setupMethod.ReturnType == typeof(Task))
                        await (Task)setupMethod.Invoke(testInstance, null)!;
                    else
                        setupMethod.Invoke(testInstance, null);
                }

                // Execute the actual test
                try
                {
                    if (testMethod.ReturnType == typeof(Task))
                        await (Task)testMethod.Invoke(testInstance, null)!;
                    else
                        testMethod.Invoke(testInstance, null);

                    testResult.Status = UITestStatus.Passed;
                    testResult.Message = "Test completed successfully";
                }
                catch (Exception testEx)
                {
                    testResult.Status = UITestStatus.Failed;
                    testResult.Message = testEx.InnerException?.Message ?? testEx.Message;
                    testResult.StackTrace = testEx.InnerException?.StackTrace ?? testEx.StackTrace;
                }

                // Execute TearDown if present
                try
                {
                    var tearDownMethod = typeof(T).GetMethod("TearDown");
                    if (tearDownMethod != null)
                    {
                        if (tearDownMethod.ReturnType == typeof(Task))
                            await (Task)tearDownMethod.Invoke(testInstance, null)!;
                        else
                            tearDownMethod.Invoke(testInstance, null);
                    }
                }
                catch (Exception tearDownEx)
                {
                    testResult.TearDownError = tearDownEx.Message;
                }

                testStopwatch.Stop();
                testResult.ExecutionTime = testStopwatch.Elapsed;
                testResult.EndTime = DateTime.Now;

                var statusIcon = testResult.Status == UITestStatus.Passed ? "✅" : "❌";
                Console.WriteLine($"{statusIcon} {testResult.TestName}: {testResult.Status} ({testResult.ExecutionTime.TotalSeconds:F1}s)");

                if (testResult.Status == UITestStatus.Failed)
                {
                    Console.WriteLine($"   Error: {testResult.Message}");
                }
            }
            catch (Exception ex)
            {
                testResult.Status = UITestStatus.Error;
                testResult.Message = $"Test execution error: {ex.Message}";
                testResult.StackTrace = ex.StackTrace;
                testResult.ExecutionTime = TimeSpan.Zero;
                testResult.EndTime = DateTime.Now;

                Console.WriteLine($"💥 {testResult.TestName}: EXECUTION ERROR - {ex.Message}");
            }

            _testResults.Add(testResult);
        }

        /// <summary>
        /// Generates a comprehensive HTML test report
        /// </summary>
        private async Task GenerateHtmlReportAsync(UITestExecutionSummary summary)
        {
            var reportPath = Path.Combine(_reportOutputPath, $"UI_Test_Report_{DateTime.Now:yyyyMMdd_HHmmss}.html");
            
            var html = GenerateHtmlReport(summary);
            await File.WriteAllTextAsync(reportPath, html);
            
            Console.WriteLine($"📊 HTML report generated: {reportPath}");
        }

        /// <summary>
        /// Generates HTML report content
        /// </summary>
        private string GenerateHtmlReport(UITestExecutionSummary summary)
        {
            var passedTests = summary.TestResults.Count(t => t.Status == UITestStatus.Passed);
            var failedTests = summary.TestResults.Count(t => t.Status == UITestStatus.Failed);
            var errorTests = summary.TestResults.Count(t => t.Status == UITestStatus.Error);
            var totalTests = summary.TestResults.Count;
            var successRate = totalTests > 0 ? (double)passedTests / totalTests * 100 : 0;

            return $@"<!DOCTYPE html>
<html lang=""en"">
<head>
    <meta charset=""UTF-8"">
    <meta name=""viewport"" content=""width=device-width, initial-scale=1.0"">
    <title>EyeRest UI Test Report</title>
    <style>
        body {{ font-family: Arial, sans-serif; margin: 20px; background-color: #f5f5f5; }}
        .container {{ max-width: 1200px; margin: 0 auto; background: white; padding: 20px; border-radius: 8px; box-shadow: 0 2px 10px rgba(0,0,0,0.1); }}
        .header {{ text-align: center; border-bottom: 2px solid #333; padding-bottom: 20px; margin-bottom: 30px; }}
        .summary {{ display: grid; grid-template-columns: repeat(auto-fit, minmax(200px, 1fr)); gap: 20px; margin-bottom: 30px; }}
        .summary-card {{ background: #f8f9fa; padding: 15px; border-radius: 6px; text-align: center; border-left: 4px solid #007bff; }}
        .summary-card.passed {{ border-left-color: #28a745; }}
        .summary-card.failed {{ border-left-color: #dc3545; }}
        .summary-card.error {{ border-left-color: #ffc107; }}
        .test-results {{ margin-top: 20px; }}
        .test-item {{ background: white; border: 1px solid #ddd; margin-bottom: 10px; border-radius: 6px; overflow: hidden; }}
        .test-header {{ padding: 15px; background: #f8f9fa; display: flex; justify-content: space-between; align-items: center; }}
        .test-header.passed {{ background: #d4edda; }}
        .test-header.failed {{ background: #f8d7da; }}
        .test-header.error {{ background: #fff3cd; }}
        .test-details {{ padding: 15px; display: none; }}
        .test-details.show {{ display: block; }}
        .status-badge {{ padding: 4px 8px; border-radius: 4px; color: white; font-size: 12px; }}
        .status-passed {{ background: #28a745; }}
        .status-failed {{ background: #dc3545; }}
        .status-error {{ background: #ffc107; color: #212529; }}
        .progress-bar {{ width: 100%; height: 20px; background: #e9ecef; border-radius: 10px; overflow: hidden; margin: 10px 0; }}
        .progress-fill {{ height: 100%; background: linear-gradient(90deg, #28a745, #20c997); transition: width 0.3s ease; }}
        .error-details {{ background: #f8f9fa; padding: 10px; border-radius: 4px; margin-top: 10px; font-family: monospace; font-size: 12px; }}
        .toggle-btn {{ background: none; border: none; color: #007bff; cursor: pointer; }}
    </style>
    <script>
        function toggleDetails(testId) {{
            const details = document.getElementById('details-' + testId);
            const btn = document.getElementById('btn-' + testId);
            if (details.classList.contains('show')) {{
                details.classList.remove('show');
                btn.textContent = '▶ Show Details';
            }} else {{
                details.classList.add('show');
                btn.textContent = '▼ Hide Details';
            }}
        }}
    </script>
</head>
<body>
    <div class=""container"">
        <div class=""header"">
            <h1>🖥️ EyeRest UI Test Report</h1>
            <p>Generated on {summary.ExecutionStartTime:yyyy-MM-dd HH:mm:ss}</p>
        </div>

        <div class=""summary"">
            <div class=""summary-card"">
                <h3>Total Tests</h3>
                <div style=""font-size: 2em; font-weight: bold;"">{totalTests}</div>
            </div>
            <div class=""summary-card passed"">
                <h3>✅ Passed</h3>
                <div style=""font-size: 2em; font-weight: bold; color: #28a745;"">{passedTests}</div>
            </div>
            <div class=""summary-card failed"">
                <h3>❌ Failed</h3>
                <div style=""font-size: 2em; font-weight: bold; color: #dc3545;"">{failedTests}</div>
            </div>
            <div class=""summary-card error"">
                <h3>💥 Errors</h3>
                <div style=""font-size: 2em; font-weight: bold; color: #ffc107;"">{errorTests}</div>
            </div>
        </div>

        <div class=""summary-card"">
            <h3>Success Rate</h3>
            <div style=""font-size: 1.5em; font-weight: bold;"">{successRate:F1}%</div>
            <div class=""progress-bar"">
                <div class=""progress-fill"" style=""width: {successRate}%""></div>
            </div>
            <p>Execution Time: {summary.TotalExecutionTime:mm\\:ss}</p>
        </div>

        <div class=""test-results"">
            <h2>📋 Test Results</h2>
            {string.Join("", summary.TestResults.Select((test, index) => $@"
            <div class=""test-item"">
                <div class=""test-header {test.Status.ToString().ToLower()}"">
                    <div>
                        <strong>{test.TestName}</strong>
                        <span class=""status-badge status-{test.Status.ToString().ToLower()}"">{test.Status}</span>
                    </div>
                    <div>
                        <span>{test.ExecutionTime.TotalSeconds:F1}s</span>
                        <button class=""toggle-btn"" id=""btn-{index}"" onclick=""toggleDetails({index})"">▶ Show Details</button>
                    </div>
                </div>
                <div class=""test-details"" id=""details-{index}"">
                    <p><strong>Test Class:</strong> {test.TestClass}</p>
                    <p><strong>Start Time:</strong> {test.StartTime:HH:mm:ss}</p>
                    <p><strong>End Time:</strong> {test.EndTime:HH:mm:ss}</p>
                    <p><strong>Execution Time:</strong> {test.ExecutionTime.TotalSeconds:F2} seconds</p>
                    {(string.IsNullOrEmpty(test.Message) ? "" : $"<p><strong>Message:</strong> {test.Message}</p>")}
                    {(string.IsNullOrEmpty(test.StackTrace) ? "" : $"<div class=\"error-details\"><strong>Stack Trace:</strong><pre>{test.StackTrace}</pre></div>")}
                    {(string.IsNullOrEmpty(test.TearDownError) ? "" : $"<p><strong>TearDown Error:</strong> {test.TearDownError}</p>")}
                </div>
            </div>
            "))}
        </div>

        <div style=""margin-top: 40px; text-align: center; color: #666; border-top: 1px solid #ddd; padding-top: 20px;"">
            <p>Report generated by EyeRest UI Test Framework</p>
            <p>Total execution time: {summary.TotalExecutionTime:mm\\:ss}</p>
        </div>
    </div>
</body>
</html>";
        }

        /// <summary>
        /// Generates a console-friendly test report
        /// </summary>
        private void GenerateConsoleReport(UITestExecutionSummary summary)
        {
            Console.WriteLine("\n" + new string('=', 60));
            Console.WriteLine("📊 UI TEST EXECUTION SUMMARY");
            Console.WriteLine(new string('=', 60));

            var passedTests = summary.TestResults.Count(t => t.Status == UITestStatus.Passed);
            var failedTests = summary.TestResults.Count(t => t.Status == UITestStatus.Failed);
            var errorTests = summary.TestResults.Count(t => t.Status == UITestStatus.Error);
            var totalTests = summary.TestResults.Count;
            var successRate = totalTests > 0 ? (double)passedTests / totalTests * 100 : 0;

            Console.WriteLine($"🕐 Execution Time: {summary.TotalExecutionTime:mm\\:ss}");
            Console.WriteLine($"📋 Total Tests: {totalTests}");
            Console.WriteLine($"✅ Passed: {passedTests}");
            Console.WriteLine($"❌ Failed: {failedTests}");
            Console.WriteLine($"💥 Errors: {errorTests}");
            Console.WriteLine($"📈 Success Rate: {successRate:F1}%");

            if (failedTests > 0 || errorTests > 0)
            {
                Console.WriteLine("\n" + new string('-', 40));
                Console.WriteLine("❌ FAILED/ERROR TESTS:");
                Console.WriteLine(new string('-', 40));

                foreach (var test in summary.TestResults.Where(t => t.Status != UITestStatus.Passed))
                {
                    var icon = test.Status == UITestStatus.Failed ? "❌" : "💥";
                    Console.WriteLine($"{icon} {test.TestName}");
                    Console.WriteLine($"   {test.Message}");
                    if (!string.IsNullOrEmpty(test.TearDownError))
                    {
                        Console.WriteLine($"   TearDown Error: {test.TearDownError}");
                    }
                    Console.WriteLine();
                }
            }

            Console.WriteLine(new string('=', 60));
        }
    }

    /// <summary>
    /// Represents the result of a single UI test execution
    /// </summary>
    public class UITestResult
    {
        public string TestName { get; set; } = string.Empty;
        public string TestClass { get; set; } = string.Empty;
        public DateTime StartTime { get; set; }
        public DateTime EndTime { get; set; }
        public TimeSpan ExecutionTime { get; set; }
        public UITestStatus Status { get; set; }
        public string Message { get; set; } = string.Empty;
        public string StackTrace { get; set; } = string.Empty;
        public string TearDownError { get; set; } = string.Empty;
    }

    /// <summary>
    /// Overall summary of UI test execution
    /// </summary>
    public class UITestExecutionSummary
    {
        public DateTime ExecutionStartTime { get; set; }
        public DateTime ExecutionEndTime { get; set; }
        public TimeSpan TotalExecutionTime { get; set; }
        public List<UITestResult> TestResults { get; set; } = new();
        public string ExecutionError { get; set; } = string.Empty;

        public int TotalTests => TestResults.Count;
        public int PassedTests => TestResults.Count(t => t.Status == UITestStatus.Passed);
        public int FailedTests => TestResults.Count(t => t.Status == UITestStatus.Failed);
        public int ErrorTests => TestResults.Count(t => t.Status == UITestStatus.Error);
        public double SuccessRate => TotalTests > 0 ? (double)PassedTests / TotalTests * 100 : 0;
    }

    /// <summary>
    /// UI test execution status enumeration
    /// </summary>
    public enum UITestStatus
    {
        Passed,
        Failed,
        Error
    }
}