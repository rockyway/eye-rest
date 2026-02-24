using Avalonia.Controls;
using Avalonia.Interactivity;

namespace EyeRest.UI.Views;

public partial class ConfirmDialog : Window
{
    public bool DialogResult { get; private set; }

    public ConfirmDialog()
    {
        InitializeComponent();
        YesButton.Click += OnYesClick;
        NoButton.Click += OnNoClick;
    }

    public ConfirmDialog(string message) : this()
    {
        MessageText.Text = message;
    }

    public ConfirmDialog(string title, string message) : this(message)
    {
        Title = title;
    }

    private void OnYesClick(object? sender, RoutedEventArgs e)
    {
        DialogResult = true;
        Close();
    }

    private void OnNoClick(object? sender, RoutedEventArgs e)
    {
        DialogResult = false;
        Close();
    }
}
