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
    public class StartupPerformanceTests
    {
        private readonly ITestOutputHelper _output;

        public StartupPerformanceTests(ITestOutputHelper output)
        {
            _output = output;
        }

        [Fact]
        public async Task ServiceInitialization_CompletesWithin3Seconds()
        {
            // Arrange
            var stopwatch = Stopwatch.StartNew();
            var host = CreateTestHost();

            // Act
            await host.StartAsync();
            
            var configService = host.Services.GetRequiredService<IConfigurationService>();
            var timerService = host.Services.GetRequiredService<ITimerService>();
            var audioService = host.Services.GetRequiredService<IAudioService>();
            
            await configService.LoadConfigurationAsync();
            await timerService.StartAsync();
            
            stopwatch.Stop();

            // Assert
            _output.WriteLine($"Service initialization took: {stopwatch.ElapsedMilliseconds}ms");
            Assert.True(stopwatch.ElapsedMilliseconds < 3000, 
                $"Service initialization took {stopwatch.ElapsedMilliseconds}ms, which exceeds the 3000ms requirement");

            // Cleanup
            await timerService.StopAsync();
            await host.StopAsync();
            host.Dispose();
        }

        [Fact]
        public async Task ConfigurationLoading_CompletesQuickly()
        {
            // Arrange
            var host = CreateTestHost();
            await host.StartAsync();
            var configService = host.Services.GetRequiredService<IConfigurationService>();
            
            var stopwatch = Stopwatch.StartNew();

            // Act
            var config = await configService.LoadConfigurationAsync();
            
            stopwatch.Stop();

            // Assert
            _output.WriteLine($"Configuration loading took: {stopwatch.ElapsedMilliseconds}ms");
            Assert.True(stopwatch.ElapsedMilliseconds < 500, 
                $"Configuration loading took {stopwatch.ElapsedMilliseconds}ms, which is too slow");
            Assert.NotNull(config);

            // Cleanup
            await host.StopAsync();
            host.Dispose();
        }

        [Fact]
        public async Task TimerServiceStart_CompletesQuickly()
        {
            // Arrange
            var host = CreateTestHost();
            await host.StartAsync();
            var timerService = host.Services.GetRequiredService<ITimerService>();
            
            var stopwatch = Stopwatch.StartNew();

            // Act
            await timerService.StartAsync();
            
            stopwatch.Stop();

            // Assert
            _output.WriteLine($"Timer service start took: {stopwatch.ElapsedMilliseconds}ms");
            Assert.True(stopwatch.ElapsedMilliseconds < 1000, 
                $"Timer service start took {stopwatch.ElapsedMilliseconds}ms, which is too slow");

            // Cleanup
            await timerService.StopAsync();
            await host.StopAsync();
            host.Dispose();
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