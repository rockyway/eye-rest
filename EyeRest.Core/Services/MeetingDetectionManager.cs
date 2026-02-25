using System;
using System.Threading.Tasks;
using EyeRest.Models;
using Microsoft.Extensions.Logging;

namespace EyeRest.Services
{
    /// <summary>
    /// Manager for meeting detection services with runtime method switching
    /// </summary>
    public class MeetingDetectionManager : IMeetingDetectionManager
    {
        private readonly IMeetingDetectionServiceFactory _factory;
        private readonly ILogger<MeetingDetectionManager> _logger;
        private readonly object _lock = new object();

        private IMeetingDetectionService? _currentService;
        private MeetingDetectionMethod _currentMethod = MeetingDetectionMethod.WindowBased;
        private MeetingDetectionSettings _currentSettings = new();
        private bool _isInitialized = false;
        private DateTime? _lastStateChange;
        private string _statusMessage = "Not initialized";
        private bool _hasErrors = false;
        private string? _errorMessage;

        public IMeetingDetectionService? CurrentDetectionService => _currentService;
        public MeetingDetectionMethod CurrentMethod => _currentMethod;

        public event EventHandler<MeetingStateEventArgs>? MeetingStateChanged;

        public MeetingDetectionManager(
            IMeetingDetectionServiceFactory factory,
            ILogger<MeetingDetectionManager> logger)
        {
            _factory = factory;
            _logger = logger;
        }

        public async Task InitializeAsync(MeetingDetectionMethod method, MeetingDetectionSettings settings)
        {
            lock (_lock)
            {
                if (_isInitialized)
                {
                    _logger.LogWarning("Meeting detection manager is already initialized");
                    return;
                }
            }

            try
            {
                _logger.LogInformation($"🎛️ Initializing meeting detection manager with method: {method}");

                _currentMethod = method;
                _currentSettings = settings ?? new MeetingDetectionSettings();

                // Validate the method is available
                if (!await _factory.ValidateDetectionMethodAsync(method))
                {
                    throw new NotSupportedException($"Detection method {method} is not available on this system");
                }

                // Create and start the detection service
                await CreateAndStartServiceAsync(method);

                lock (_lock)
                {
                    _isInitialized = true;
                    _statusMessage = $"Initialized with {method} detection";
                    _hasErrors = false;
                    _errorMessage = null;
                }

                _logger.LogInformation($"🎛️ Meeting detection manager initialized successfully with {method} detection");
            }
            catch (Exception ex)
            {
                lock (_lock)
                {
                    _hasErrors = true;
                    _errorMessage = ex.Message;
                    _statusMessage = $"Failed to initialize: {ex.Message}";
                }

                _logger.LogError(ex, $"Failed to initialize meeting detection manager with method {method}");
                throw;
            }
        }

        public async Task SwitchDetectionMethodAsync(MeetingDetectionMethod newMethod)
        {
            if (!_isInitialized)
            {
                throw new InvalidOperationException("Manager must be initialized before switching methods");
            }

            if (_currentMethod == newMethod)
            {
                _logger.LogInformation($"Already using {newMethod} detection method");
                return;
            }

            try
            {
                _logger.LogInformation($"🔄 Switching detection method from {_currentMethod} to {newMethod}");

                // Validate the new method is available
                if (!await _factory.ValidateDetectionMethodAsync(newMethod))
                {
                    throw new NotSupportedException($"Detection method {newMethod} is not available on this system");
                }

                // Stop current service
                await StopCurrentServiceAsync();

                // Create and start new service
                await CreateAndStartServiceAsync(newMethod);

                lock (_lock)
                {
                    _currentMethod = newMethod;
                    _statusMessage = $"Switched to {newMethod} detection";
                    _hasErrors = false;
                    _errorMessage = null;
                    _lastStateChange = DateTime.Now;
                }

                _logger.LogInformation($"🔄 Successfully switched to {newMethod} detection method");
            }
            catch (Exception ex)
            {
                lock (_lock)
                {
                    _hasErrors = true;
                    _errorMessage = ex.Message;
                    _statusMessage = $"Failed to switch to {newMethod}: {ex.Message}";
                }

                _logger.LogError(ex, $"Failed to switch to {newMethod} detection method");
                throw;
            }
        }

        public async Task<bool> ValidateMethodAvailabilityAsync(MeetingDetectionMethod method)
        {
            try
            {
                return await _factory.ValidateDetectionMethodAsync(method);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error validating method {method}");
                return false;
            }
        }

        public async Task UpdateSettingsAsync(MeetingDetectionSettings settings)
        {
            await Task.CompletedTask;
            if (!_isInitialized || _currentService == null)
            {
                _logger.LogWarning("Cannot update settings - manager not initialized");
                return;
            }

            try
            {
                _logger.LogDebug("Updating detection settings");

                _currentSettings = settings ?? new MeetingDetectionSettings();
                _currentService.Settings = _currentSettings;

                _logger.LogDebug("Detection settings updated successfully");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error updating detection settings");
                lock (_lock)
                {
                    _hasErrors = true;
                    _errorMessage = ex.Message;
                }
            }
        }

        public DetectionServiceStatus GetStatus()
        {
            lock (_lock)
            {
                return new DetectionServiceStatus
                {
                    CurrentMethod = _currentMethod,
                    IsMonitoring = _currentService != null && _isInitialized,
                    IsMeetingActive = _currentService?.IsMeetingActive ?? false,
                    DetectedMeetingsCount = _currentService?.DetectedMeetings?.Count ?? 0,
                    LastStateChange = _lastStateChange,
                    StatusMessage = _statusMessage,
                    HasErrors = _hasErrors,
                    ErrorMessage = _errorMessage
                };
            }
        }

        public async Task ShutdownAsync()
        {
            try
            {
                _logger.LogInformation("🛑 Shutting down meeting detection manager");

                await StopCurrentServiceAsync();

                lock (_lock)
                {
                    _isInitialized = false;
                    _statusMessage = "Shutdown";
                    _lastStateChange = DateTime.Now;
                }

                _logger.LogInformation("🛑 Meeting detection manager shutdown completed");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error during meeting detection manager shutdown");
                throw;
            }
        }

        private async Task CreateAndStartServiceAsync(MeetingDetectionMethod method)
        {
            try
            {
                // Create the service
                _currentService = _factory.CreateDetectionService(method);
                
                // Apply current settings
                _currentService.Settings = _currentSettings;
                
                // Subscribe to events
                _currentService.MeetingStateChanged += OnServiceMeetingStateChanged;
                
                // Start monitoring
                await _currentService.StartMonitoringAsync();

                _logger.LogInformation($"Detection service for {method} created and started successfully");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Failed to create and start detection service for {method}");
                
                // Clean up partially created service
                if (_currentService != null)
                {
                    try
                    {
                        _currentService.MeetingStateChanged -= OnServiceMeetingStateChanged;
                        _currentService.Dispose();
                    }
                    catch { /* Ignore cleanup errors */ }
                    _currentService = null;
                }
                
                throw;
            }
        }

        private async Task StopCurrentServiceAsync()
        {
            if (_currentService == null) return;

            try
            {
                _logger.LogDebug($"Stopping current detection service ({_currentMethod})");

                // Unsubscribe from events
                _currentService.MeetingStateChanged -= OnServiceMeetingStateChanged;
                
                // Stop monitoring
                await _currentService.StopMonitoringAsync();
                
                // Dispose
                _currentService.Dispose();
                
                _currentService = null;

                _logger.LogDebug("Current detection service stopped successfully");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error stopping current detection service");
                // Don't rethrow - we want to continue with switching
            }
        }

        private void OnServiceMeetingStateChanged(object? sender, MeetingStateEventArgs e)
        {
            try
            {
                lock (_lock)
                {
                    _lastStateChange = e.StateChangedAt;
                    _statusMessage = e.IsMeetingActive ? 
                        $"Meeting active ({e.ActiveMeetings.Count} detected)" : 
                        "No meeting detected";
                }

                // Forward the event
                MeetingStateChanged?.Invoke(this, e);

                _logger.LogDebug($"Meeting state changed via {_currentMethod}: {e.IsMeetingActive}");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error handling meeting state change");
            }
        }

        public void Dispose()
        {
            try
            {
                ShutdownAsync().Wait(TimeSpan.FromSeconds(5));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error during disposal");
            }
        }
    }
}