using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using RedFox.GameExtraction.UI.ViewModels;

namespace RedFox.GameExtraction.UI.Views;

/// <summary>
/// Preview window bound to a <see cref="ViewModels.PreviewViewModel"/>.
/// </summary>
public partial class PreviewWindow : Window
{
    /// <summary>
    /// Initializes a new instance of the <see cref="PreviewWindow"/> class.
    /// </summary>
    public PreviewWindow()
    {
        InitializeComponent();
        AddHandler(KeyDownEvent, OnPreviewKeyDown, RoutingStrategies.Tunnel);
    }

    private void OnPreviewKeyDown(object? sender, KeyEventArgs e)
    {
        if (DataContext is PreviewViewModel { Content: IPreviewKeyHandler handler } && handler.HandleKey(e.Key, e.KeyModifiers))
        {
            e.Handled = true;
        }
    }
}
