using System.Numerics;
using RedFox.Graphics3D.Rendering.Materials;
using RedFox.Imaging;
using RedFox.Imaging.Primitives;

namespace RedFox.Graphics3D.Rendering;

/// <summary>
/// Draws a single image slice with zoom, pan, and channel isolation. The slice is uploaded in its stored format, so
/// block-compressed data is sampled by the GPU without being decoded on the CPU.
/// </summary>
public sealed class ImageRenderer : IDisposable
{
    private const int ImageTextureSlot = 0;

    private readonly IGraphicsDevice _graphicsDevice;
    private readonly ICommandList _commandList;
    private IGpuPipelineState? _pipeline;
    private IGpuTexture? _texture;
    private ImageSlice? _uploadedSlice;
    private int _viewportWidth = 1;
    private int _viewportHeight = 1;
    private bool _disposed;

    /// <summary>
    /// Gets or sets the color drawn behind the image.
    /// </summary>
    public Vector4 ClearColor { get; set; }

    /// <summary>
    /// Gets a value indicating whether the graphics device can sample the most recently rendered slice.
    /// </summary>
    public bool IsSliceSupported { get; private set; } = true;

    /// <summary>
    /// Initializes a new instance of the <see cref="ImageRenderer"/> class.
    /// </summary>
    /// <param name="graphicsDevice">The graphics device that owns the image resources.</param>
    /// <param name="clearColor">The color drawn behind the image.</param>
    public ImageRenderer(IGraphicsDevice graphicsDevice, Vector4 clearColor)
    {
        _graphicsDevice = graphicsDevice ?? throw new ArgumentNullException(nameof(graphicsDevice));
        _commandList = graphicsDevice.CreateCommandList();
        ClearColor = clearColor;
    }

    /// <summary>
    /// Resizes the active viewport.
    /// </summary>
    /// <param name="width">The viewport width in pixels.</param>
    /// <param name="height">The viewport height in pixels.</param>
    public void Resize(int width, int height)
    {
        _viewportWidth = Math.Max(1, width);
        _viewportHeight = Math.Max(1, height);
    }

    /// <summary>
    /// Renders the slice centered in the viewport.
    /// </summary>
    /// <param name="slice">The slice to draw, or <see langword="null"/> to only clear the viewport.</param>
    /// <param name="offset">The offset of the image center from the viewport center, in pixels with Y pointing down.</param>
    /// <param name="zoom">The number of viewport pixels covered by one texel.</param>
    /// <param name="options">The channel, exposure, normal, tiling, and orientation options.</param>
    public void Render(ImageSlice? slice, Vector2 offset, float zoom, ImageViewOptions options)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);

        UpdateTexture(slice);

        _commandList.Reset();
        _commandList.SetViewport(_viewportWidth, _viewportHeight);
        _commandList.SetRenderTarget(null);
        _commandList.ClearRenderTarget(ClearColor.X, ClearColor.Y, ClearColor.Z, ClearColor.W, 1.0f);

        if (_texture is not null && slice is { } current)
        {
            ImageFormat format = current.Format;
            _commandList.SetPipelineState(EnsurePipeline());
            _commandList.BindTexture(ImageTextureSlot, _texture);
            _commandList.SetUniformInt("ImageTexture", ImageTextureSlot);
            _commandList.SetUniformVector2("ImageSize", new Vector2(current.Width, current.Height));
            _commandList.SetUniformVector2("ViewportSize", new Vector2(_viewportWidth, _viewportHeight));
            _commandList.SetUniformVector2("Offset", offset);
            _commandList.SetUniformFloat("Zoom", zoom);
            _commandList.SetUniformVector4("ChannelMask", options.ChannelMask);
            _commandList.SetUniformFloat("Exposure", SupportsExposure(format) ? options.Exposure : 0.0f);
            _commandList.SetUniformInt("ReconstructNormal", options.ReconstructNormal && SupportsNormalReconstruction(format) ? 1 : 0);
            _commandList.SetUniformInt("Tile", options.Tile ? 1 : 0);
            _commandList.SetUniformInt("FlipY", options.FlipY ? 1 : 0);
            _commandList.SetUniformInt("IsSingleChannel", format is ImageFormat.BC4Typeless or ImageFormat.BC4Unorm or ImageFormat.BC4Snorm ? 1 : 0);
            _commandList.SetUniformInt("IsSigned", format is ImageFormat.BC4Snorm or ImageFormat.BC5Snorm ? 1 : 0);
            _commandList.SetUniformInt("EncodeSrgb", ImageFormatInfo.IsSrgb(format) ? 1 : 0);
            _commandList.Draw(3, 0);
        }

        _graphicsDevice.Submit(_commandList);
    }

    /// <summary>
    /// Determines whether <see cref="ImageViewOptions.Exposure"/> applies to the format, which is true for high dynamic range formats.
    /// </summary>
    /// <param name="format">The image format to inspect.</param>
    /// <returns><see langword="true"/> when the format stores values outside the zero to one range.</returns>
    public static bool SupportsExposure(ImageFormat format)
    {
        return format is ImageFormat.BC6HTypeless or ImageFormat.BC6HUF16 or ImageFormat.BC6HSF16 or ImageFormat.R9G9B9E5SharedExp || format.ToString().EndsWith("Float", StringComparison.Ordinal);
    }

    /// <summary>
    /// Determines whether <see cref="ImageViewOptions.ReconstructNormal"/> applies to the format, which is true for two-channel formats.
    /// </summary>
    /// <param name="format">The image format to inspect.</param>
    /// <returns><see langword="true"/> when the format stores only red and green channels.</returns>
    public static bool SupportsNormalReconstruction(ImageFormat format)
    {
        return format is ImageFormat.BC5Typeless or ImageFormat.BC5Unorm or ImageFormat.BC5Snorm or ImageFormat.R8G8Unorm or ImageFormat.R8G8Snorm or ImageFormat.R16G16Unorm or ImageFormat.R16G16Snorm or ImageFormat.R16G16Float;
    }

    /// <inheritdoc/>
    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _texture?.Dispose();
        _texture = null;
        _pipeline?.Dispose();
        _pipeline = null;
        _disposed = true;
    }

    private void UpdateTexture(ImageSlice? slice)
    {
        if (_uploadedSlice is { } uploaded && slice is { } requested && uploaded.Pixels.Equals(requested.Pixels) && uploaded.Format == requested.Format && uploaded.Width == requested.Width && uploaded.Height == requested.Height)
        {
            return;
        }

        _texture?.Dispose();
        _texture = null;
        _uploadedSlice = slice;
        IsSliceSupported = true;

        if (slice is not { } current)
        {
            return;
        }

        IsSliceSupported = _graphicsDevice.SupportsFormat(current.Format, TextureUsage.Sampled);
        if (IsSliceSupported)
        {
            _texture = _graphicsDevice.CreateTexture(current.Width, current.Height, current.Format, TextureUsage.Sampled, current.PixelSpan);
        }
    }

    private IGpuPipelineState EnsurePipeline()
    {
        if (_pipeline is not null)
        {
            return _pipeline;
        }

        if (_graphicsDevice.MaterialTypes is not IMaterialPipelineProvider pipelineProvider)
        {
            throw new InvalidOperationException($"Material registry '{_graphicsDevice.MaterialTypes.GetType().Name}' does not provide runtime pipeline services.");
        }

        _pipeline = pipelineProvider.CreatePipeline(_graphicsDevice, "Image");
        return _pipeline;
    }
}
