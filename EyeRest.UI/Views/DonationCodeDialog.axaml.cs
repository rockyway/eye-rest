using System;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Media;
using EyeRest.Services;
using Microsoft.Extensions.DependencyInjection;

namespace EyeRest.UI.Views;

public partial class DonationCodeDialog : Window
{
    private readonly IDonationService? _donationService;

    public DonationCodeDialog()
    {
        InitializeComponent();
        _donationService = App.Services?.GetService<IDonationService>();
    }

    private void OnDragMove(object? sender, PointerPressedEventArgs e)
    {
        if (e.GetCurrentPoint(this).Properties.IsLeftButtonPressed)
            BeginMoveDrag(e);
    }

    private async void OnValidateClick(object? sender, RoutedEventArgs e)
    {
        var key = LicenseKeyInput.Text?.Trim();
        if (string.IsNullOrEmpty(key))
        {
            ShowFeedback("Please enter a license key.", isError: true);
            return;
        }

        if (_donationService == null)
        {
            ShowFeedback("Service unavailable. Please restart the app.", isError: true);
            return;
        }

        ValidateButton.IsEnabled = false;
        ShowFeedback("Validating...", isError: false);

        try
        {
            var result = await _donationService.ValidateDonationCodeAsync(key);

            if (result.IsValid)
            {
                ShowFeedback("Thank you for your support!", isError: false);
                await System.Threading.Tasks.Task.Delay(1500);
                Close();
            }
            else
            {
                ShowFeedback(result.ErrorMessage ?? "Invalid license key.", isError: true);
            }
        }
        catch (Exception)
        {
            ShowFeedback("An error occurred. Please try again.", isError: true);
        }
        finally
        {
            ValidateButton.IsEnabled = true;
        }
    }

    private void OnCancelClick(object? sender, RoutedEventArgs e)
    {
        Close();
    }

    private void ShowFeedback(string message, bool isError)
    {
        FeedbackText.Text = message;
        FeedbackText.Foreground = isError
            ? (IBrush)this.FindResource("ErrorBrush")! ?? Brushes.Red
            : (IBrush)this.FindResource("SuccessBrush")! ?? Brushes.Green;
    }
}
