using System.Globalization;
using System.Numerics;
using Avalonia;
using CommunityToolkit.Mvvm.ComponentModel;
using RedFox.Graphics3D;
using RedFox.Graphics3D.Rendering;
using RedFox.Imaging;
using RedFox.Imaging.IO;

namespace RedFox.GameExtraction.UI.ViewModels;

/// <summary>
/// Drives the image preview: mip and slice selection, format details, and the pixel readout under the pointer. Viewer preferences live in the shared <see cref="ImagePreviewSettings"/>.
/// Textures without loaded data are read on a background thread.
/// </summary>
public sealed partial class ImagePreviewViewModel : ObservableObject
{
    private static readonly string[] CubeFaceNames = ["+X", "-X", "+Y", "-Y", "+Z", "-Z"];

    private Image? _image;

    /// <summary>
    /// Gets the mip level names shown in the mip selector.
    /// </summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasMultipleMips))]
    public partial IReadOnlyList<string> MipNames { get; private set; } = [];

    /// <summary>
    /// Gets or sets the index of the displayed mip level.
    /// </summary>
    [ObservableProperty]
    public partial int SelectedMipIndex { get; set; }

    /// <summary>
    /// Gets the array, cube face, or depth slice names shown in the slice selector.
    /// </summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasMultipleLayers))]
    public partial IReadOnlyList<string> LayerNames { get; private set; } = [];

    /// <summary>
    /// Gets or sets the index of the displayed array, cube face, or depth slice.
    /// </summary>
    [ObservableProperty]
    public partial int SelectedLayerIndex { get; set; }

    /// <summary>
    /// Gets the slice bound to the renderer.
    /// </summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(PixelDisplay))]
    public partial ImageSlice? Slice { get; private set; }

    /// <summary>
    /// Gets the coarsest mip used while the selected slice is uploaded.
    /// </summary>
    public ImageSlice? FallbackSlice { get; private set; }

    /// <summary>
    /// Gets a value indicating whether the format stores values outside the zero to one range.
    /// </summary>
    [ObservableProperty]
    public partial bool IsHighDynamicRange { get; private set; }

    /// <summary>
    /// Gets a value indicating whether the format stores only red and green channels.
    /// </summary>
    [ObservableProperty]
    public partial bool IsTwoChannel { get; private set; }

    /// <summary>
    /// Gets or sets the texel under the pointer.
    /// </summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(PixelDisplay))]
    public partial PixelPoint? PointerTexel { get; set; }

    /// <summary>
    /// Gets the dimensions, format, mip, slice, and size summary of the image.
    /// </summary>
    [ObservableProperty]
    public partial string StatsDisplay { get; private set; } = string.Empty;

    /// <summary>
    /// Gets the image format name.
    /// </summary>
    [ObservableProperty]
    public partial string FormatDisplay { get; private set; } = string.Empty;

    /// <summary>
    /// Gets a value indicating whether texture data is being read.
    /// </summary>
    [ObservableProperty]
    public partial bool IsLoading { get; private set; }

    /// <summary>
    /// Gets the error raised while reading texture data, if any.
    /// </summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasError))]
    public partial string? ErrorMessage { get; private set; }

    /// <summary>
    /// Gets the viewer preferences shared with other image previews.
    /// </summary>
    public ImagePreviewSettings Settings { get; }

    /// <summary>
    /// Gets a value indicating whether the image has more than one mip level.
    /// </summary>
    public bool HasMultipleMips => MipNames.Count > 1;

    /// <summary>
    /// Gets a value indicating whether the image has more than one array, cube face, or depth slice.
    /// </summary>
    public bool HasMultipleLayers => LayerNames.Count > 1;

    /// <summary>
    /// Gets a value indicating whether reading texture data failed.
    /// </summary>
    public bool HasError => ErrorMessage is not null;

    /// <summary>
    /// Gets the coordinates and value of the texel under the pointer.
    /// </summary>
    public string PixelDisplay
    {
        get
        {
            if (PointerTexel is not { } texel || Slice is not { } slice)
            {
                return string.Empty;
            }

            if (!PixelCodecs.TryGetCodec(slice.Format, out _))
            {
                return $"{texel.X}, {texel.Y}";
            }

            Vector4 pixel = slice.GetPixel(texel.X, texel.Y);
            return string.Create(CultureInfo.InvariantCulture, $"{texel.X}, {texel.Y}  •  {pixel.X:0.000}  {pixel.Y:0.000}  {pixel.Z:0.000}  {pixel.W:0.000}");
        }
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="ImagePreviewViewModel"/> class.
    /// </summary>
    /// <param name="image">The image to preview.</param>
    /// <param name="settings">The viewer preferences shared with other image previews.</param>
    public ImagePreviewViewModel(Image image, ImagePreviewSettings settings)
    {
        ArgumentNullException.ThrowIfNull(image);
        Settings = settings ?? throw new ArgumentNullException(nameof(settings));
        ShowImage(image);
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="ImagePreviewViewModel"/> class and reads the texture data in the background.
    /// </summary>
    /// <param name="texture">The texture whose image is previewed.</param>
    /// <param name="translatorManager">The translators used to read the texture.</param>
    /// <param name="settings">The viewer preferences shared with other image previews.</param>
    public ImagePreviewViewModel(Texture texture, ImageTranslatorManager translatorManager, ImagePreviewSettings settings)
    {
        ArgumentNullException.ThrowIfNull(texture);
        ArgumentNullException.ThrowIfNull(translatorManager);
        Settings = settings ?? throw new ArgumentNullException(nameof(settings));
        _ = LoadAsync(texture, translatorManager);
    }

    partial void OnSelectedMipIndexChanged(int value)
    {
        if (_image is { Depth: > 1 } image && value >= 0)
        {
            int layer = SelectedLayerIndex;
            LayerNames = CreateLayerNames(image, value);
            SelectedLayerIndex = Math.Clamp(layer, 0, LayerNames.Count - 1);
        }

        UpdateSlice();
    }

    partial void OnSelectedLayerIndexChanged(int value)
    {
        UpdateSlice();
    }

    private static IReadOnlyList<string> CreateMipNames(Image image)
    {
        return [.. Enumerable.Range(0, image.MipLevels).Select(mip => $"{mip}  ({Math.Max(1, image.Width >> mip)} × {Math.Max(1, image.Height >> mip)})")];
    }

    private static IReadOnlyList<string> CreateLayerNames(Image image, int mipLevel)
    {
        if (image.Depth > 1)
        {
            return [.. Enumerable.Range(0, Math.Max(1, image.Depth >> mipLevel)).Select(depth => $"Depth {depth}")];
        }

        if (image.IsCubemap)
        {
            return [.. Enumerable.Range(0, image.ArraySize).Select(face => image.ArraySize > 6 ? $"Cube {face / 6} {CubeFaceNames[face % 6]}" : CubeFaceNames[face % 6])];
        }

        return [.. Enumerable.Range(0, image.ArraySize).Select(index => $"Slice {index}")];
    }

    private static string CreateStats(Image image)
    {
        string depth = image.Depth > 1 ? $" × {image.Depth}" : string.Empty;
        string layers = image.IsCubemap ? $"{image.ArraySize / 6:N0} cube(s)" : $"{image.ArraySize:N0} slice(s)";
        double megabytes = image.PixelMemory.Length / (1024.0 * 1024.0);
        return string.Create(CultureInfo.InvariantCulture, $"{image.Width} × {image.Height}{depth}  •  {image.Format}  •  {image.MipLevels:N0} mips  •  {layers}  •  {megabytes:0.00} MB");
    }

    private async Task LoadAsync(Texture texture, ImageTranslatorManager translatorManager)
    {
        IsLoading = true;

        try
        {
            Image? image = texture.Data ?? await Task.Run(() => texture.ImageLoader?.Load(texture, translatorManager)).ConfigureAwait(true);
            if (image is null)
            {
                ErrorMessage = $"{texture.Name} has no image data.";
                return;
            }

            texture.Data = image;
            ShowImage(image);
        }
        catch (Exception exception)
        {
            ErrorMessage = exception.Message;
        }
        finally
        {
            IsLoading = false;
        }
    }

    private void ShowImage(Image image)
    {
        _image = image;
        FormatDisplay = image.Format.ToString();
        IsHighDynamicRange = ImageRenderer.SupportsExposure(image.Format);
        IsTwoChannel = ImageRenderer.SupportsNormalReconstruction(image.Format);
        StatsDisplay = CreateStats(image);
        MipNames = CreateMipNames(image);
        LayerNames = CreateLayerNames(image, 0);
        SelectedMipIndex = 0;
        SelectedLayerIndex = 0;
        UpdateSlice();
    }

    private void UpdateSlice()
    {
        if (_image is not { } image || SelectedMipIndex < 0 || SelectedMipIndex >= image.MipLevels || SelectedLayerIndex < 0 || SelectedLayerIndex >= LayerNames.Count)
        {
            Slice = null;
            FallbackSlice = null;
            OnPropertyChanged(nameof(FallbackSlice));
            return;
        }

        Slice = image.Depth > 1 ? image.GetSlice(SelectedMipIndex, 0, SelectedLayerIndex) : image.GetSlice(SelectedMipIndex, SelectedLayerIndex);
        int fallbackMip = image.MipLevels - 1;
        int fallbackLayer = image.Depth > 1 ? Math.Clamp(SelectedLayerIndex, 0, Math.Max(1, image.Depth >> fallbackMip) - 1) : SelectedLayerIndex;
        FallbackSlice = image.Depth > 1 ? image.GetSlice(fallbackMip, 0, fallbackLayer) : image.GetSlice(fallbackMip, fallbackLayer);
        OnPropertyChanged(nameof(FallbackSlice));
    }
}
