using System;
using System.Numerics;
using RedFox.Graphics3D.Rendering.Materials;

namespace RedFox.Graphics3D.Rendering.Handles;

/// <summary>
/// Owns backend pipeline state and texture binding state for a material node.
/// </summary>
/// <remarks>
/// Initializes a new instance of the <see cref="MaterialRenderHandle"/> class.
/// </remarks>
/// <param name="graphicsDevice">The graphics device that creates material resources.</param>
/// <param name="material">The material node represented by this handle.</param>
internal sealed class MaterialRenderHandle(IGraphicsDevice graphicsDevice, Material material) : RenderHandle
{
    private const string DefaultMaterialTypeName = "Default";

    private readonly IGraphicsDevice _graphicsDevice = graphicsDevice ?? throw new ArgumentNullException(nameof(graphicsDevice));
    private readonly Material _material = material ?? throw new ArgumentNullException(nameof(material));

    private IGpuPipelineState? _pipeline;
    private string? _resolvedTypeName;
    private MaterialTextureBinding[]? _textureBindingSnapshot;
    private uint _bindingVersion = uint.MaxValue;
    private ulong _lastUpdateFrameIndex = ulong.MaxValue;


    /// <inheritdoc/>
    public override RenderHandleFlags Flags => RenderHandleFlags.SubHandle;

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
    /// Binds the resolved pipeline and any available textures for the material.
    /// </summary>
    /// <param name="commandList">The active command list.</param>
    internal void BindResources(ICommandList commandList)
    {
        ThrowIfDisposed();
        ArgumentNullException.ThrowIfNull(commandList);

        if (_pipeline is not null)
        {
            commandList.SetPipelineState(_pipeline);
        }

        SetMaterialUniforms(commandList);

        BindTextureResources(commandList);
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

        string requestedTypeName = GetRequestedTypeName();
        IMaterialTypeRegistry materialTypes = _graphicsDevice.MaterialTypes;

        if (!materialTypes.Contains(requestedTypeName))
        {
            string registeredNames = string.Join(", ", materialTypes.RegisteredNames);
            throw new InvalidOperationException($"Material '{_material.Name}' requested unknown material type '{requestedTypeName}'. Registered names: {registeredNames}.");
        }

        if (_resolvedTypeName != requestedTypeName || _pipeline is null || _pipeline.IsDisposed)
        {
            EnsurePipeline(requestedTypeName);
        }

        _lastUpdateFrameIndex = frameIndex;
    }

    /// <inheritdoc/>
    public override void Render(ICommandList commandList, RenderPhase phase, in Matrix4x4 view, in Matrix4x4 projection, in Matrix4x4 sceneAxis, Vector3 cameraPosition, Vector2 viewportSize)
    {
        ThrowIfDisposed();
        ArgumentNullException.ThrowIfNull(commandList);

        if (_pipeline is not null)
        {
            commandList.SetPipelineState(_pipeline);
        }

        SetMaterialUniforms(commandList);

        BindTextureResources(commandList);
    }

    /// <inheritdoc/>
    protected override void ReleaseCore()
    {
        ReleasePipelineReference();
        _textureBindingSnapshot = null;
        _bindingVersion = uint.MaxValue;
        _lastUpdateFrameIndex = ulong.MaxValue;
    }

    private void EnsurePipeline(string requestedTypeName)
    {
        if (_resolvedTypeName == requestedTypeName && _pipeline is not null && !_pipeline.IsDisposed)
        {
            return;
        }

        ReleasePipelineReference();

        IMaterialPipelineProvider pipelineProvider = GetPipelineProvider();
        _pipeline = pipelineProvider.AcquirePipeline(_graphicsDevice, requestedTypeName);
        _resolvedTypeName = requestedTypeName;
    }

    private void ReleasePipelineReference()
    {
        string? resolvedTypeName = _resolvedTypeName;
        IGpuPipelineState? pipeline = _pipeline;

        _pipeline = null;
        _resolvedTypeName = null;

        if (resolvedTypeName is null || pipeline is null)
        {
            return;
        }

        IMaterialPipelineProvider pipelineProvider = GetPipelineProvider();
        pipelineProvider.ReleasePipeline(resolvedTypeName, pipeline);
    }

    private MaterialTextureBinding[] GetTextureBindingSnapshot()
    {
        if (_textureBindingSnapshot is null || _material.Version != _bindingVersion)
        {
            _textureBindingSnapshot = [.. _material.Textures];
            _bindingVersion = _material.Version;
        }

        return _textureBindingSnapshot;
    }

    private void SetMaterialUniforms(ICommandList commandList)
    {
        commandList.SetUniformVector4("BaseColor", _material.DiffuseColor ?? Vector4.One);
        commandList.SetUniformFloat("MaterialSpecularStrength", _material.SpecularStrength ?? 0.28f);
        commandList.SetUniformFloat("MaterialSpecularPower", _material.SpecularPower ?? 32.0f);
    }

    private void BindTextureResources(ICommandList commandList)
    {
        MaterialTextureBinding[] snapshot = GetTextureBindingSnapshot();
        for (int i = 0; i < snapshot.Length; i++)
        {
            MaterialTextureBinding binding = snapshot[i];
            if (binding.Texture.GraphicsHandle is not TextureRenderHandle textureHandle || !textureHandle.IsOwnedBy(_graphicsDevice))
            {
                throw new InvalidOperationException($"Texture '{binding.Texture.Name}' was not prepared for rendering.");
            }

            textureHandle.Bind(commandList, binding.Slot);
        }
    }

    private bool NeedsPerFrameUpdate()
    {
        if (_pipeline is null || _pipeline.IsDisposed || _resolvedTypeName != GetRequestedTypeName())
        {
            return true;
        }

        if (_textureBindingSnapshot is null || _bindingVersion != _material.Version)
        {
            return true;
        }

        return false;
    }

    private string GetRequestedTypeName()
    {
        return string.IsNullOrWhiteSpace(_material.Type)
            ? DefaultMaterialTypeName
            : _material.Type.Trim();
    }

    private IMaterialPipelineProvider GetPipelineProvider()
    {
        if (_graphicsDevice.MaterialTypes is IMaterialPipelineProvider pipelineProvider)
        {
            return pipelineProvider;
        }

        throw new InvalidOperationException($"Material registry '{_graphicsDevice.MaterialTypes.GetType().Name}' does not provide runtime pipeline services.");
    }
}