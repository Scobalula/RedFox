using Avalonia.Controls;
using Avalonia.Interactivity;
using RedFox.GameExtraction.UI.ViewModels;

namespace RedFox.GameExtraction.UI.Views;

/// <summary>
/// Asks the user to confirm an action.
/// </summary>
public partial class ConfirmWindow : Window
{
    /// <summary>
    /// Creates an uninitialized confirm window.
    /// </summary>
    public ConfirmWindow()
    {
        InitializeComponent();
    }

    /// <summary>
    /// Shows a confirmation prompt over the owner window.
    /// </summary>
    /// <param name="owner">The window that owns the prompt.</param>
    /// <param name="request">The prompt to show.</param>
    /// <returns><see langword="true"/> when confirmed, <see langword="false"/> for the alternate choice, or <see langword="null"/> when cancelled.</returns>
    public static Task<bool?> ShowAsync(Window owner, ConfirmRequest request)
    {
        ConfirmWindow window = new();

        window.Title						= request.Title;
        window.MessageText.Text				= request.Message;
        window.ConfirmButton.Content		= request.ConfirmText;
        window.AlternateButton.Content		= request.AlternateText;
        window.AlternateButton.IsVisible	= request.AlternateText is not null;

        return window.ShowDialog<bool?>(owner);
    }

    private void OnConfirmClick(object? sender, RoutedEventArgs e)
    {
        Close(true);
    }

    private void OnAlternateClick(object? sender, RoutedEventArgs e)
    {
        Close(false);
    }

    private void OnCancelClick(object? sender, RoutedEventArgs e)
    {
        Close(null);
    }
}
