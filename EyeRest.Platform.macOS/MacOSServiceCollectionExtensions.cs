using EyeRest.Services;
using EyeRest.Services.Abstractions;
using Microsoft.Extensions.DependencyInjection;

namespace EyeRest.Platform.macOS
{
    /// <summary>
    /// Extension methods for registering macOS platform services in the DI container.
    /// </summary>
    public static class MacOSServiceCollectionExtensions
    {
        /// <summary>
        /// Registers all macOS platform service implementations.
        /// </summary>
        public static IServiceCollection AddMacOSPlatformServices(this IServiceCollection services)
        {
            services.AddSingleton<IDispatcherService, MacOSDispatcherService>();
            services.AddSingleton<IAudioService, MacOSAudioService>();
            services.AddSingleton<ISystemTrayService, MacOSSystemTrayService>();
            services.AddSingleton<IStartupManager, MacOSStartupManager>();
            services.AddSingleton<IScreenOverlayService, MacOSScreenOverlayService>();
            services.AddSingleton<IUserPresenceService, MacOSUserPresenceService>();
            services.AddSingleton<IScreenDimmingService, MacOSScreenDimmingService>();
            services.AddSingleton<IPauseReminderService, MacOSPauseReminderService>();
            services.AddSingleton<ITimerFactory, MacOSTimerFactory>();
            services.AddSingleton<MacOSIconService>();
            services.AddSingleton<ISecureStorageService, MacOSSecureStorageService>();

            return services;
        }
    }
}
