using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading.Tasks;
using Xunit;
using Xunit.Abstractions;

namespace EyeRest.Tests.E2E
{
    /// <summary>
    /// Critical Fixes Test Runner
    /// Executes comprehensive end-to-end testing of all the critical fixes
    /// Provides detailed reporting and evidence collection
    /// </summary>
    public class CriticalFixesTestRunner
    {
        private readonly ITestOutputHelper _output;
        private readonly List<CriticalFixTestResult> _testResults = new();

        public CriticalFixesTestRunner(ITestOutputHelper output)
        {
            _output = output;
        }

        public async Task<CriticalFixesTestReport> ExecuteAllCriticalFixesTestsAsync()
        {
            _output.WriteLine("🚀 EXECUTING CRITICAL FIXES VALIDATION TEST SUITE");
            _output.WriteLine(new string('=', 80));
            _output.WriteLine($"📅 Test Execution Started: {DateTime.Now:yyyy-MM-dd HH:mm:ss}");
            _output.WriteLine("");

            var overallStopwatch = Stopwatch.StartNew();

            try
            {
                // Initialize test infrastructure
                using var testSuite = new CriticalFixesValidationTests(_output);

                // Execute each critical fix test
                await ExecuteCriticalFixTest("CRITICAL_FIX_001", 
                    "Timer Auto-Start Functionality", 
                    "Verify timers start automatically when app opens",
                    () => testSuite.CRITICAL_FIX_001_TimerAutoStart_ShouldStartAutomaticallyOnApplicationLaunch());

                await ExecuteCriticalFixTest("CRITICAL_FIX_002", 
                    "Default Eye Rest Settings", 
                    "Verify defaults are 20 minutes interval / 20 seconds duration",
                    () => testSuite.CRITICAL_FIX_002_DefaultEyeRestSettings_ShouldBe20Minutes20Seconds());

                await ExecuteCriticalFixTest("CRITICAL_FIX_003", 
                    "Dual Countdown Display", 
                    "Verify UI shows both countdowns on same line",
                    () => testSuite.CRITICAL_FIX_003_DualCountdownDisplay_ShouldShowBothTimersOnSameLine());

                await ExecuteCriticalFixTest("CRITICAL_FIX_004", 
                    "Eye Rest Warning Popup", 
                    "Test warning popup appears 30 seconds before eye rest",
                    () => testSuite.CRITICAL_FIX_004_EyeRestWarningPopup_ShouldAppear30SecondsBeforeEvent());

                await ExecuteCriticalFixTest("CRITICAL_FIX_005", 
                    "Eye Rest Popup Display", 
                    "Test full-screen eye rest popup appears when time is up",
                    () => testSuite.CRITICAL_FIX_005_EyeRestPopupDisplay_ShouldShowFullScreenPopup());

                await ExecuteCriticalFixTest("CRITICAL_FIX_006", 
                    "End-to-End Timer Flow", 
                    "Test complete timer cycle from start to eye rest",
                    () => testSuite.CRITICAL_FIX_006_EndToEndTimerFlow_ShouldExecuteCompleteSequence());

                await ExecuteCriticalFixTest("CRITICAL_FIX_007", 
                    "Performance Validation", 
                    "Validate startup time and memory usage",
                    () => testSuite.CRITICAL_FIX_007_PerformanceValidation_StartupAndMemoryUsage());

                overallStopwatch.Stop();

                // Generate comprehensive report
                var report = GenerateCriticalFixesReport(overallStopwatch.Elapsed);
                PrintDetailedReport(report);

                return report;
            }
            catch (Exception ex)
            {
                _output.WriteLine($"❌ CRITICAL ERROR during test execution: {ex.Message}");
                _output.WriteLine($"Stack Trace: {ex.StackTrace}");
                throw;
            }
        }

        private async Task ExecuteCriticalFixTest(string testId, string testName, string description, Func<Task> testMethod)
        {
            _output.WriteLine($"\n🧪 {testId}: {testName}");
            _output.WriteLine($"📋 Description: {description}");
            _output.WriteLine(new string('-', 60));

            var stopwatch = Stopwatch.StartNew();
            var result = new CriticalFixTestResult
            {
                TestId = testId,
                TestName = testName,
                Description = description,
                StartTime = DateTime.Now
            };

            try
            {
                await testMethod();
                stopwatch.Stop();

                result.Status = CriticalFixTestStatus.Passed;
                result.ExecutionTime = stopwatch.Elapsed;
                result.EndTime = DateTime.Now;

                _output.WriteLine($"✅ {testId} PASSED - {stopwatch.ElapsedMilliseconds}ms");
                _output.WriteLine($"🎯 Result: All assertions passed successfully");
            }
            catch (Exception ex)
            {
                stopwatch.Stop();

                result.Status = CriticalFixTestStatus.Failed;
                result.ExecutionTime = stopwatch.Elapsed;
                result.EndTime = DateTime.Now;
                result.ErrorMessage = ex.Message;
                result.Exception = ex;
                result.StackTrace = ex.StackTrace;

                _output.WriteLine($"❌ {testId} FAILED - {stopwatch.ElapsedMilliseconds}ms");
                _output.WriteLine($"💥 Error: {ex.Message}");
                
                if (ex.InnerException != null)
                {
                    _output.WriteLine($"🔍 Inner Exception: {ex.InnerException.Message}");
                }
            }

            _testResults.Add(result);
            _output.WriteLine("");
        }

        private CriticalFixesTestReport GenerateCriticalFixesReport(TimeSpan totalExecutionTime)
        {
            var report = new CriticalFixesTestReport
            {
                ExecutionDate = DateTime.Now,
                TotalExecutionTime = totalExecutionTime,
                TestResults = _testResults.ToList(),
                TotalTests = _testResults.Count,
                PassedTests = _testResults.Count(r => r.Status == CriticalFixTestStatus.Passed),
                FailedTests = _testResults.Count(r => r.Status == CriticalFixTestStatus.Failed),
                SuccessRate = _testResults.Count > 0 ? 
                    (_testResults.Count(r => r.Status == CriticalFixTestStatus.Passed) * 100.0 / _testResults.Count) : 0
            };

            // Analyze specific critical fix compliance
            report.CriticalFixCompliance = new CriticalFixCompliance
            {
                TimerAutoStartWorks = _testResults.Any(t => t.TestId == "CRITICAL_FIX_001" && t.Status == CriticalFixTestStatus.Passed),
                DefaultSettingsCorrect = _testResults.Any(t => t.TestId == "CRITICAL_FIX_002" && t.Status == CriticalFixTestStatus.Passed),
                DualCountdownDisplayWorks = _testResults.Any(t => t.TestId == "CRITICAL_FIX_003" && t.Status == CriticalFixTestStatus.Passed),
                WarningPopupWorks = _testResults.Any(t => t.TestId == "CRITICAL_FIX_004" && t.Status == CriticalFixTestStatus.Passed),
                EyeRestPopupWorks = _testResults.Any(t => t.TestId == "CRITICAL_FIX_005" && t.Status == CriticalFixTestStatus.Passed),
                EndToEndFlowWorks = _testResults.Any(t => t.TestId == "CRITICAL_FIX_006" && t.Status == CriticalFixTestStatus.Passed),
                PerformanceRequirementsMet = _testResults.Any(t => t.TestId == "CRITICAL_FIX_007" && t.Status == CriticalFixTestStatus.Passed)
            };

            // Calculate overall compliance score
            var complianceFields = typeof(CriticalFixCompliance).GetProperties();
            var passedCompliance = complianceFields.Count(prop => (bool)prop.GetValue(report.CriticalFixCompliance)!);
            report.CriticalFixCompliance.OverallComplianceScore = (passedCompliance * 100.0) / complianceFields.Length;

            return report;
        }

        private void PrintDetailedReport(CriticalFixesTestReport report)
        {
            _output.WriteLine(new string('=', 80));
            _output.WriteLine("📊 CRITICAL FIXES VALIDATION TEST REPORT");
            _output.WriteLine(new string('=', 80));

            // Executive Summary
            _output.WriteLine("\n🎯 EXECUTIVE SUMMARY:");
            _output.WriteLine($"📅 Execution Date: {report.ExecutionDate:yyyy-MM-dd HH:mm:ss}");
            _output.WriteLine($"⏱️ Total Execution Time: {report.TotalExecutionTime.TotalSeconds:F2} seconds");
            _output.WriteLine($"🧪 Total Tests: {report.TotalTests}");
            _output.WriteLine($"✅ Passed: {report.PassedTests}");
            _output.WriteLine($"❌ Failed: {report.FailedTests}");
            _output.WriteLine($"📈 Success Rate: {report.SuccessRate:F1}%");

            // Critical Fix Compliance Analysis
            _output.WriteLine("\n🔍 CRITICAL FIX COMPLIANCE ANALYSIS:");
            _output.WriteLine($"   🔄 Timer Auto-Start: {(report.CriticalFixCompliance.TimerAutoStartWorks ? "✅ WORKING" : "❌ FAILED")}");
            _output.WriteLine($"   ⚙️ Default Settings: {(report.CriticalFixCompliance.DefaultSettingsCorrect ? "✅ CORRECT" : "❌ INCORRECT")}");
            _output.WriteLine($"   📱 Dual Countdown Display: {(report.CriticalFixCompliance.DualCountdownDisplayWorks ? "✅ WORKING" : "❌ FAILED")}");
            _output.WriteLine($"   ⚠️ Warning Popup: {(report.CriticalFixCompliance.WarningPopupWorks ? "✅ WORKING" : "❌ FAILED")}");
            _output.WriteLine($"   🖥️ Eye Rest Popup: {(report.CriticalFixCompliance.EyeRestPopupWorks ? "✅ WORKING" : "❌ FAILED")}");
            _output.WriteLine($"   🔄 End-to-End Flow: {(report.CriticalFixCompliance.EndToEndFlowWorks ? "✅ WORKING" : "❌ FAILED")}");
            _output.WriteLine($"   ⚡ Performance: {(report.CriticalFixCompliance.PerformanceRequirementsMet ? "✅ MEETS REQUIREMENTS" : "❌ BELOW REQUIREMENTS")}");
            _output.WriteLine($"\n🏆 Overall Compliance Score: {report.CriticalFixCompliance.OverallComplianceScore:F1}%");

            // Detailed Test Results
            _output.WriteLine("\n📋 DETAILED TEST RESULTS:");
            foreach (var test in report.TestResults)
            {
                var status = test.Status == CriticalFixTestStatus.Passed ? "✅ PASS" : "❌ FAIL";
                _output.WriteLine($"   {test.TestId}: {status} ({test.ExecutionTime.TotalMilliseconds:F0}ms)");
                _output.WriteLine($"      📝 {test.TestName}");
                _output.WriteLine($"      📄 {test.Description}");
                
                if (test.Status == CriticalFixTestStatus.Failed)
                {
                    _output.WriteLine($"      💥 Error: {test.ErrorMessage}");
                }
                _output.WriteLine("");
            }

            // Failed Tests Analysis
            if (report.FailedTests > 0)
            {
                _output.WriteLine("\n❌ FAILED TESTS DETAILED ANALYSIS:");
                foreach (var failedTest in report.TestResults.Where(r => r.Status == CriticalFixTestStatus.Failed))
                {
                    _output.WriteLine($"\n🔴 {failedTest.TestId} - {failedTest.TestName}");
                    _output.WriteLine($"   💥 Error Message: {failedTest.ErrorMessage}");
                    _output.WriteLine($"   ⏱️ Execution Time: {failedTest.ExecutionTime.TotalMilliseconds:F0}ms");
                    
                    if (!string.IsNullOrEmpty(failedTest.StackTrace))
                    {
                        var topStackLine = failedTest.StackTrace.Split('\n').FirstOrDefault()?.Trim();
                        _output.WriteLine($"   📍 Location: {topStackLine}");
                    }
                    
                    if (failedTest.Exception?.InnerException != null)
                    {
                        _output.WriteLine($"   🔍 Inner Exception: {failedTest.Exception.InnerException.Message}");
                    }
                }
            }

            // Performance Analysis
            _output.WriteLine("\n⚡ PERFORMANCE ANALYSIS:");
            var avgExecutionTime = report.TestResults.Average(t => t.ExecutionTime.TotalMilliseconds);
            var maxExecutionTime = report.TestResults.Max(t => t.ExecutionTime.TotalMilliseconds);
            var minExecutionTime = report.TestResults.Min(t => t.ExecutionTime.TotalMilliseconds);
            
            _output.WriteLine($"   📊 Average Test Time: {avgExecutionTime:F0}ms");
            _output.WriteLine($"   🔝 Longest Test: {maxExecutionTime:F0}ms");
            _output.WriteLine($"   ⚡ Fastest Test: {minExecutionTime:F0}ms");
            _output.WriteLine($"   🎯 Total Suite Time: {report.TotalExecutionTime.TotalSeconds:F2}s");

            // Overall Assessment
            var overallStatus = report.SuccessRate == 100 ? "🎉 EXCELLENT - ALL FIXES VERIFIED" : 
                               report.SuccessRate >= 85 ? "✅ GOOD - MINOR ISSUES FOUND" : 
                               report.SuccessRate >= 70 ? "⚠️ NEEDS ATTENTION - SEVERAL ISSUES" : "❌ CRITICAL - MAJOR PROBLEMS";

            _output.WriteLine($"\n🏆 OVERALL CRITICAL FIXES STATUS: {overallStatus}");
            _output.WriteLine($"🎯 Compliance Score: {report.CriticalFixCompliance.OverallComplianceScore:F1}%");
            
            if (report.SuccessRate == 100)
            {
                _output.WriteLine("🎊 ALL CRITICAL FIXES VERIFIED! The application is ready for production use.");
            }
            else
            {
                _output.WriteLine($"⚠️ {report.FailedTests} critical fix(es) failed. Issues must be addressed before release.");
            }
            
            _output.WriteLine(new string('=', 80));
        }
    }

    public class CriticalFixesTestReport
    {
        public DateTime ExecutionDate { get; set; }
        public TimeSpan TotalExecutionTime { get; set; }
        public List<CriticalFixTestResult> TestResults { get; set; } = new();
        public int TotalTests { get; set; }
        public int PassedTests { get; set; }
        public int FailedTests { get; set; }
        public double SuccessRate { get; set; }
        public CriticalFixCompliance CriticalFixCompliance { get; set; } = new();
    }

    public class CriticalFixTestResult
    {
        public string TestId { get; set; } = string.Empty;
        public string TestName { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public CriticalFixTestStatus Status { get; set; }
        public TimeSpan ExecutionTime { get; set; }
        public DateTime StartTime { get; set; }
        public DateTime EndTime { get; set; }
        public string? ErrorMessage { get; set; }
        public Exception? Exception { get; set; }
        public string? StackTrace { get; set; }
    }

    public class CriticalFixCompliance
    {
        public bool TimerAutoStartWorks { get; set; }
        public bool DefaultSettingsCorrect { get; set; }
        public bool DualCountdownDisplayWorks { get; set; }
        public bool WarningPopupWorks { get; set; }
        public bool EyeRestPopupWorks { get; set; }
        public bool EndToEndFlowWorks { get; set; }
        public bool PerformanceRequirementsMet { get; set; }
        public double OverallComplianceScore { get; set; }
    }

    public enum CriticalFixTestStatus
    {
        Passed,
        Failed,
        Skipped
    }
}