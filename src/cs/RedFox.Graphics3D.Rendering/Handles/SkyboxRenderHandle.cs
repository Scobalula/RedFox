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
    private const int RotatedEnvironmentMaxWidth = 2048;

    internal const string SkyboxMaterialTypeName = "Skybox";
    internal const string SkyboxRotationMaterialTypeName = "SkyboxRotation";

    private readonly IGraphicsDevice _graphicsDevice;
    private readonly IMaterialTypeRegistry _materialTypes;
    private readonly Scene _scene;
    private readonly Skybox _skybox;

    private IGpuPipelineState? _pipeline;
    private IGpuPipelineState? _rotationPipeline;
    private TextureRenderHandle? _textureHandle;
    private int _mipLevels;
    private Image? _lightsSource;
    private EnvironmentLight[] _extractedLights = [];
    private EnvironmentLight[] _lights = [];
    private float _lightsRotation;
    private IGpuTexture? _rotatedTexture;
    private IGpuRenderTarget[] _rotatedTargets = [];
    private float _rotatedAngle;
    private bool _rotatedComplete;

    internal IGpuTexture? Texture => _textureHandle?.BindableTexture;

    internal IGpuTexture? EnvironmentTexture => _skybox.Rotation != 0.0f && Texture is not null && _rotatedTexture is not null ? _rotatedTexture : Texture;

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

        _pipeline = EnsurePipeline(_pipeline, SkyboxMaterialTypeName);

        // The GPU copy is keyed by the texture rather than the scene, so switching previewed assets reuses the uploaded panorama.
        _textureHandle = SceneRenderResources.GetOrCreate(_graphicsDevice, texture, () => new TextureRenderHandle(_graphicsDevice, texture));
        _textureHandle.Update(commandList);
        _mipLevels = image.MipLevels;

        if (!ReferenceEquals(_lightsSource, image))
        {
            _extractedLights = EnvironmentLightExtractor.Extract(image);
            _lightsSource = image;
            _lightsRotation = float.NaN;
            ReleaseRotatedTexture();
        }

        if (_lightsRotation != _skybox.Rotation)
        {
            Matrix4x4 rotation = Matrix4x4.CreateRotationY(_skybox.Rotation);
            _lights = [.. _extractedLights.Select(light => light with { Direction = Vector3.TransformNormal(light.Direction, rotation), MajorAxis = Vector3.TransformNormal(light.MajorAxis, rotation) })];
            _lightsRotation = _skybox.Rotation;
        }

        UpdateRotatedTexture(commandList, image);
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
        commandList.SetUniformMatrix4x4("InverseView", inverseView * Matrix4x4.CreateRotationY(-_skybox.Rotation));
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
        _extractedLights = [];
        _lights = [];
        ReleaseRotatedTexture();
        ReleasePipeline(_pipeline, SkyboxMaterialTypeName);
        ReleasePipeline(_rotationPipeline, SkyboxRotationMaterialTypeName);
        _pipeline = null;
        _rotationPipeline = null;
    }

    // Materials sample the environment with world directions, so a rotated sky is baked into a copy that is shifted around the panorama.
    private void UpdateRotatedTexture(ICommandList commandList, Image image)
    {
        if (_skybox.Rotation == 0.0f || Texture is not { } source || !_graphicsDevice.SupportsFormat(image.Format, TextureUsage.Sampled | TextureUsage.RenderTarget))
        {
            return;
        }

        int width	= Math.Min(image.Width, RotatedEnvironmentMaxWidth);
        int height	= width / 2;

        if (_rotatedTexture is null)
        {
            _rotatedTexture		= _graphicsDevice.CreateTexture(width, height, _mipLevels, image.Format, TextureUsage.Sampled | TextureUsage.RenderTarget);
            _rotatedTargets		= [.. Enumerable.Range(0, _mipLevels).Select(level => _graphicsDevice.CreateRenderTarget(_rotatedTexture, level))];
            _rotatedComplete	= false;
        }

        if (_rotatedComplete && _rotatedAngle == _skybox.Rotation)
        {
            return;
        }

        _rotationPipeline = EnsurePipeline(_rotationPipeline, SkyboxRotationMaterialTypeName);

        commandList.SetPipelineState(_rotationPipeline);
        commandList.BindTexture(SkyboxTextureSlot, source);
        commandList.SetUniformInt("SourceTexture", SkyboxTextureSlot);
        commandList.SetUniformFloat("RotationOffset", _skybox.Rotation / MathF.Tau);

        for (int level = 0; level < _rotatedTargets.Length; level++)
        {
            Vector2 size = new(Math.Max(1, width >> level), Math.Max(1, height >> level));

            commandList.SetRenderTarget(_rotatedTargets[level]);
            commandList.SetViewport((int)size.X, (int)size.Y);
            commandList.SetUniformVector2("TargetSize", size);
            commandList.SetUniformFloat("SourceLod", level);
            commandList.Draw(SkyboxVertexCount, 0);
        }

        commandList.SetRenderTarget(null);

        _rotatedAngle		= _skybox.Rotation;
        _rotatedComplete	= _textureHandle?.HasPendingUploads == false;
    }

    private void ReleaseRotatedTexture()
    {
        foreach (IGpuRenderTarget target in _rotatedTargets)
        {
            target.Dispose();
        }

        _rotatedTargets = [];
        _rotatedTexture?.Dispose();
        _rotatedTexture = null;
    }

    private IGpuPipelineState EnsurePipeline(IGpuPipelineState? pipeline, string materialTypeName)
    {
        if (pipeline is not null && !pipeline.IsDisposed)
        {
            return pipeline;
        }

        if (_materialTypes is not IMaterialPipelineProvider pipelineProvider)
        {
            throw new InvalidOperationException($"Material registry '{_materialTypes.GetType().Name}' does not provide runtime pipeline services.");
        }

        return pipelineProvider.AcquirePipeline(_graphicsDevice, materialTypeName);
    }

    private void ReleasePipeline(IGpuPipelineState? pipeline, string materialTypeName)
    {
        if (pipeline is null)
        {
            return;
        }

        if (_materialTypes is IMaterialPipelineProvider pipelineProvider)
        {
            pipelineProvider.ReleasePipeline(materialTypeName, pipeline);
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
