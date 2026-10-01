using CommunityToolkit.Mvvm.ComponentModel;
using RedFox.Graphics3D;
using RedFox.Graphics3D.Rendering;

namespace RedFox.GameExtraction.UI.ViewModels;

/// <summary>
/// Viewer preferences shared by every scene preview created by the same previewer, so they persist between assets.
/// </summary>
public sealed partial class ScenePreviewSettings : ObservableObject
{
    /// <summary>
    /// Gets or sets the scene up axis.
    /// </summary>
    [ObservableProperty]
    public partial SceneUpAxis UpAxis { get; set; } = SceneUpAxis.Y;

    /// <summary>
    /// Gets or sets the renderer skinning mode.
    /// </summary>
    [ObservableProperty]
    public partial SkinningMode SkinningMode { get; set; } = SkinningMode.Linear;

    /// <summary>
    /// Gets or sets a value indicating whether lighting follows the camera.
    /// </summary>
    [ObservableProperty]
    public partial bool UseViewBasedLighting { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether animation playback is paused.
    /// </summary>
    [ObservableProperty]
    public partial bool IsAnimationPaused { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether the ground grid is drawn.
    /// </summary>
    [ObservableProperty]
    public partial bool ShowGrid { get; set; } = true;
}
