using System;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using EyeRest.Models;
using Microsoft.Extensions.Logging;

namespace EyeRest.Services
{
    public class DonationService : IDonationService
    {
        private readonly IConfigurationService _configurationService;
        private readonly ISecureStorageService _secureStorage;
        private readonly ITimerService _timerService;
        private readonly ILogger<DonationService> _logger;
        private static readonly HttpClient HttpClient = new() { Timeout = TimeSpan.FromSeconds(15) };

        private const string SecureKey_Verified = "donor.verified";
        private const string SecureKey_VerifiedAt = "donor.verifiedAt";
        private const string SecureKey_MaskedKey = "donor.maskedKey";

        private bool _isDonor;
        private bool _initialized;
        private DonationSettings _settings = new();

        public bool IsDonor => _isDonor;

        public bool ShouldShowDonationPrompt
        {
            get
            {
                if (_isDonor) return false;
                if (_settings.SessionCount < 5) return false;
                if (_settings.TotalUsageMinutes < 60) return false;

                if (_settings.FirstInstallDate.HasValue)
                {
                    var daysSinceInstall = (DateTime.UtcNow - _settings.FirstInstallDate.Value).TotalDays;
                    if (daysSinceInstall < 7) return false;
                }

                if (_settings.LastPromptDismissedAt.HasValue)
                {
                    var daysSinceDismissed = (DateTime.UtcNow - _settings.LastPromptDismissedAt.Value).TotalDays;
                    if (daysSinceDismissed < 7) return false;
                }

                if (_timerService.IsAnyNotificationActive) return false;

                return true;
            }
        }

        public string DonationUrl => _settings.DonationUrl;

        public event EventHandler? DonorStatusChanged;
        public event EventHandler? PromptVisibilityChanged;

        public DonationService(
            IConfigurationService configurationService,
            ISecureStorageService secureStorage,
            ITimerService timerService,
            ILogger<DonationService> logger)
        {
            _configurationService = configurationService;
            _secureStorage = secureStorage;
            _timerService = timerService;
            _logger = logger;
        }

        public async Task InitializeAsync()
        {
            if (_initialized) return;

            try
            {
                var config = await _configurationService.LoadConfigurationAsync();
                _settings = config.Donation;

                if (_settings.FirstInstallDate == null)
                {
                    _settings.FirstInstallDate = DateTime.UtcNow;
                    await _configurationService.SaveConfigurationAsync(config);
                }

                var verifiedStr = await _secureStorage.GetAsync(SecureKey_Verified);
                _isDonor = verifiedStr == "true";

                _initialized = true;
                _logger.LogInformation("Donation service initialized. IsDonor={IsDonor}, Sessions={Sessions}, Usage={Minutes}min",
                    _isDonor, _settings.SessionCount, _settings.TotalUsageMinutes);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to initialize donation service");
                _initialized = true;
            }
        }

        public void IncrementSessionCount()
        {
            _settings.SessionCount++;
            SaveSettingsAsync().ConfigureAwait(false);
        }

        public void AddUsageMinutes(long minutes)
        {
            _settings.TotalUsageMinutes += minutes;
            SaveSettingsAsync().ConfigureAwait(false);
        }

        public void RecordPromptShown()
        {
            _settings.LastPromptShownAt = DateTime.UtcNow;
            SaveSettingsAsync().ConfigureAwait(false);
        }

        public void RecordPromptDismissed()
        {
            _settings.LastPromptDismissedAt = DateTime.UtcNow;
            _settings.PromptDismissCount++;
            SaveSettingsAsync().ConfigureAwait(false);
            PromptVisibilityChanged?.Invoke(this, EventArgs.Empty);
        }

        public async Task<DonationCodeValidationResult> ValidateDonationCodeAsync(string licenseKey)
        {
            if (string.IsNullOrWhiteSpace(licenseKey))
            {
                return new DonationCodeValidationResult
                {
                    IsValid = false,
                    ErrorMessage = "Please enter a license key."
                };
            }

            try
            {
                var payload = JsonSerializer.Serialize(new
                {
                    license_key = licenseKey.Trim(),
                    instance_name = "EyeRest"
                });

                var content = new StringContent(payload, Encoding.UTF8, "application/json");
                var response = await HttpClient.PostAsync(
                    "https://api.lemonsqueezy.com/v1/licenses/validate", content);

                var responseBody = await response.Content.ReadAsStringAsync();
                using var doc = JsonDocument.Parse(responseBody);
                var root = doc.RootElement;

                if (response.IsSuccessStatusCode && root.TryGetProperty("valid", out var validProp) && validProp.GetBoolean())
                {
                    var maskedCode = licenseKey.Length >= 4
                        ? "****" + licenseKey[^4..]
                        : "****";

                    await _secureStorage.SetAsync(SecureKey_Verified, "true");
                    await _secureStorage.SetAsync(SecureKey_VerifiedAt, DateTime.UtcNow.ToString("O"));
                    await _secureStorage.SetAsync(SecureKey_MaskedKey, maskedCode);

                    _isDonor = true;
                    DonorStatusChanged?.Invoke(this, EventArgs.Empty);
                    PromptVisibilityChanged?.Invoke(this, EventArgs.Empty);

                    _logger.LogInformation("Donation code validated successfully (masked: {MaskedCode})", maskedCode);

                    return new DonationCodeValidationResult
                    {
                        IsValid = true,
                        MaskedCode = maskedCode
                    };
                }

                var errorMessage = "Invalid license key.";
                if (root.TryGetProperty("error", out var errorProp))
                    errorMessage = errorProp.GetString() ?? errorMessage;

                return new DonationCodeValidationResult
                {
                    IsValid = false,
                    ErrorMessage = errorMessage
                };
            }
            catch (HttpRequestException)
            {
                return new DonationCodeValidationResult
                {
                    IsValid = false,
                    ErrorMessage = "Unable to verify. Please check your internet connection."
                };
            }
            catch (TaskCanceledException)
            {
                return new DonationCodeValidationResult
                {
                    IsValid = false,
                    ErrorMessage = "Verification timed out. Please try again."
                };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Unexpected error during donation code validation");
                return new DonationCodeValidationResult
                {
                    IsValid = false,
                    ErrorMessage = "An unexpected error occurred. Please try again."
                };
            }
        }

        private async Task SaveSettingsAsync()
        {
            try
            {
                var config = await _configurationService.LoadConfigurationAsync();
                config.Donation = _settings;
                await _configurationService.SaveConfigurationAsync(config);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to save donation settings");
            }
        }
    }
}
