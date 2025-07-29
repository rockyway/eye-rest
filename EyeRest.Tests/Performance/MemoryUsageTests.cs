using System;
using System.Diagnostics;
using System.Threading.Tasks;
using EyeRest.Services;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Xunit;
using Xunit.Abstractions;

namespace EyeRest.Tests.Performance
{
    public class MemoryUsageTests
    {
        private readonly ITestOutputHelper _output;

        public MemoryUsageTests(ITestOutputHelper output)
        {
            _output = output;
        }

        [Fact]
        public async Task IdleApplication_StaysUnder50MB()
        {
            // Arrange
            var host = CreateTestHost();
            await host.StartAsync();
            
            var configService = host.Services.GetRequiredService<IConfigurationService>();
            var timerService = host.Services.GetRequiredService<ITimerService>();
            
            await configService.LoadConfigurationAsync();
            await timerService.StartAsync();

            // Force garbage collection to get accurate baseline
            GC.Collect();
            GC.WaitForPendingFinalizers();
            GC.Collect();

            // Act - Let the application run for a short time
            await Task.Delay(2000);

            var process = Process.GetCurrentProcess();
            var memoryUsageMB = process.WorkingSet64 / (1024 * 1024);

            // Assert
            _output.WriteLine($"Memory usage: {memoryUsageMB}MB");
            Assert.True(memoryUsageMB < 50, 
                $"Memory usage is {memoryUsageMB}MB, which exceeds the 50MB requirement");

            // Cleanup
            await timerService.StopAsync();
            await host.StopAsync();
            host.Dispose();
        }

        [Fact]
        public async Task RepeatedOperations_DoNotLeakMemory()
        {
            // Arrange
            var host = CreateTestHost();
            await host.StartAsync();
            
            var configService = host.Services.GetRequiredService<IConfigurationService>();
            var timerService = host.Services.GetRequiredService<ITimerService>();
            
            await configService.LoadConfigurationAsync();
            await timerService.StartAsync();

            // Get baseline memory usage
            GC.Collect();
            GC.WaitForPendingFinalizers();
            GC.Collect();
            
            var process = Process.GetCurrentProcess();
            var baselineMemoryMB = process.WorkingSet64 / (1024 * 1024);

            // Act - Perform repeated operations that might cause memory leaks
            for (int i = 0; i < 100; i++)
            {
                await timerService.ResetEyeRestTimer();
                await timerService.ResetBreakTimer();
                await configService.LoadConfigurationAsync();
                
                if (i % 20 == 0)
                {
                    GC.Collect();
                    GC.WaitForPendingFinalizers();
                }
            }

            // Force garbage collection
            GC.Collect();
            GC.WaitForPendingFinalizers();
            GC.Collect();

            var finalMemoryMB = process.WorkingSet64 / (1024 * 1024);
            var memoryIncrease = finalMemoryMB - baselineMemoryMB;

            // Assert
            _output.WriteLine($"Baseline memory: {baselineMemoryMB}MB");
            _output.WriteLine($"Final memory: {finalMemoryMB}MB");
            _output.WriteLine($"Memory increase: {memoryIncrease}MB");
            
            Assert.True(memoryIncrease < 10, 
                $"Memory increased by {memoryIncrease}MB after repeated operations, indicating potential memory leak");

            // Cleanup
            await timerService.StopAsync();
            await host.StopAsync();
            host.Dispose();
        }

        [Fact]
        public async Task ServiceDisposal_ReleasesResources()
        {
            // Arrange
            var host = CreateTestHost();
            await host.StartAsync();
            
            var configService = host.Services.GetRequiredService<IConfigurationService>();
            var timerService = host.Services.GetRequiredService<ITimerService>();
            
            await configService.LoadConfigurationAsync();
            await timerService.StartAsync();

            // Get memory usage before disposal
            GC.Collect();
            GC.WaitForPendingFinalizers();
            GC.Collect();
            
            var process = Process.GetCurrentProcess();
            var beforeDisposalMB = process.WorkingSet64 / (1024 * 1024);

            // Act - Dispose services
            await timerService.StopAsync();
            if (timerService is IDisposable disposableTimer)
            {
                disposableTimer.Dispose();
            }

            await host.StopAsync();
            host.Dispose();

            // Force garbage collection
            GC.Collect();
            GC.WaitForPendingFinalizers();
            GC.Collect();

            var afterDisposalMB = process.WorkingSet64 / (1024 * 1024);
            var memoryReduction = beforeDisposalMB - afterDisposalMB;

            // Assert
            _output.WriteLine($"Memory before disposal: {beforeDisposalMB}MB");
            _output.WriteLine($"Memory after disposal: {afterDisposalMB}MB");
            _output.WriteLine($"Memory reduction: {memoryReduction}MB");
            
            // We expect some memory to be released, but the exact amount may vary
            Assert.True(memoryReduction >= 0, 
                "Memory usage should not increase after disposal");
        }

        private IHost CreateTestHost()
        {
            return Host.CreateDefaultBuilder()
                .ConfigureServices(services =>
                {
                    services.AddSingleton<IConfigurationService, ConfigurationService>();
                    services.AddSingleton<ITimerService, TimerService>();
                    services.AddSingleton<IAudioService, AudioService>();
                    services.AddSingleton<INotificationService, NotificationService>();
                })
                .ConfigureLogging(logging =>
                {
                    logging.ClearProviders();
                    logging.SetMinimumLevel(LogLevel.Warning);
                })
                .Build();
        }
    }
}