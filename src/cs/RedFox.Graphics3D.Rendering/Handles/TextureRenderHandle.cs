using System;
using System.Numerics;
using RedFox.Graphics2D;
using RedFox.Graphics3D.Rendering;

namespace RedFox.Graphics3D.Rendering.Handles;

/// <summary>
/// Owns a backend texture resource for a texture node.
/// </summary>
/// <remarks>
/// Initializes a new instance of the <see cref="TextureRenderHandle"/> class.
/// </remarks>
/// <param name="graphicsDevice">The graphics device that creates texture resources.</param>
/// <param name="texture">The texture node represented by this handle.</param>
internal sealed class TextureRenderHandle(IGraphicsDevice graphicsDevice, Texture texture) : RenderHandle
{
    private readonly IGraphicsDevice _graphicsDevice = graphicsDevice ?? throw new ArgumentNullException(nameof(graphicsDevice));
    private readonly Texture _texture = texture ?? throw new ArgumentNullException(nameof(texture));

    private IGpuTexture? _gpuTexture;
    private int _arraySize;
    private ImageFormat _format;
    private int _height;
    private Image? _failedImage;
    private bool _isCubemap;
    private Image? _image;
    private string? _lastLoadAttemptPath;
    private int _mipLevels;
    private int _payloadLength;
    private ulong _lastUpdateFrameIndex = ulong.MaxValue;
    private int _width;

    /// <summary>
    /// Returns whether this handle belongs to the supplied graphics device.
    /// </summary>
    /// <param name="graphicsDevice">The graphics device to compare.</param>
    /// <returns><see langword="true"/> when the handle belongs to the supplied device; otherwise <see langword="false"/>.</returns>
    internal bool IsOwnedBy(IGraphicsDevice graphicsDevice)
    {
        ArgumentNullException.ThrowIfNull(graphicsDevice);
        return ReferenceEquals(_graphicsDevice, graphicsDevice);
    }

    /// <summary>
    /// Binds the texture resource when one is available.
    /// </summary>
    /// <param name="commandList">The active command list.</param>
    /// <param name="slot">The binding slot to populate.</param>
    internal void Bind(ICommandList commandList, int slot)
    {
        ThrowIfDisposed();

        if (_gpuTexture is null)
        {
            return;
        }

        commandList.BindTexture(slot, _gpuTexture);
    }

    /// <inheritdoc/>
    public override bool RequiresPerFrameUpdate => NeedsPerFrameUpdate();

    /// <inheritdoc/>
    public override void Update(ICommandList commandList)
    {
        ThrowIfDisposed();
        ArgumentNullException.ThrowIfNull(commandList);

        ulong frameIndex = commandList.FrameIndex;
        if (_lastUpdateFrameIndex == frameIndex)
        {
            return;
        }

        if (_texture.Data is null)
        {
            _lastLoadAttemptPath = _texture.EffectiveFilePath;
        }

        Image? image = _texture.Data;
        if (image is null
            || image.Width <= 0
            || image.Height <= 0
            || image.Format == ImageFormat.Unknown
            || image.PixelMemory.IsEmpty)
        {
            ReleaseTexture();
            _failedImage = image;
            _lastUpdateFrameIndex = frameIndex;
            return;
        }

        TextureUsage usage = TextureUsage.Sampled;
        if (!_graphicsDevice.SupportsFormat(image.Format, usage))
        {
            ReleaseTexture();
            _failedImage = image;
            _lastUpdateFrameIndex = frameIndex;
            return;
        }

        if (_gpuTexture is not null
            && _width == image.Width
            && _height == image.Height
            && _arraySize == image.ArraySize
            && _mipLevels == image.MipLevels
            && _isCubemap == image.IsCubemap
            && _format == image.Format
            && ReferenceEquals(_image, image)
            && _payloadLength == image.PixelMemory.Length)
        {
            _lastUpdateFrameIndex = frameIndex;
            return;
        }

        ReleaseTexture();
        _failedImage = null;
        _gpuTexture = _graphicsDevice.CreateTexture(image, usage);
        _width = image.Width;
        _height = image.Height;
        _arraySize = image.ArraySize;
        _mipLevels = image.MipLevels;
        _isCubemap = image.IsCubemap;
        _format = image.Format;
        _image = image;
        _payloadLength = image.PixelMemory.Length;
        _lastUpdateFrameIndex = frameIndex;
    }

    /// <inheritdoc/>
    public override void Render(
        ICommandList commandList,
        RenderFlags phase,
        in Matrix4x4 view,
        in Matrix4x4 projection,
        in Matrix4x4 sceneAxis,
        Vector3 cameraPosition,
        Vector2 viewportSize)
    {
        ThrowIfDisposed();
    }

    /// <inheritdoc/>
    protected override void ReleaseCore()
    {
        ReleaseTexture();
        _failedImage = null;
        _lastLoadAttemptPath = null;
        _lastUpdateFrameIndex = ulong.MaxValue;
    }

    private bool NeedsPerFrameUpdate()
    {
        if (_texture.Data is not { } image)
        {
            if (_gpuTexture is not null)
            {
                return true;
            }

            return !string.Equals(_lastLoadAttemptPath, _texture.EffectiveFilePath, StringComparison.OrdinalIgnoreCase);
        }

        if (ReferenceEquals(_failedImage, image))
        {
            return false;
        }

        return _gpuTexture is null
            || _width != image.Width
            || _height != image.Height
            || _arraySize != image.ArraySize
            || _mipLevels != image.MipLevels
            || _isCubemap != image.IsCubemap
            || _format != image.Format
            || !ReferenceEquals(_image, image)
            || _payloadLength != image.PixelMemory.Length;
    }

    private void ReleaseTexture()
    {
        _gpuTexture?.Dispose();
        _gpuTexture = null;
        _width = 0;
        _height = 0;
        _arraySize = 0;
        _mipLevels = 0;
        _isCubemap = false;
        _format = ImageFormat.Unknown;
        _image = null;
        _payloadLength = 0;
    }
}