using Avalonia.Controls;
using RedFox.GameExtraction;
using RedFox.GameExtraction.UI;
using RedFox.GameExtraction.UI.ViewModels;

namespace RedFox.GameExtraction.UI.Views;

public partial class PreviewWindow : Window
{
    private readonly PreviewWindowViewModel _viewModel = new();

    public PreviewWindow()
    {
        DataContext = _viewModel;
        InitializeComponent();
    }

    /// <summary>
    /// Sets the camera mode from the current settings.
    /// </summary>
    public void ApplySettings(SettingsBase settings)
    {
        //_viewModel.ViewportCameraMode = settings.ViewportCameraMode;
    }

    /// <summary>
    /// Update the preview with the given selected assets.
    /// </summary>
    public async Task UpdatePreviewAsync(IReadOnlyList<IAssetEntry> assets, IPreviewHandler handler)
    {
        await _viewModel.UpdatePreviewAsync(assets, handler);
    }
}
