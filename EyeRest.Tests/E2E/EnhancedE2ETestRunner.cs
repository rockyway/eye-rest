using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading.Tasks;
using Xunit;
using Xunit.Abstractions;

namespace EyeRest.Tests.E2E
{
    public class EnhancedE2ETestRunner
    {
        private readonly ITestOutputHelper _output;
        private readonly List<TestResult> _testResults = new();

        public EnhancedE2ETestRunner(ITestOutputHelper output)
        {
            _output = output;
        }

        public async Task<EnhancedE2EReport> ExecuteUIValidationTestsAsync()
        {
            _output.WriteLine("🚀 EXECUTING ENHANCED E2E UI VALIDATION TESTS");
            _output.WriteLine(new string('=', 60));

            var overallStopwatch = Stopwatch.StartNew();
            var uiTests = new UIValidationTests(_output);

            try
            {
                // Execute UI-specific validation tests
                _output.WriteLine("\n📋 PHASE 1: UI Default Values Validation");
                await ExecuteTestWithReporting("TC_UI_001", "Default Values Display", 
                    () => uiTests.TC_UI_001_DefaultValues_DisplayCorrectly());

                _output.WriteLine("\n📋 PHASE 2: Configuration Auto-Save");
                await ExecuteTestWithReporting("TC_UI_002", "Auto-Save Persistence", 
                    () => uiTests.TC_UI_002_ConfigurationPersistence_AutoSavesCorrectly());

                _output.WriteLine("\n📋 PHASE 3: Restore Defaults Functionality");
                await ExecuteTestWithReporting("TC_UI_003", "Restore Defaults", 
                    () => uiTests.TC_UI_003_RestoreDefaults_ResetsAllValues());

                _output.WriteLine("\n📋 PHASE 4: Input Validation");
                await ExecuteTestWithReporting("TC_UI_004", "Validation Errors", 
                    () => uiTests.TC_UI_004_ValidationRules_ShowErrorMessages());

                _output.WriteLine("\n📋 PHASE 5: Real-time Auto-Save");
                await ExecuteTestWithReporting("TC_UI_005", "Real-time Auto-Save", 
                    () => uiTests.TC_UI_005_AutoSave_WorksInRealTime());

                _output.WriteLine("\n📋 PHASE 6: Timer Commands");
                await ExecuteTestWithReporting("TC_UI_006", "Timer Commands", 
                    () => uiTests.TC_UI_006_TimerCommands_ExecuteCorrectly());

                _output.WriteLine("\n📋 PHASE 7: Requirements Compliance");
                await ExecuteTestWithReporting("TC_UI_007", "Requirements Compliance", 
                    () => uiTests.TC_UI_007_RequirementsCompliance_AllRequirementsMet());

                overallStopwatch.Stop();

                // Generate comprehensive report
                var report = GenerateEnhancedReport(overallStopwatch.Elapsed);
                PrintEnhancedReport(report);

                return report;
            }
            finally
            {
                uiTests.Dispose();
            }
        }

        private async Task ExecuteTestWithReporting(string testId, string testName, Func<Task> testMethod)
        {
            var stopwatch = Stopwatch.StartNew();
            
            try
            {
                await testMethod();
                stopwatch.Stop();

                var result = new TestResult
                {
                    TestName = testId,
                    Status = TestStatus.Passed,
                    ExecutionTime = stopwatch.Elapsed,
                    Phase = testName
                };

                _testResults.Add(result);
                _output.WriteLine($"✅ {testId} ({testName}) PASSED - {stopwatch.ElapsedMilliseconds}ms");
            }
            catch (Exception ex)
            {
                stopwatch.Stop();

                var result = new TestResult
                {
                    TestName = testId,
                    Status = TestStatus.Failed,
                    ExecutionTime = stopwatch.Elapsed,
                    Phase = testName,
                    ErrorMessage = ex.Message,
                    Exception = ex
                };

                _testResults.Add(result);
                _output.WriteLine($"❌ {testId} ({testName}) FAILED: {ex.Message}");
            }
        }

        private EnhancedE2EReport GenerateEnhancedReport(TimeSpan totalExecutionTime)
        {
            var report = new EnhancedE2EReport
            {
                ExecutionDate = DateTime.Now,
                TotalExecutionTime = totalExecutionTime,
                TestResults = _testResults.ToList(),
                TotalTests = _testResults.Count,
                PassedTests = _testResults.Count(r => r.Status == TestStatus.Passed),
                FailedTests = _testResults.Count(r => r.Status == TestStatus.Failed),
                SuccessRate = _testResults.Count > 0 ? 
                    (_testResults.Count(r => r.Status == TestStatus.Passed) * 100.0 / _testResults.Count) : 0
            };

            // Analyze specific requirement compliance
            report.RequirementCompliance = new RequirementCompliance
            {
                DefaultValuesCorrect = _testResults.Any(t => t.TestName == "TC_UI_001" && t.Status == TestStatus.Passed),
                ConfigurationPersists = _testResults.Any(t => t.TestName == "TC_UI_002" && t.Status == TestStatus.Passed),
                RestoreDefaultsWorks = _testResults.Any(t => t.TestName == "TC_UI_003" && t.Status == TestStatus.Passed),
                ValidationRulesWork = _testResults.Any(t => t.TestName == "TC_UI_004" && t.Status == TestStatus.Passed),
                ChangeDetectionWorks = _testResults.Any(t => t.TestName == "TC_UI_005" && t.Status == TestStatus.Passed),
                TimerCommandsWork = _testResults.Any(t => t.TestName == "TC_UI_006" && t.Status == TestStatus.Passed),
                AllRequirementsMet = _testResults.Any(t => t.TestName == "TC_UI_007" && t.Status == TestStatus.Passed)
            };

            return report;
        }

        private void PrintEnhancedReport(EnhancedE2EReport report)
        {
                        _output.WriteLine("\n" + new string('=', 70));
            _output.WriteLine("📊 ENHANCED E2E UI VALIDATION REPORT");
            _output.WriteLine(new string('=', 60));

            _output.WriteLine($"📅 Execution Date: {report.ExecutionDate:yyyy-MM-dd HH:mm:ss}");
            _output.WriteLine($"⏱️ Total Execution Time: {report.TotalExecutionTime.TotalSeconds:F2} seconds");
            _output.WriteLine($"🧪 Total Tests: {report.TotalTests}");
            _output.WriteLine($"✅ Passed: {report.PassedTests}");
            _output.WriteLine($"❌ Failed: {report.FailedTests}");
            _output.WriteLine($"📈 Success Rate: {report.SuccessRate:F1}%");

            _output.WriteLine("\n🎯 REQUIREMENT COMPLIANCE ANALYSIS:");
            _output.WriteLine($"   📋 Default Values Display Correctly: {(report.RequirementCompliance.DefaultValuesCorrect ? "✅ PASS" : "❌ FAIL")}");
            _output.WriteLine($"   💾 Auto-Save Configuration Works: {(report.RequirementCompliance.ConfigurationPersists ? "✅ PASS" : "❌ FAIL")}");
            _output.WriteLine($"   🔄 Restore Defaults Functions: {(report.RequirementCompliance.RestoreDefaultsWorks ? "✅ PASS" : "❌ FAIL")}");
            _output.WriteLine($"   ✅ Input Validation Rules: {(report.RequirementCompliance.ValidationRulesWork ? "✅ PASS" : "❌ FAIL")}");
            _output.WriteLine($"   ⚡ Real-time Auto-Save System: {(report.RequirementCompliance.ChangeDetectionWorks ? "✅ PASS" : "❌ FAIL")}");
            _output.WriteLine($"   ⏰ Timer Commands Execute: {(report.RequirementCompliance.TimerCommandsWork ? "✅ PASS" : "❌ FAIL")}");
            _output.WriteLine($"   🎯 All Requirements Met: {(report.RequirementCompliance.AllRequirementsMet ? "✅ PASS" : "❌ FAIL")}");

            if (report.FailedTests > 0)
            {
                _output.WriteLine("\n❌ FAILED TESTS ANALYSIS:");
                foreach (var failedTest in report.TestResults.Where(r => r.Status == TestStatus.Failed))
                {
                    _output.WriteLine($"   • {failedTest.TestName} ({failedTest.Phase}): {failedTest.ErrorMessage}");
                    if (failedTest.Exception != null)
                    {
                        _output.WriteLine($"     Stack Trace: {failedTest.Exception.StackTrace?.Split('\n').FirstOrDefault()}");
                    }
                }
            }

            _output.WriteLine("\n📋 DETAILED TEST RESULTS:");
            foreach (var test in report.TestResults)
            {
                var status = test.Status == TestStatus.Passed ? "✅ PASS" : "❌ FAIL";
                _output.WriteLine($"   {test.TestName}: {status} ({test.ExecutionTime.TotalMilliseconds:F0}ms) - {test.Phase}");
            }

            var overallStatus = report.SuccessRate == 100 ? "🎉 EXCELLENT" : 
                               report.SuccessRate >= 85 ? "✅ GOOD" : 
                               report.SuccessRate >= 70 ? "⚠️ NEEDS ATTENTION" : "❌ CRITICAL ISSUES";

            _output.WriteLine($"\n🏆 OVERALL UI VALIDATION STATUS: {overallStatus}");
            
            if (report.SuccessRate == 100)
            {
                _output.WriteLine("🎊 All UI validation tests passed! The application UI meets requirements.");
            }
            else
            {
                _output.WriteLine($"⚠️ {report.FailedTests} test(s) failed. UI issues need to be addressed.");
            }
            
            _output.WriteLine(new string('=', 60));
        }
    }

    public class EnhancedE2EReport
    {
        public DateTime ExecutionDate { get; set; }
        public TimeSpan TotalExecutionTime { get; set; }
        public List<TestResult> TestResults { get; set; } = new();
        public int TotalTests { get; set; }
        public int PassedTests { get; set; }
        public int FailedTests { get; set; }
        public double SuccessRate { get; set; }
        public RequirementCompliance RequirementCompliance { get; set; } = new();
    }

    public class RequirementCompliance
    {
        public bool DefaultValuesCorrect { get; set; }
        public bool ConfigurationPersists { get; set; }
        public bool RestoreDefaultsWorks { get; set; }
        public bool ValidationRulesWork { get; set; }
        public bool ChangeDetectionWorks { get; set; }
        public bool TimerCommandsWork { get; set; }
        public bool AllRequirementsMet { get; set; }
    }
}