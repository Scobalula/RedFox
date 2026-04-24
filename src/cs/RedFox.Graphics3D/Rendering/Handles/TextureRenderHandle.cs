using System;
using System.Numerics;
using RedFox.Graphics2D;
using RedFox.Graphics3D.Rendering.Backend;

namespace RedFox.Graphics3D.Rendering.Handles;

/// <summary>
/// Owns a backend texture resource for a texture node.
/// </summary>
internal sealed class TextureRenderHandle : RenderHandle
{
    private readonly IGraphicsDevice _graphicsDevice;
    private readonly Texture _texture;

    private IGpuTexture? _gpuTexture;
    private ImageFormat _format;
    private int _height;
    private int _payloadLength;
    private int _width;

    /// <summary>
    /// Initializes a new instance of the <see cref="TextureRenderHandle"/> class.
    /// </summary>
    /// <param name="graphicsDevice">The graphics device that creates texture resources.</param>
    /// <param name="texture">The texture node represented by this handle.</param>
    public TextureRenderHandle(IGraphicsDevice graphicsDevice, Texture texture)
    {
        _graphicsDevice = graphicsDevice ?? throw new ArgumentNullException(nameof(graphicsDevice));
        _texture = texture ?? throw new ArgumentNullException(nameof(texture));
    }

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
    public override void Update(ICommandList commandList)
    {
        ThrowIfDisposed();
        ArgumentNullException.ThrowIfNull(commandList);

        if (_texture.Data is not { } image
            || image.Width <= 0
            || image.Height <= 0
            || image.Format == ImageFormat.Unknown
            || image.PixelMemory.IsEmpty)
        {
            ReleaseTexture();
            return;
        }

        TextureUsage usage = TextureUsage.Sampled;
        if (!_graphicsDevice.SupportsFormat(image.Format, usage))
        {
            ReleaseTexture();
            return;
        }

        if (_gpuTexture is not null
            && _width == image.Width
            && _height == image.Height
            && _format == image.Format
            && _payloadLength == image.PixelMemory.Length)
        {
            return;
        }

        ReleaseTexture();
        _gpuTexture = _graphicsDevice.CreateTexture(image.Width, image.Height, image.Format, usage, image.PixelMemory.Span);
        _width = image.Width;
        _height = image.Height;
        _format = image.Format;
        _payloadLength = image.PixelMemory.Length;
    }

    /// <inheritdoc/>
    public override void Render(
        ICommandList commandList,
        RenderPhase phase,
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
    }

    private void ReleaseTexture()
    {
        _gpuTexture?.Dispose();
        _gpuTexture = null;
        _width = 0;
        _height = 0;
        _format = ImageFormat.Unknown;
        _payloadLength = 0;
    }
}