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
            // Timer factory (uses IDispatcherService registered by the UI layer)
            services.AddSingleton<ITimerFactory>(sp =>
                new HybridTimerFactory(
                    sp.GetRequiredService<IDispatcherService>(),
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
            services.AddSingleton<ISecureStorageService, WindowsSecureStorageService>();
            services.AddSingleton<IAppLifecycleService, Services.WindowsAppLifecycleService>();

            return services;
        }
    }
}
