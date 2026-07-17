using EyeRest.Services;
using EyeRest.Services.Abstractions;
using Microsoft.Extensions.DependencyInjection;

namespace EyeRest.Platform.Linux
{
    /// <summary>
    /// Extension methods for registering Linux platform services in the DI container.
    /// </summary>
    public static class LinuxServiceCollectionExtensions
    {
        /// <summary>
        /// Registers all Linux platform service implementations.
        /// IDispatcherService is intentionally not registered — the cross-platform
        /// AvaloniaDispatcherService registered in App.axaml.cs is used (same as Windows).
        /// </summary>
        public static IServiceCollection AddLinuxPlatformServices(this IServiceCollection services)
        {
            services.AddSingleton<IUrlOpener, DefaultUrlOpener>(); // BL-002 M2: must precede IAudioService
            services.AddSingleton<IAudioService, LinuxAudioService>();
            services.AddSingleton<ISystemTrayService, LinuxSystemTrayService>();
            services.AddSingleton<IStartupManager, LinuxStartupManager>();
            services.AddSingleton<IScreenOverlayService, LinuxScreenOverlayService>();
            services.AddSingleton<IUserPresenceService, LinuxUserPresenceService>();
            services.AddSingleton<IScreenDimmingService, LinuxScreenDimmingService>();
            services.AddSingleton<IPauseReminderService, LinuxPauseReminderService>();
            services.AddSingleton<ITimerFactory, LinuxTimerFactory>();
            services.AddSingleton<ISecureStorageService, LinuxSecureStorageService>();
            services.AddSingleton<IAppLifecycleService, LinuxAppLifecycleService>();

            return services;
        }
    }
}
