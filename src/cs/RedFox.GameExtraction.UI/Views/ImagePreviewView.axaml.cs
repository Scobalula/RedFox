using Avalonia.Controls;
using Avalonia.Interactivity;

namespace RedFox.GameExtraction.UI.Views;

/// <summary>
/// Hosts the OpenGL image renderer for a <see cref="ViewModels.ImagePreviewViewModel"/>.
/// </summary>
public partial class ImagePreviewView : UserControl
{
    /// <summary>
    /// Initializes a new instance of the <see cref="ImagePreviewView"/> class.
    /// </summary>
    public ImagePreviewView()
    {
        InitializeComponent();
    }

    private void OnFitClick(object? sender, RoutedEventArgs e) => Viewer.FitToView();

    private void OnActualSizeClick(object? sender, RoutedEventArgs e) => Viewer.ShowActualSize();
}
