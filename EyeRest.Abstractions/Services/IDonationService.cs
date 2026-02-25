using System;
using System.Threading.Tasks;

namespace EyeRest.Services
{
    public class DonationCodeValidationResult
    {
        public bool IsValid { get; set; }
        public string? ErrorMessage { get; set; }
        public string? MaskedCode { get; set; }
    }

    public interface IDonationService
    {
        bool IsDonor { get; }
        bool ShouldShowDonationPrompt { get; }
        string DonationUrl { get; }

        event EventHandler? DonorStatusChanged;
        event EventHandler? PromptVisibilityChanged;

        Task InitializeAsync();
        void IncrementSessionCount();
        void AddUsageMinutes(long minutes);
        void RecordPromptShown();
        void RecordPromptDismissed();
        Task<DonationCodeValidationResult> ValidateDonationCodeAsync(string licenseKey);
    }
}
