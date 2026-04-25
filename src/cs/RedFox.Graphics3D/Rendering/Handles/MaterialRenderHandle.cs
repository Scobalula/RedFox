using System;
using System.Numerics;
using RedFox.Graphics3D.Rendering.Backend;
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
    public override void Update(ICommandList commandList)
    {
        ThrowIfDisposed();
        ArgumentNullException.ThrowIfNull(commandList);

        string requestedTypeName = string.IsNullOrWhiteSpace(_material.Type)
            ? DefaultMaterialTypeName
            : _material.Type.Trim();

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

    private static int ResolveTextureSlot(Texture texture, int fallbackIndex)
    {
        if (int.TryParse(texture.Slot, out int explicitSlot))
        {
            return explicitSlot;
        }

        return texture.Slot switch
        {
            string slot when slot.Equals("diffuse", StringComparison.OrdinalIgnoreCase) => 0,
            string slot when slot.Equals("normal", StringComparison.OrdinalIgnoreCase) => 1,
            string slot when slot.Equals("specular", StringComparison.OrdinalIgnoreCase) => 2,
            string slot when slot.Equals("metallic", StringComparison.OrdinalIgnoreCase) => 3,
            string slot when slot.Equals("roughness", StringComparison.OrdinalIgnoreCase) => 4,
            string slot when slot.Equals("emissive", StringComparison.OrdinalIgnoreCase) => 5,
            string slot when slot.Equals("ao", StringComparison.OrdinalIgnoreCase) => 6,
            _ => fallbackIndex,
        };
    }

    private void SetMaterialUniforms(ICommandList commandList)
    {
        commandList.SetUniformVector4("BaseColor", _material.DiffuseColor ?? Vector4.One);
        commandList.SetUniformFloat("MaterialSpecularStrength", _material.SpecularStrength ?? 0.28f);
        commandList.SetUniformFloat("MaterialSpecularPower", _material.SpecularPower ?? 32.0f);
    }

    private void BindTextureResources(ICommandList commandList)
    {
        IReadOnlyList<MaterialTextureBinding> textureBindings = _material.Textures;
        for (int i = 0; i < textureBindings.Count; i++)
        {
            MaterialTextureBinding binding = textureBindings[i];
            if (binding.Texture.GraphicsHandle is TextureRenderHandle textureHandle && textureHandle.IsOwnedBy(_graphicsDevice))
            {
                textureHandle.Bind(commandList, binding.Slot);
            }
        }
    }

    private void UpdateTextureHandles(ICommandList commandList)
    {
        IReadOnlyList<MaterialTextureBinding> textureBindings = _material.Textures;
        for (int i = 0; i < textureBindings.Count; i++)
        {
            TextureRenderHandle textureHandle = EnsureTextureHandle(textureBindings[i].Texture);
            textureHandle.Update(commandList);
        }
    }
}