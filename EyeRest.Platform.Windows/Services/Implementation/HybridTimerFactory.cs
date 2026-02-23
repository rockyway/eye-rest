using System.Windows.Threading;
using EyeRest.Services.Abstractions;
using Microsoft.Extensions.Logging;

namespace EyeRest.Services.Implementation
{
    /// <summary>
    /// Factory for creating hybrid timer instances that use System.Threading.Timer + Dispatcher
    /// This replaces the fragile DispatcherTimer-based ProductionTimerFactory
    /// </summary>
    public class HybridTimerFactory : ITimerFactory
    {
        private readonly Dispatcher _dispatcher;
        private readonly ILoggerFactory? _loggerFactory;
        private readonly ILogger<HybridTimerFactory>? _logger;

        public HybridTimerFactory(Dispatcher dispatcher, ILoggerFactory? loggerFactory = null)
        {
            _dispatcher = dispatcher ?? throw new System.ArgumentNullException(nameof(dispatcher));
            _loggerFactory = loggerFactory;
            _logger = _loggerFactory?.CreateLogger<HybridTimerFactory>();

            _logger?.LogInformation("HybridTimerFactory initialized - will create robust System.Threading.Timer + Dispatcher instances");
        }

        public ITimer CreateTimer(TimerPriority priority = TimerPriority.Normal)
        {
            // Create hybrid timer that doesn't suffer from DispatcherTimer corruption issues
            // Priority parameter is ignored since System.Threading.Timer doesn't use dispatcher priorities
            var hybridTimer = new HybridTimer(_dispatcher, _loggerFactory?.CreateLogger<HybridTimer>());

            _logger?.LogDebug("Created new HybridTimer instance");

            return hybridTimer;
        }
    }
}
