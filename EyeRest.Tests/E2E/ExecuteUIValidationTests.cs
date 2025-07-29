using System.Threading.Tasks;
using Xunit;
using Xunit.Abstractions;

namespace EyeRest.Tests.E2E
{
    /// <summary>
    /// Main UI validation test execution class that runs comprehensive E2E tests
    /// to ensure the UI loads and acts correctly according to requirements
    /// </summary>
    public class ExecuteUIValidationTests
    {
        private readonly ITestOutputHelper _output;

        public ExecuteUIValidationTests(ITestOutputHelper output)
        {
            _output = output;
        }

        [Fact]
        public async Task ExecuteCompleteUIValidationTestSuite()
        {
            _output.WriteLine("🎯 EXECUTING COMPLETE UI VALIDATION TEST SUITE");
            _output.WriteLine("================================================================");
            _output.WriteLine("This test suite validates that the UI loads and acts correctly");
            _output.WriteLine("according to all requirements, including default values display.");
            _output.WriteLine("================================================================");

            var testRunner = new EnhancedE2ETestRunner(_output);
            var report = await testRunner.ExecuteUIValidationTestsAsync();

            // Assert critical UI functionality
            Assert.True(report.RequirementCompliance.DefaultValuesCorrect, 
                "CRITICAL: Default values are not displaying correctly in the UI");

            Assert.True(report.RequirementCompliance.ConfigurationPersists, 
                "CRITICAL: Configuration persistence is not working");

            Assert.True(report.RequirementCompliance.RestoreDefaultsWorks, 
                "CRITICAL: Restore defaults functionality is not working");

            Assert.True(report.RequirementCompliance.AllRequirementsMet, 
                "CRITICAL: Not all requirements are being met by the UI");

            // Assert overall success rate
            Assert.True(report.SuccessRate >= 85, 
                $"UI validation success rate {report.SuccessRate:F1}% is below acceptable threshold of 85%");

            // Assert no critical failures
            var criticalFailures = report.TestResults.Where(r => 
                r.Status == TestStatus.Failed && 
                (r.TestName.Contains("TC_UI_001") || // Default values
                 r.TestName.Contains("TC_UI_007"))); // Requirements compliance

            Assert.Empty(criticalFailures);

            _output.WriteLine($"\n🎉 UI VALIDATION TEST SUITE COMPLETED SUCCESSFULLY!");
            _output.WriteLine($"📊 Final Score: {report.SuccessRate:F1}% ({report.PassedTests}/{report.TotalTests} tests passed)");
            
            if (report.SuccessRate == 100)
            {
                _output.WriteLine("🏆 PERFECT SCORE: All UI validation tests passed!");
                _output.WriteLine("✅ The application UI loads and acts correctly according to all requirements.");
            }
            else
            {
                _output.WriteLine($"⚠️ {report.FailedTests} test(s) failed - see report above for details.");
            }
        }

        [Fact]
        public async Task ValidateDefaultValuesSpecifically()
        {
            _output.WriteLine("🔍 SPECIFIC TEST: Default Values Validation");
            _output.WriteLine("This test specifically validates the bug you reported about default values.");

            var uiTests = new UIValidationTests(_output);
            
            try
            {
                // This should catch the bug where UI shows 0 instead of proper defaults
                await uiTests.TC_UI_001_DefaultValues_DisplayCorrectly();
                
                _output.WriteLine("✅ Default values test PASSED - UI shows correct default values");
            }
            catch (Exception ex)
            {
                _output.WriteLine($"❌ Default values test FAILED: {ex.Message}");
                _output.WriteLine("This confirms the bug you reported about default values not displaying correctly.");
                throw;
            }
            finally
            {
                uiTests.Dispose();
            }
        }

        [Fact]
        public async Task ValidateRequirementsCompliance()
        {
            _output.WriteLine("📋 REQUIREMENTS COMPLIANCE VALIDATION");
            _output.WriteLine("Validating that the UI meets all specified requirements:");

            var uiTests = new UIValidationTests(_output);
            
            try
            {
                await uiTests.TC_UI_007_RequirementsCompliance_AllRequirementsMet();
                
                _output.WriteLine("✅ Requirements compliance test PASSED");
                _output.WriteLine("✅ All 8 core requirements are properly implemented in the UI");
            }
            catch (Exception ex)
            {
                _output.WriteLine($"❌ Requirements compliance test FAILED: {ex.Message}");
                _output.WriteLine("This indicates that some requirements are not properly implemented.");
                throw;
            }
            finally
            {
                uiTests.Dispose();
            }
        }
    }
}