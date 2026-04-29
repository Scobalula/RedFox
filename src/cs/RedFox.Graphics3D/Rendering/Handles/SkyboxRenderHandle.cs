using System;
using System.Numerics;
using RedFox.Graphics2D;
using RedFox.Graphics3D.Rendering.Materials;

namespace RedFox.Graphics3D.Rendering.Handles;

/// <summary>
/// Owns skybox pipeline and cube-map texture state for scene rendering.
/// </summary>
internal sealed class SkyboxRenderHandle : RenderHandle
{
    private const int SkyboxTextureSlot = 0;
    private const int SkyboxVertexCount = 3;

    private readonly IGraphicsDevice _graphicsDevice;
    private readonly IMaterialTypeRegistry _materialTypes;
    private readonly Scene _scene;
    private readonly Skybox _skybox;

    private int _arraySize;
    private ImageFormat _format;
    private IGpuTexture? _gpuTexture;
    private int _height;
    private Image? _image;
    private int _mipLevels;
    private int _payloadLength;
    private IGpuPipelineState? _pipeline;
    private Texture? _texture;
    private int _width;

    /// <summary>
    /// Initializes a new instance of the <see cref="SkyboxRenderHandle"/> class.
    /// </summary>
    /// <param name="graphicsDevice">The graphics device that owns skybox resources.</param>
    /// <param name="materialTypes">The material registry used to resolve the skybox pipeline.</param>
    /// <param name="skybox">The skybox settings represented by this handle.</param>
    /// <param name="scene">The scene that owns image translators used by skybox textures.</param>
    public SkyboxRenderHandle(IGraphicsDevice graphicsDevice, IMaterialTypeRegistry materialTypes, Skybox skybox, Scene scene)
    {
        _graphicsDevice = graphicsDevice ?? throw new ArgumentNullException(nameof(graphicsDevice));
        _materialTypes = materialTypes ?? throw new ArgumentNullException(nameof(materialTypes));
        _skybox = skybox ?? throw new ArgumentNullException(nameof(skybox));
        _scene = scene ?? throw new ArgumentNullException(nameof(scene));
    }

    /// <inheritdoc/>
    public override void Update(ICommandList commandList)
    {
        ThrowIfDisposed();
        ArgumentNullException.ThrowIfNull(commandList);

        if (!_skybox.Enabled || _skybox.Texture is null)
        {
            ReleaseTexture();
            return;
        }

        Texture texture = _skybox.Texture;
        if (texture.Data is null)
        {
            texture.TryLoad(_scene.ImageTranslators);
        }

        if (texture.Data is not { } image || !IsUsableCubemap(image))
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

        EnsurePipeline();
        if (_gpuTexture is not null
            && ReferenceEquals(_texture, texture)
            && ReferenceEquals(_image, image)
            && _width == image.Width
            && _height == image.Height
            && _arraySize == image.ArraySize
            && _mipLevels == image.MipLevels
            && _format == image.Format
            && _payloadLength == image.PixelMemory.Length)
        {
            return;
        }

        ReleaseTexture();
        _gpuTexture = _graphicsDevice.CreateTexture(image, usage);
        _texture = texture;
        _image = image;
        _width = image.Width;
        _height = image.Height;
        _arraySize = image.ArraySize;
        _mipLevels = image.MipLevels;
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
        ArgumentNullException.ThrowIfNull(commandList);

        if (phase != RenderPhase.Opaque || !_skybox.Enabled || _pipeline is null || _gpuTexture is null)
        {
            return;
        }

        if (!Matrix4x4.Invert(view, out Matrix4x4 inverseView) || !Matrix4x4.Invert(projection, out Matrix4x4 inverseProjection))
        {
            return;
        }

        commandList.SetPipelineState(_pipeline);
        commandList.BindTexture(SkyboxTextureSlot, _gpuTexture);
        commandList.SetUniformMatrix4x4("InverseView", inverseView);
        commandList.SetUniformMatrix4x4("InverseProjection", inverseProjection);
        commandList.SetUniformVector4("SkyboxTint", _skybox.Tint);
        commandList.SetUniformFloat("SkyboxIntensity", MathF.Max(0.0f, _skybox.Intensity));
        commandList.SetUniformInt("SkyboxTexture", SkyboxTextureSlot);
        commandList.Draw(SkyboxVertexCount, 0);
    }

    /// <inheritdoc/>
    protected override void ReleaseCore()
    {
        ReleaseTexture();
        _pipeline?.Dispose();
        _pipeline = null;
    }

    private void EnsurePipeline()
    {
        if (_pipeline is not null)
        {
            return;
        }

        MaterialTypeDefinition definition = _materialTypes.Get("Skybox");
        _pipeline = definition.BuildPipeline(_graphicsDevice);
    }

    private void ReleaseTexture()
    {
        _gpuTexture?.Dispose();
        _gpuTexture = null;
        _texture = null;
        _image = null;
        _width = 0;
        _height = 0;
        _arraySize = 0;
        _mipLevels = 0;
        _format = ImageFormat.Unknown;
        _payloadLength = 0;
    }

    private static bool IsUsableCubemap(Image image)
    {
        return image.IsCubemap
            && image.Width > 0
            && image.Height > 0
            && image.Width == image.Height
            && image.Depth == 1
            && image.ArraySize >= 6
            && image.ArraySize % 6 == 0
            && image.Format != ImageFormat.Unknown
            && !image.PixelMemory.IsEmpty;
    }
}