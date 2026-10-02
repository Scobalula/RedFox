using System;
using System.Numerics;
using RedFox.Imaging;
using RedFox.Imaging.Primitives;
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
    private int _uploadByteOffset;
    private bool _textureUploadPending;
    private int[] _uploadSliceIndices = [];
    private int _uploadSlicePosition;
    private int _availableBaseMip = -1;

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
    internal bool Bind(ICommandList commandList, int slot)
    {
        ThrowIfDisposed();

        if (_gpuTexture is null || (_textureUploadPending && _availableBaseMip < 0))
        {
            return false;
        }

        commandList.BindTexture(slot, _gpuTexture);
        return true;
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

        if (_textureUploadPending
            && ReferenceEquals(_image, image)
            && _payloadLength == image.PixelMemory.Length)
        {
            if (_graphicsDevice is IFrameTextureUploadBudget continuationBudget)
            {
                UploadPendingImage(image, continuationBudget);
            }

            _lastUpdateFrameIndex = frameIndex;
            return;
        }

        if (!_textureUploadPending
            && _gpuTexture is not null
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
        if (_graphicsDevice is IFrameTextureUploadBudget uploadBudget)
        {
            _gpuTexture = uploadBudget.CreateTextureStorage(image, usage);
            _textureUploadPending = true;
            _uploadSliceIndices = uploadBudget is IProgressiveTextureUploadBudget ? GetMipUploadOrder(image) : [.. Enumerable.Range(0, image.SliceCount)];
            _image = image;
            _width = image.Width;
            _height = image.Height;
            _arraySize = image.ArraySize;
            _mipLevels = image.MipLevels;
            _isCubemap = image.IsCubemap;
            _format = image.Format;
            _payloadLength = image.PixelMemory.Length;
            UploadPendingImage(image, uploadBudget);
        }
        else
        {
            _gpuTexture = _graphicsDevice.CreateTexture(image, usage);
        }

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
    public override void Render(ICommandList commandList, RenderFlags phase, in Matrix4x4 view, in Matrix4x4 projection, in Matrix4x4 sceneAxis, Vector3 cameraPosition, Vector2 viewportSize)
    {
        ThrowIfDisposed();
    }

    /// <inheritdoc/>
    protected override void ReleaseResources()
    {
        ReleaseTexture();
        _failedImage = null;
        _lastLoadAttemptPath = null;
        _lastUpdateFrameIndex = ulong.MaxValue;
    }

    private bool NeedsPerFrameUpdate()
    {
        if (_textureUploadPending)
        {
            return true;
        }

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
        _uploadByteOffset = 0;
        _textureUploadPending = false;
        _uploadSliceIndices = [];
        _uploadSlicePosition = 0;
        _availableBaseMip = -1;
        _width = 0;
        _height = 0;
        _arraySize = 0;
        _mipLevels = 0;
        _isCubemap = false;
        _format = ImageFormat.Unknown;
        _image = null;
        _payloadLength = 0;
    }

    private void UploadPendingImage(Image image, IFrameTextureUploadBudget uploadBudget)
    {
        while (_textureUploadPending && uploadBudget.RemainingBufferUploadBytes > 0)
        {
            if (_uploadSlicePosition >= _uploadSliceIndices.Length)
            {
                _textureUploadPending = false;
                return;
            }

            int sliceIndex = _uploadSliceIndices[_uploadSlicePosition];
            ref readonly ImageSlice slice = ref image.Slices[sliceIndex];
            int uploadedBytes = uploadBudget.UploadTextureRange(_gpuTexture!, image, sliceIndex, _uploadByteOffset);
            if (uploadedBytes <= 0)
            {
                return;
            }

            _uploadByteOffset += uploadedBytes;
            if (_uploadByteOffset >= slice.SlicePitch)
            {
                int completedMipLevel = slice.MipLevel;
                _uploadSlicePosition++;
                _uploadByteOffset = 0;
                if (uploadBudget is IProgressiveTextureUploadBudget progressiveBudget
                    && (_uploadSlicePosition >= _uploadSliceIndices.Length || image.Slices[_uploadSliceIndices[_uploadSlicePosition]].MipLevel != completedMipLevel))
                {
                    _availableBaseMip = completedMipLevel;
                    progressiveBudget.SetTextureMipRange(_gpuTexture!, _availableBaseMip, image.MipLevels - 1);
                }
            }
        }

        _textureUploadPending = _uploadSlicePosition < _uploadSliceIndices.Length;
    }

    private static int[] GetMipUploadOrder(Image image)
    {
        ImageSlice[] slices = image.Slices.ToArray();
        int[] sliceIndices = [.. Enumerable.Range(0, slices.Length)];
        Array.Sort(sliceIndices, (left, right) =>
        {
            int mipComparison = slices[right].MipLevel.CompareTo(slices[left].MipLevel);
            if (mipComparison != 0)
            {
                return mipComparison;
            }

            int arrayComparison = slices[left].ArrayIndex.CompareTo(slices[right].ArrayIndex);
            return arrayComparison != 0 ? arrayComparison : slices[left].DepthIndex.CompareTo(slices[right].DepthIndex);
        });
        return sliceIndices;
    }
}
