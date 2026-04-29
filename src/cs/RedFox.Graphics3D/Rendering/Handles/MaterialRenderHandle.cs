using System;
using System.Numerics;
using RedFox.Graphics3D.Rendering;
using RedFox.Graphics3D.Rendering.Materials;

namespace RedFox.Graphics3D.Rendering.Handles;

/// <summary>
/// Owns backend pipeline state and texture binding state for a material node.
/// </summary>
internal sealed class MaterialRenderHandle : RenderHandle
{
    private const string DefaultMaterialTypeName = "Default";

    private readonly IGraphicsDevice _graphicsDevice;
    private readonly Material _material;
    private readonly IMaterialTypeRegistry _materialTypes;

    private IGpuPipelineState? _pipeline;
    private string? _resolvedTypeName;
    private MaterialTextureBinding[]? _textureBindingSnapshot;
    private uint _bindingVersion = uint.MaxValue;
    private ulong _lastUpdateFrameIndex = ulong.MaxValue;

    /// <summary>
    /// Initializes a new instance of the <see cref="MaterialRenderHandle"/> class.
    /// </summary>
    /// <param name="graphicsDevice">The graphics device that creates material resources.</param>
    /// <param name="materialTypes">The material-type registry used to resolve pipelines.</param>
    /// <param name="material">The material node represented by this handle.</param>
    public MaterialRenderHandle(IGraphicsDevice graphicsDevice, IMaterialTypeRegistry materialTypes, Material material)
    {
        _graphicsDevice = graphicsDevice ?? throw new ArgumentNullException(nameof(graphicsDevice));
        _materialTypes = materialTypes ?? throw new ArgumentNullException(nameof(materialTypes));
        _material = material ?? throw new ArgumentNullException(nameof(material));
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

        if (!_materialTypes.Contains(requestedTypeName))
        {
            string registeredNames = string.Join(", ", _materialTypes.RegisteredNames);
            throw new InvalidOperationException($"Material '{_material.Name}' requested unknown material type '{requestedTypeName}'. Registered names: {registeredNames}.");
        }

        if (_resolvedTypeName != requestedTypeName || _pipeline is null)
        {
            _pipeline?.Dispose();
            _pipeline = null;

            MaterialTypeDefinition definition = _materialTypes.Get(requestedTypeName);
            _pipeline = definition.BuildPipeline(_graphicsDevice);
            _resolvedTypeName = requestedTypeName;
        }

        UpdateTextureHandles(commandList);
        _lastUpdateFrameIndex = frameIndex;
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
        _pipeline?.Dispose();
        _pipeline = null;
        _resolvedTypeName = null;
        _textureBindingSnapshot = null;
        _bindingVersion = uint.MaxValue;
        _lastUpdateFrameIndex = ulong.MaxValue;
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

    private TextureRenderHandle EnsureTextureHandle(Texture texture)
    {
        if (texture.GraphicsHandle is TextureRenderHandle existingHandle && existingHandle.IsOwnedBy(_graphicsDevice))
        {
            return existingHandle;
        }

        if (texture.GraphicsHandle is not null)
        {
            texture.GraphicsHandle.Release();
            texture.GraphicsHandle.Dispose();
        }

        IRenderHandle? renderHandle = texture.CreateRenderHandle(_graphicsDevice, _materialTypes);
        TextureRenderHandle textureHandle = renderHandle as TextureRenderHandle
            ?? throw new InvalidOperationException($"Texture '{texture.Name}' did not create a {nameof(TextureRenderHandle)}.");

        texture.GraphicsHandle = textureHandle;
        return textureHandle;
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
            if (binding.Texture.GraphicsHandle is TextureRenderHandle textureHandle && textureHandle.IsOwnedBy(_graphicsDevice))
            {
                textureHandle.Bind(commandList, binding.Slot);
            }
        }
    }

    private void UpdateTextureHandles(ICommandList commandList)
    {
        MaterialTextureBinding[] snapshot = GetTextureBindingSnapshot();
        for (int i = 0; i < snapshot.Length; i++)
        {
            TextureRenderHandle textureHandle = EnsureTextureHandle(snapshot[i].Texture);
            textureHandle.Update(commandList);
        }
    }

    private bool NeedsPerFrameUpdate()
    {
        if (_pipeline is null || _resolvedTypeName != GetRequestedTypeName())
        {
            return true;
        }

        if (_textureBindingSnapshot is null || _bindingVersion != _material.Version)
        {
            return true;
        }

        for (int i = 0; i < _textureBindingSnapshot.Length; i++)
        {
            Texture texture = _textureBindingSnapshot[i].Texture;
            if (texture.GraphicsHandle is not TextureRenderHandle textureHandle
                || !textureHandle.IsOwnedBy(_graphicsDevice)
                || textureHandle.RequiresPerFrameUpdate)
            {
                return true;
            }
        }

        return false;
    }

    private string GetRequestedTypeName()
    {
        return string.IsNullOrWhiteSpace(_material.Type)
            ? DefaultMaterialTypeName
            : _material.Type.Trim();
    }
}