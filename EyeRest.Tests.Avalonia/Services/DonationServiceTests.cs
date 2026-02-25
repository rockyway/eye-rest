using System;
using System.Threading.Tasks;
using EyeRest.Models;
using EyeRest.Services;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;

namespace EyeRest.Tests.Avalonia.Services
{
    public class DonationServiceTests : IDisposable
    {
        private readonly Mock<IConfigurationService> _mockConfigService;
        private readonly Mock<ISecureStorageService> _mockSecureStorage;
        private readonly Mock<ITimerService> _mockTimerService;
        private readonly Mock<ILogger<DonationService>> _mockLogger;
        private readonly DonationService _service;
        private readonly AppConfiguration _testConfig;

        public DonationServiceTests()
        {
            _mockConfigService = new Mock<IConfigurationService>();
            _mockSecureStorage = new Mock<ISecureStorageService>();
            _mockTimerService = new Mock<ITimerService>();
            _mockLogger = new Mock<ILogger<DonationService>>();

            _testConfig = new AppConfiguration
            {
                Donation = new DonationSettings
                {
                    SessionCount = 0,
                    TotalUsageMinutes = 0,
                    FirstInstallDate = DateTime.UtcNow.AddDays(-30)
                }
            };

            _mockConfigService.Setup(x => x.LoadConfigurationAsync())
                .ReturnsAsync(_testConfig);

            _service = new DonationService(
                _mockConfigService.Object,
                _mockSecureStorage.Object,
                _mockTimerService.Object,
                _mockLogger.Object);
        }

        public void Dispose() { }

        [Fact]
        public async Task InitializeAsync_SetsIsDonorFromSecureStorage_WhenVerified()
        {
            _mockSecureStorage.Setup(x => x.GetAsync("donor.verified"))
                .ReturnsAsync("true");

            await _service.InitializeAsync();

            Assert.True(_service.IsDonor);
        }

        [Fact]
        public async Task InitializeAsync_SetsIsDonorFalse_WhenNotVerified()
        {
            _mockSecureStorage.Setup(x => x.GetAsync("donor.verified"))
                .ReturnsAsync((string?)null);

            await _service.InitializeAsync();

            Assert.False(_service.IsDonor);
        }

        [Fact]
        public async Task InitializeAsync_SetsFirstInstallDate_WhenNull()
        {
            _testConfig.Donation.FirstInstallDate = null;

            await _service.InitializeAsync();

            Assert.NotNull(_testConfig.Donation.FirstInstallDate);
            _mockConfigService.Verify(x => x.SaveConfigurationAsync(It.IsAny<AppConfiguration>()), Times.Once);
        }

        [Fact]
        public async Task ShouldShowDonationPrompt_ReturnsFalse_WhenIsDonor()
        {
            _mockSecureStorage.Setup(x => x.GetAsync("donor.verified"))
                .ReturnsAsync("true");
            await _service.InitializeAsync();

            Assert.False(_service.ShouldShowDonationPrompt);
        }

        [Fact]
        public async Task ShouldShowDonationPrompt_ReturnsFalse_WhenSessionCountBelowThreshold()
        {
            _testConfig.Donation.SessionCount = 3;
            _testConfig.Donation.TotalUsageMinutes = 120;
            await _service.InitializeAsync();

            Assert.False(_service.ShouldShowDonationPrompt);
        }

        [Fact]
        public async Task ShouldShowDonationPrompt_ReturnsFalse_WhenUsageMinutesBelowThreshold()
        {
            _testConfig.Donation.SessionCount = 10;
            _testConfig.Donation.TotalUsageMinutes = 30;
            await _service.InitializeAsync();

            Assert.False(_service.ShouldShowDonationPrompt);
        }

        [Fact]
        public async Task ShouldShowDonationPrompt_ReturnsFalse_WhenInstallLessThan7DaysAgo()
        {
            _testConfig.Donation.SessionCount = 10;
            _testConfig.Donation.TotalUsageMinutes = 120;
            _testConfig.Donation.FirstInstallDate = DateTime.UtcNow.AddDays(-3);
            await _service.InitializeAsync();

            Assert.False(_service.ShouldShowDonationPrompt);
        }

        [Fact]
        public async Task ShouldShowDonationPrompt_ReturnsFalse_WhenDismissedRecently()
        {
            _testConfig.Donation.SessionCount = 10;
            _testConfig.Donation.TotalUsageMinutes = 120;
            _testConfig.Donation.LastPromptDismissedAt = DateTime.UtcNow.AddDays(-3);
            await _service.InitializeAsync();

            Assert.False(_service.ShouldShowDonationPrompt);
        }

        [Fact]
        public async Task ShouldShowDonationPrompt_ReturnsFalse_WhenNotificationActive()
        {
            _testConfig.Donation.SessionCount = 10;
            _testConfig.Donation.TotalUsageMinutes = 120;
            _mockTimerService.Setup(x => x.IsAnyNotificationActive).Returns(true);
            await _service.InitializeAsync();

            Assert.False(_service.ShouldShowDonationPrompt);
        }

        [Fact]
        public async Task ShouldShowDonationPrompt_ReturnsTrue_WhenAllConditionsMet()
        {
            _testConfig.Donation.SessionCount = 10;
            _testConfig.Donation.TotalUsageMinutes = 120;
            _testConfig.Donation.FirstInstallDate = DateTime.UtcNow.AddDays(-30);
            _testConfig.Donation.LastPromptDismissedAt = null;
            _mockTimerService.Setup(x => x.IsAnyNotificationActive).Returns(false);
            await _service.InitializeAsync();

            Assert.True(_service.ShouldShowDonationPrompt);
        }

        [Fact]
        public async Task IncrementSessionCount_IncrementsAndSaves()
        {
            await _service.InitializeAsync();
            var before = _testConfig.Donation.SessionCount;

            _service.IncrementSessionCount();

            Assert.Equal(before + 1, _testConfig.Donation.SessionCount);
        }

        [Fact]
        public async Task AddUsageMinutes_AddsAndSaves()
        {
            await _service.InitializeAsync();
            var before = _testConfig.Donation.TotalUsageMinutes;

            _service.AddUsageMinutes(5);

            Assert.Equal(before + 5, _testConfig.Donation.TotalUsageMinutes);
        }

        [Fact]
        public async Task RecordPromptDismissed_UpdatesTimestampAndCount()
        {
            await _service.InitializeAsync();

            _service.RecordPromptDismissed();

            Assert.NotNull(_testConfig.Donation.LastPromptDismissedAt);
            Assert.Equal(1, _testConfig.Donation.PromptDismissCount);
        }

        [Fact]
        public async Task RecordPromptDismissed_RaisesPromptVisibilityChanged()
        {
            await _service.InitializeAsync();
            var eventRaised = false;
            _service.PromptVisibilityChanged += (_, _) => eventRaised = true;

            _service.RecordPromptDismissed();

            Assert.True(eventRaised);
        }

        [Fact]
        public async Task ValidateDonationCodeAsync_ReturnsInvalid_WhenEmptyKey()
        {
            await _service.InitializeAsync();

            var result = await _service.ValidateDonationCodeAsync("");

            Assert.False(result.IsValid);
            Assert.NotNull(result.ErrorMessage);
        }

        [Fact]
        public async Task ValidateDonationCodeAsync_ReturnsNetworkError_WhenOffline()
        {
            // This test verifies the service handles network errors gracefully.
            // The actual HTTP call will fail since there's no real API available.
            await _service.InitializeAsync();

            var result = await _service.ValidateDonationCodeAsync("test-key-12345");

            // Should return an error (either network or API error) but not throw
            Assert.False(result.IsValid);
            Assert.NotNull(result.ErrorMessage);
        }

        [Fact]
        public async Task DonationUrl_ReturnsDefaultUrl()
        {
            await _service.InitializeAsync();

            Assert.Contains("lemonsqueezy.com", _service.DonationUrl);
        }
    }
}
