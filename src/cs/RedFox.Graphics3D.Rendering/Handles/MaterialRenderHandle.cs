using System;
using System.Numerics;
using RedFox.Graphics3D.Rendering.Materials;
using RedFox.Imaging.Primitives;

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

    private static readonly MaterialTextureRequirement DiffuseTextureRequirement = new("DiffuseMap", 15, false, "diffuse");

    private readonly IGraphicsDevice _graphicsDevice = graphicsDevice ?? throw new ArgumentNullException(nameof(graphicsDevice));
    private readonly Material _material = material ?? throw new ArgumentNullException(nameof(material));

    private IGpuPipelineState? _pipeline;
    private string? _resolvedTypeName;
    private MaterialTextureRequirement[] _textureRequirements = [];
    private bool _bindsMaterialTextures;
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
        IReadOnlyList<MaterialTextureRequirement> textureRequirements = descriptor.Requirements.Textures;
        _bindsMaterialTextures = textureRequirements.Count == 0;
        _textureRequirements = textureRequirements.Any(requirement => requirement.Name == DiffuseTextureRequirement.Name) ? [.. textureRequirements] : [.. textureRequirements, DiffuseTextureRequirement];
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

        if (_bindsMaterialTextures)
        {
            foreach (MaterialTextureBinding binding in _material.Textures)
            {
                textureSlots.Add(CreateTextureSlot(GetTextureHandle(binding.Texture), binding.SamplerUniform, binding.Slot, MaterialChannelTransform.Identity));
            }
        }

        foreach (MaterialTextureRequirement requirement in _textureRequirements)
        {
            _material.TryGetChannelSource(requirement.Name, out MaterialChannelSource? source);
            TextureRenderHandle?		textureHandle	= FindTexture(requirement, source) is { } texture ? GetTextureHandle(texture) : null;
            MaterialChannelTransform	channels		= source is null ? MaterialChannelTransform.Identity : MaterialChannelTransform.From(source);
            textureSlots.Add(CreateTextureSlot(textureHandle, requirement.Name, requirement.Slot, channels));
        }

        return [.. textureSlots];
    }

    private Texture? FindTexture(MaterialTextureRequirement requirement, MaterialChannelSource? source)
    {
        if (source is not null)
        {
            return _material.TryGetTexture(source.TextureName, out Texture? sourceTexture) ? sourceTexture : null;
        }

        if (requirement.Semantic is not null)
        {
            return _material.GetMapName(requirement.Semantic) is { } mapName && _material.TryGetTexture(mapName, out Texture? semanticTexture) ? semanticTexture : null;
        }

        return _material.TryGetTexture(requirement.Name, out Texture? texture) ? texture : null;
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
        commandList.SetUniformVector4("EmissiveColor", _material.EmissiveColor ?? Vector4.One);
        commandList.SetUniformFloat("MaterialSpecularStrength", _material.SpecularStrength ?? 0.28f);
        commandList.SetUniformFloat("MaterialSpecularPower", _material.SpecularPower ?? 32.0f);
        commandList.SetUniformFloat("MaterialMetallic", _material.Metallic ?? 0.0f);
        commandList.SetUniformFloat("MaterialRoughness", _material.Roughness ?? 1.0f);
        commandList.SetUniformFloat("MaterialAlphaCutoff", _material.AlphaCutoff ?? 0.0f);

        for (int i = 0; i < _uniformRequirements.Count; i++)
        {
            MaterialUniformRequirement requirement = _uniformRequirements[i];
            if (GetUniformValue(requirement) is not { } value)
            {
                continue;
            }

            switch (requirement.ValueType)
            {
                case MaterialValueType.Float:
                    commandList.SetUniformFloat(requirement.Name, value.X);
                    break;
                case MaterialValueType.Float2:
                    commandList.SetUniformVector2(requirement.Name, new Vector2(value.X, value.Y));
                    break;
                case MaterialValueType.Float3:
                    commandList.SetUniformVector3(requirement.Name, new Vector3(value.X, value.Y, value.Z));
                    break;
                case MaterialValueType.Float4:
                    commandList.SetUniformVector4(requirement.Name, value);
                    break;
                case MaterialValueType.Int32 or MaterialValueType.UInt32 or MaterialValueType.Boolean:
                    commandList.SetUniformInt(requirement.Name, (int)value.X);
                    break;
            }
        }
    }

    private Vector4? GetUniformValue(MaterialUniformRequirement requirement)
    {
        _material.TryGetAttribute(requirement.Name, out object? attribute);

        return attribute switch
        {
            float scalar	=> new Vector4(scalar, 0.0f, 0.0f, 0.0f),
            Vector2 pair	=> new Vector4(pair, 0.0f, 0.0f),
            Vector3 triple	=> new Vector4(triple, 0.0f),
            Vector4 vector	=> vector,
            int integer		=> new Vector4(integer, 0.0f, 0.0f, 0.0f),
            uint number		=> new Vector4(number, 0.0f, 0.0f, 0.0f),
            bool flag		=> new Vector4(flag ? 1.0f : 0.0f, 0.0f, 0.0f, 0.0f),
            Enum option		=> new Vector4(Convert.ToInt32(option), 0.0f, 0.0f, 0.0f),
            _				=> requirement.DefaultValue is { } defaultValue ? new Vector4(defaultValue.ElementAtOrDefault(0), defaultValue.ElementAtOrDefault(1), defaultValue.ElementAtOrDefault(2), defaultValue.ElementAtOrDefault(3)) : null,
        };
    }

    private void BindTextureResources(ICommandList commandList)
    {
        foreach (MaterialTextureSlot textureSlot in _textureSlots)
        {
            if (textureSlot.Handle is not { } handle || !handle.Bind(commandList, textureSlot.Slot))
            {
                commandList.SetUniformInt(textureSlot.FlagUniform, 0);
                continue;
            }

            commandList.SetUniformInt(textureSlot.SamplerUniform, textureSlot.Slot);
            commandList.SetUniformInt(textureSlot.FlagUniform, 1);
            commandList.SetUniformMatrix4x4(textureSlot.TransformUniform, textureSlot.Channels.Matrix);
            commandList.SetUniformVector4(textureSlot.BiasUniform, textureSlot.Channels.Bias);
            commandList.SetUniformInt(textureSlot.DecodeSrgbUniform, NeedsSrgbDecode(handle.Format) ? 1 : 0);
        }
    }

    private static MaterialTextureSlot CreateTextureSlot(TextureRenderHandle? handle, string samplerUniform, int slot, MaterialChannelTransform channels)
    {
        return new MaterialTextureSlot(handle, samplerUniform, slot, $"Has{samplerUniform}", $"{samplerUniform}Transform", $"{samplerUniform}Bias", $"{samplerUniform}DecodeSrgb", channels);
    }

    private static bool NeedsSrgbDecode(ImageFormat format)
    {
        return format is ImageFormat.R8G8B8A8Unorm or ImageFormat.B8G8R8A8Unorm or ImageFormat.B8G8R8X8Unorm or ImageFormat.BC1Unorm or ImageFormat.BC2Unorm or ImageFormat.BC3Unorm or ImageFormat.BC7Unorm;
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
