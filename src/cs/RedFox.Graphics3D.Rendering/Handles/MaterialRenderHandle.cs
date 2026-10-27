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
    internal const string DefaultMaterialTypeName = "Default";
    private const string DiffuseTextureUniformName = "DiffuseMap";
    private const string HasDiffuseMapUniformName = "HasDiffuseMap";
    private const int DiffuseTextureSlot = 15;

    private readonly IGraphicsDevice _graphicsDevice = graphicsDevice ?? throw new ArgumentNullException(nameof(graphicsDevice));
    private readonly Material _material = material ?? throw new ArgumentNullException(nameof(material));

    private IGpuPipelineState? _pipeline;
    private string? _resolvedTypeName;
    private IReadOnlyList<MaterialTextureRequirement> _textureRequirements = [];
    private string[] _textureFlagUniformNames = [];
    private IReadOnlyList<MaterialUniformRequirement> _uniformRequirements = [];
    private MaterialTextureSlot[] _textureSlots = [];
    private uint _bindingVersion = uint.MaxValue;
    private ulong _lastUpdateFrameIndex = ulong.MaxValue;


    /// <summary>
    /// Gets the currently prepared pipeline, if one has been resolved.
    /// </summary>
    internal IGpuPipelineState? Pipeline => _pipeline;

    internal bool IsOpaque { get; private set; }

    internal Material Material => _material;


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

        UpdateTextureSlots(commandList);
        _lastUpdateFrameIndex = frameIndex;
    }

    /// <inheritdoc/>
    public override void Render(ICommandList commandList, RenderFlags phase, in Matrix4x4 view, in Matrix4x4 projection, in Matrix4x4 sceneAxis, Vector3 cameraPosition, Vector2 viewportSize)
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
    protected override void ReleaseResources()
    {
        ReleasePipelineReference();
        _textureSlots = [];
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
        MaterialTypeDescriptor descriptor = _graphicsDevice.MaterialTypes.Get(requestedTypeName).Descriptor;
        IsOpaque = descriptor.PipelineState is { Blend: false, DepthWrite: true };
        _textureRequirements = descriptor.Requirements.Textures;
        _textureFlagUniformNames = [.. _textureRequirements.Select(requirement => $"Has{requirement.Name}")];
        _uniformRequirements = descriptor.Requirements.Uniforms;
        _bindingVersion = uint.MaxValue;
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

    private void UpdateTextureSlots(ICommandList commandList)
    {
        if (_bindingVersion != _material.Version || HasReleasedTextureSlot())
        {
            _textureSlots = CreateTextureSlots();
            _bindingVersion = _material.Version;
        }

        foreach (MaterialTextureSlot textureSlot in _textureSlots)
        {
            textureSlot.Handle?.Update(commandList);
        }
    }

    private MaterialTextureSlot[] CreateTextureSlots()
    {
        List<MaterialTextureSlot> textureSlots = [];

        if (_textureRequirements.Count == 0)
        {
            foreach (MaterialTextureBinding binding in _material.Textures)
            {
                textureSlots.Add(new MaterialTextureSlot(GetTextureHandle(binding.Texture), binding.SamplerUniform, binding.Slot, null));
            }
        }

        for (int i = 0; i < _textureRequirements.Count; i++)
        {
            MaterialTextureRequirement requirement = _textureRequirements[i];
            TextureRenderHandle? textureHandle = _material.TryGetTexture(requirement.Name, out Texture? texture) ? GetTextureHandle(texture) : null;
            textureSlots.Add(new MaterialTextureSlot(textureHandle, requirement.Name, requirement.Slot, _textureFlagUniformNames[i]));
        }

        TextureRenderHandle? diffuseHandle = _material.DiffuseMap is { } diffuseTexture ? GetTextureHandle(diffuseTexture) : null;
        textureSlots.Add(new MaterialTextureSlot(diffuseHandle, DiffuseTextureUniformName, DiffuseTextureSlot, HasDiffuseMapUniformName));
        return [.. textureSlots];
    }

    private bool HasReleasedTextureSlot()
    {
        foreach (MaterialTextureSlot textureSlot in _textureSlots)
        {
            if (textureSlot.Handle is { IsReleased: true })
            {
                return true;
            }
        }

        return false;
    }

    private TextureRenderHandle GetTextureHandle(Texture texture)
    {
        return SceneRenderResources.GetOrCreate(_graphicsDevice, texture, () => new TextureRenderHandle(_graphicsDevice, texture));
    }

    private void SetMaterialUniforms(ICommandList commandList)
    {
        commandList.SetUniformVector4("BaseColor", _material.DiffuseColor ?? Vector4.One);
        commandList.SetUniformFloat("MaterialSpecularStrength", _material.SpecularStrength ?? 0.28f);
        commandList.SetUniformFloat("MaterialSpecularPower", _material.SpecularPower ?? 32.0f);
        commandList.SetUniformFloat("MaterialMetallic", _material.Metallic ?? 0.0f);
        commandList.SetUniformFloat("MaterialRoughness", _material.Roughness ?? 1.0f);
        commandList.SetUniformFloat("MaterialAlphaCutoff", _material.AlphaCutoff ?? 0.0f);

        for (int i = 0; i < _uniformRequirements.Count; i++)
        {
            string name = _uniformRequirements[i].Name;

            if (_material.TryGetAttribute(name, out Vector4 vector))
            {
                commandList.SetUniformVector4(name, vector);
            }
            else if (_material.TryGetAttribute(name, out float value))
            {
                commandList.SetUniformFloat(name, value);
            }
        }
    }

    private void BindTextureResources(ICommandList commandList)
    {
        foreach (MaterialTextureSlot textureSlot in _textureSlots)
        {
            bool bound = textureSlot.Handle?.Bind(commandList, textureSlot.Slot) == true;
            if (bound)
            {
                commandList.SetUniformInt(textureSlot.SamplerUniform, textureSlot.Slot);
            }

            if (textureSlot.FlagUniform is not null)
            {
                commandList.SetUniformInt(textureSlot.FlagUniform, bound ? 1 : 0);
            }
        }
    }

    private bool NeedsPerFrameUpdate()
    {
        if (_pipeline is null || _pipeline.IsDisposed || _resolvedTypeName != GetRequestedTypeName())
        {
            return true;
        }

        if (_bindingVersion != _material.Version)
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
