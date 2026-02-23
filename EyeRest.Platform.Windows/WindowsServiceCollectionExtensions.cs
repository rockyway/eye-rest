using System.Windows.Threading;
using EyeRest.Services;
using EyeRest.Services.Abstractions;
using EyeRest.Services.Implementation;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace EyeRest.Platform.Windows
{
    public static class WindowsServiceCollectionExtensions
    {
        public static IServiceCollection AddWindowsPlatformServices(this IServiceCollection services)
        {
            // Dispatcher service
            services.AddSingleton<IDispatcherService>(sp =>
                new WpfDispatcherService(System.Windows.Application.Current.Dispatcher));

            // Register Dispatcher for services that need it directly (e.g., PauseReminderService)
            services.AddSingleton<Dispatcher>(_ => Dispatcher.CurrentDispatcher);

            // Timer factory
            services.AddSingleton<ITimerFactory>(sp =>
                new HybridTimerFactory(
                    System.Windows.Application.Current.Dispatcher,
                    sp.GetService<ILoggerFactory>()));

            // Platform services
            services.AddSingleton<IAudioService, AudioService>();
            services.AddSingleton<ISystemTrayService, SystemTrayService>();
            services.AddSingleton<IStartupManager, StartupManager>();
            services.AddSingleton<IScreenOverlayService, ScreenOverlayService>();
            services.AddSingleton<IUserPresenceService, UserPresenceService>();
            services.AddSingleton<IScreenDimmingService, ScreenDimmingService>();
            services.AddSingleton<IPauseReminderService, PauseReminderService>();
            services.AddSingleton<IconService>();

            return services;
        }
    }
}
