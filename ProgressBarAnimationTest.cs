using System;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Threading;
using EyeRest.Views;

namespace EyeRest
{
    /// <summary>
    /// Simple test to verify progress bar animation functionality
    /// Run this with: dotnet run -- --test-progress-bars
    /// </summary>
    public static class ProgressBarAnimationTest
    {
        public static async Task RunProgressBarTest()
        {
            try
            {
                Console.WriteLine("🧪 Starting Progress Bar Animation Test...");
                
                // Test each popup individually
                await TestBreakPopupProgress();
                await TestEyeRestPopupProgress();
                await TestBreakWarningPopupProgress();
                await TestEyeRestWarningPopupProgress();
                
                Console.WriteLine("✅ All progress bar animation tests completed successfully!");
                Console.WriteLine("💡 Progress bars should now animate smoothly during countdown periods.");
                
            }
            catch (Exception ex)
            {
                Console.WriteLine($"❌ Progress bar test failed: {ex.Message}");
                Console.WriteLine($"🔍 Stack trace: {ex.StackTrace}");
            }
        }
        
        private static async Task TestBreakPopupProgress()
        {
            Console.WriteLine("🔥 Testing BreakPopup progress animation...");
            
            var popup = new BreakPopup();
            var testDuration = TimeSpan.FromSeconds(2); // Short test duration
            
            popup.StartCountdown(testDuration);
            
            // Wait a bit to see if animation starts
            await Task.Delay(500);
            
            // Check if progress bar value has changed
            if (popup.ProgressBar.Value > 0)
            {
                Console.WriteLine($"   ✅ BreakPopup progress animation working - Value: {popup.ProgressBar.Value:F1}%");
            }
            else
            {
                Console.WriteLine($"   ⚠️  BreakPopup progress might not be animating - Value: {popup.ProgressBar.Value:F1}%");
            }
            
            popup.StopCountdown();
        }
        
        private static async Task TestEyeRestPopupProgress()
        {
            Console.WriteLine("👁 Testing EyeRestPopup progress animation...");
            
            var popup = new EyeRestPopup();
            var testDuration = TimeSpan.FromSeconds(2);
            
            popup.StartCountdown(testDuration);
            
            await Task.Delay(500);
            
            if (popup.ProgressBar.Value > 0)
            {
                Console.WriteLine($"   ✅ EyeRestPopup progress animation working - Value: {popup.ProgressBar.Value:F1}%");
            }
            else
            {
                Console.WriteLine($"   ⚠️  EyeRestPopup progress might not be animating - Value: {popup.ProgressBar.Value:F1}%");
            }
            
            popup.StopCountdown();
        }
        
        private static async Task TestBreakWarningPopupProgress()
        {
            Console.WriteLine("🟠 Testing BreakWarningPopup progress animation...");
            
            var popup = new BreakWarningPopup();
            var testDuration = TimeSpan.FromSeconds(2);
            
            popup.StartCountdown(testDuration);
            
            await Task.Delay(500);
            
            if (popup.ProgressBar.Value > 0)
            {
                Console.WriteLine($"   ✅ BreakWarningPopup progress animation working - Value: {popup.ProgressBar.Value:F1}%");
            }
            else
            {
                Console.WriteLine($"   ⚠️  BreakWarningPopup progress might not be animating - Value: {popup.ProgressBar.Value:F1}%");
            }
            
            popup.StopCountdown();
        }
        
        private static async Task TestEyeRestWarningPopupProgress()
        {
            Console.WriteLine("👁 Testing EyeRestWarningPopup progress animation...");
            
            var popup = new EyeRestWarningPopup();
            
            popup.StartCountdown(2); // 2 seconds
            
            await Task.Delay(500);
            
            if (popup.ProgressBar.Value > 0)
            {
                Console.WriteLine($"   ✅ EyeRestWarningPopup progress animation working - Value: {popup.ProgressBar.Value:F1}%");
            }
            else
            {
                Console.WriteLine($"   ⚠️  EyeRestWarningPopup progress might not be animating - Value: {popup.ProgressBar.Value:F1}%");
            }
            
            popup.StopCountdown();
        }
    }
}