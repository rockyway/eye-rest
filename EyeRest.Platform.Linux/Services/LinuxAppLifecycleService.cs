using EyeRest.Services.Abstractions;
using Microsoft.Extensions.Logging;

namespace EyeRest.Services
{
    /// <summary>
    /// Linux lifecycle service. There is no App Nap equivalent to opt out of, and
    /// suspend/resume recovery is already covered by the presence service: X11 idle
    /// time keeps accumulating across a system sleep, so a long suspend surfaces as
    /// an extended-away session and triggers the same session reset that the
    /// macOS/Windows wake events do. The sleep/wake events are therefore not raised
    /// here (a logind DBus listener can be added later if finer-grained wake
    /// handling is ever needed).
    /// </summary>
    public sealed class LinuxAppLifecycleService : IAppLifecycleService
    {
        private readonly ILogger<LinuxAppLifecycleService> _logger;

#pragma warning disable CS0067 // Events required by interface; see class doc for why they are not raised
        public event Action? SystemAwoke;
        public event Action? SystemWillSleep;
#pragma warning restore CS0067

        public LinuxAppLifecycleService(ILogger<LinuxAppLifecycleService> logger)
        {
            _logger = logger;
        }

        public Task StartAsync()
        {
            _logger.LogInformation("Linux lifecycle service started (sleep/wake recovery handled via presence idle detection)");
            return Task.CompletedTask;
        }

        public Task StopAsync()
        {
            return Task.CompletedTask;
        }
    }
}
