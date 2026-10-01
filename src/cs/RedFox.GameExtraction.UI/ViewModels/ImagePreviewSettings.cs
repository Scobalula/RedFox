using System.Globalization;
using System.Numerics;
using CommunityToolkit.Mvvm.ComponentModel;
using RedFox.Graphics3D.Rendering;

namespace RedFox.GameExtraction.UI.ViewModels;

/// <summary>
/// Viewer preferences shared by every image preview created by the same previewer, so they persist between assets.
/// </summary>
public sealed partial class ImagePreviewSettings : ObservableObject
{
    /// <summary>
    /// Gets or sets a value indicating whether the red channel is displayed.
    /// </summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(Options))]
    public partial bool ShowRed { get; set; } = true;

    /// <summary>
    /// Gets or sets a value indicating whether the green channel is displayed.
    /// </summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(Options))]
    public partial bool ShowGreen { get; set; } = true;

    /// <summary>
    /// Gets or sets a value indicating whether the blue channel is displayed.
    /// </summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(Options))]
    public partial bool ShowBlue { get; set; } = true;

    /// <summary>
    /// Gets or sets a value indicating whether the alpha channel is displayed.
    /// </summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(Options))]
    public partial bool ShowAlpha { get; set; } = true;

    /// <summary>
    /// Gets or sets the exposure adjustment in stops applied to high dynamic range images.
    /// </summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(Options), nameof(ExposureDisplay))]
    public partial double Exposure { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether the blue channel is rebuilt for two-channel normal maps.
    /// </summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(Options))]
    public partial bool ReconstructNormal { get; set; } = true;

    /// <summary>
    /// Gets or sets a value indicating whether the image is repeated in a three by three grid.
    /// </summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(Options))]
    public partial bool Tile { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether the image is displayed upside down.
    /// </summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(Options))]
    public partial bool FlipY { get; set; }

    /// <summary>
    /// Gets the options bound to the renderer.
    /// </summary>
    public ImageViewOptions Options => new(new Vector4(ShowRed ? 1.0f : 0.0f, ShowGreen ? 1.0f : 0.0f, ShowBlue ? 1.0f : 0.0f, ShowAlpha ? 1.0f : 0.0f), (float)Exposure, ReconstructNormal, Tile, FlipY);

    /// <summary>
    /// Gets the exposure display.
    /// </summary>
    public string ExposureDisplay => string.Create(CultureInfo.InvariantCulture, $"{Exposure:+0.0;-0.0;0.0} EV");
}
