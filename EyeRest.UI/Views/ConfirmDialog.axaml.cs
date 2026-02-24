using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Threading;

namespace EyeRest.UI.Views;

public partial class ConfirmDialog : Window
{
    public bool DialogResult { get; private set; }

    public ConfirmDialog()
    {
        InitializeComponent();
        YesButton.Click += OnYesClick;
        NoButton.Click += OnNoClick;

        // ESC key handling using tunnel strategy (catches before child controls)
        AddHandler(KeyDownEvent, OnKeyDown, RoutingStrategies.Tunnel);
    }

    protected override void OnOpened(System.EventArgs e)
    {
        base.OnOpened(e);
        // Defer activation so the window is fully rendered before requesting focus
        Dispatcher.UIThread.Post(() =>
        {
            Activate();
            Focusable = true;
            Focus();
        }, DispatcherPriority.Input);
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

    private void OnKeyDown(object? sender, KeyEventArgs e)
    {
        if (e.Key == Key.Escape)
        {
            DialogResult = false;
            Close();
            e.Handled = true;
        }
    }
}
