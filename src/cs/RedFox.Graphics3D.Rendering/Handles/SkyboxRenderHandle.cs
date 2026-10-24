using System;
using System.Numerics;
using RedFox.Imaging;
using RedFox.Imaging.Primitives;
using RedFox.Graphics3D.Rendering.Materials;

namespace RedFox.Graphics3D.Rendering.Handles;

/// <summary>
/// Owns skybox pipeline state and resolves the panorama texture used for the background and environment lighting.
/// </summary>
internal sealed class SkyboxRenderHandle : RenderHandle
{
    private const int SkyboxTextureSlot = 0;
    private const int SkyboxVertexCount = 3;

    internal const string SkyboxMaterialTypeName = "Skybox";

    private readonly IGraphicsDevice _graphicsDevice;
    private readonly IMaterialTypeRegistry _materialTypes;
    private readonly Scene _scene;
    private readonly Skybox _skybox;

    private IGpuPipelineState? _pipeline;
    private TextureRenderHandle? _textureHandle;
    private int _mipLevels;
    private Image? _lightsSource;
    private EnvironmentLight[] _lights = [];

    internal IGpuTexture? Texture => _textureHandle?.BindableTexture;

    internal int MipLevels => _mipLevels;

    internal EnvironmentLight[] Lights => Texture is null ? [] : _lights;

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

        _textureHandle = null;
        _mipLevels = 0;

        if (!_skybox.Enabled || _skybox.Texture is not { Data: { } image } texture || !IsUsablePanorama(image) || !_graphicsDevice.SupportsFormat(image.Format, TextureUsage.Sampled))
        {
            return;
        }

        EnsurePipeline();

        // The GPU copy is keyed by the texture rather than the scene, so switching previewed assets reuses the uploaded panorama.
        _textureHandle = SceneRenderResources.GetOrCreate(_graphicsDevice, texture, () => new TextureRenderHandle(_graphicsDevice, texture));
        _textureHandle.Update(commandList);
        _mipLevels = image.MipLevels;

        if (!ReferenceEquals(_lightsSource, image))
        {
            _lights = EnvironmentLightExtractor.Extract(image);
            _lightsSource = image;
        }
    }

    /// <inheritdoc/>
    public override void Render(ICommandList commandList, RenderFlags phase, in Matrix4x4 view, in Matrix4x4 projection, in Matrix4x4 sceneAxis, Vector3 cameraPosition, Vector2 viewportSize)
    {
        ThrowIfDisposed();
        ArgumentNullException.ThrowIfNull(commandList);

        if (phase != Rendering.RenderFlags.Opaque || !_skybox.Enabled || !_skybox.ShowBackground || _pipeline is null || Texture is not { } gpuTexture)
        {
            return;
        }

        if (!Matrix4x4.Invert(view, out Matrix4x4 inverseView) || !Matrix4x4.Invert(projection, out Matrix4x4 inverseProjection))
        {
            return;
        }

        commandList.SetPipelineState(_pipeline);
        commandList.BindTexture(SkyboxTextureSlot, gpuTexture);
        commandList.SetUniformMatrix4x4("InverseView", inverseView);
        commandList.SetUniformMatrix4x4("InverseProjection", inverseProjection);
        commandList.SetUniformVector4("SkyboxTint", _skybox.Tint);
        commandList.SetUniformFloat("SkyboxIntensity", MathF.Max(0.0f, _skybox.Intensity));
        commandList.SetUniformFloat("SkyboxLod", Math.Clamp(_skybox.BackgroundBlur, 0.0f, 1.0f) * Math.Max(_mipLevels - 1, 0));
        commandList.SetUniformInt("SkyboxTexture", SkyboxTextureSlot);
        commandList.Draw(SkyboxVertexCount, 0);
    }

    /// <inheritdoc/>
    protected override void ReleaseResources()
    {
        _textureHandle = null;
        _mipLevels = 0;
        _lightsSource = null;
        _lights = [];
        ReleasePipeline();
    }

    private void EnsurePipeline()
    {
        if (_pipeline is not null && !_pipeline.IsDisposed)
        {
            return;
        }

        if (_materialTypes is not IMaterialPipelineProvider pipelineProvider)
        {
            throw new InvalidOperationException($"Material registry '{_materialTypes.GetType().Name}' does not provide runtime pipeline services.");
        }

        _pipeline = pipelineProvider.AcquirePipeline(_graphicsDevice, SkyboxMaterialTypeName);
    }

    private void ReleasePipeline()
    {
        IGpuPipelineState? pipeline = _pipeline;
        _pipeline = null;

        if (pipeline is null)
        {
            return;
        }

        if (_materialTypes is IMaterialPipelineProvider pipelineProvider)
        {
            pipelineProvider.ReleasePipeline(SkyboxMaterialTypeName, pipeline);
            return;
        }

        pipeline.Dispose();
    }

    // Skyboxes are equirectangular panoramas, which are also sampled directly for environment lighting.
    private static bool IsUsablePanorama(Image image)
    {
        return !image.IsCubemap
            && image.Height > 0
            && image.Width == image.Height * 2
            && image.Depth == 1
            && image.ArraySize == 1
            && image.Format != ImageFormat.Unknown
            && !image.PixelMemory.IsEmpty;
    }
}
