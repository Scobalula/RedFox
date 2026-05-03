using System.Diagnostics.CodeAnalysis;
using System.Numerics;
using RedFox.Graphics3D.Rendering;
using RedFox.Graphics3D.Rendering.Handles;
using RedFox.Graphics3D.Rendering.Materials;

namespace RedFox.Graphics3D;

/// <summary>
/// Represents a material node that defines surface properties and texture bindings.
/// Textures live anywhere in the scene graph and are associated with this material through
/// explicit slot-keyed connections managed by <see cref="Connect"/>, <see cref="Disconnect"/>,
/// and <see cref="TryGetTexture"/>.
/// </summary>
public class Material(string name) : SceneNode(name)
{
    private readonly List<MaterialTextureBinding> _connections = [];
    private uint _version;

    /// <summary>
    /// Gets the mutation version of the connection list. Incremented on every
    /// <see cref="Connect"/> or <see cref="Disconnect"/> call.
    /// </summary>
    /// <summary>Gets the mutation version counter. Incremented on every <see cref="Connect"/> or <see cref="Disconnect"/> call; useful for cache invalidation.</summary>
    public uint Version => _version;

    // ── *MapName slot-key strings ───────────────────────────────────────────

    /// <summary>
    /// Gets or sets the slot key that identifies the diffuse map on this material's shader.
    /// </summary>
    public string? DiffuseMapName { get; set; }

    /// <summary>
    /// Gets or sets the slot key that identifies the specular map on this material's shader.
    /// </summary>
    public string? SpecularMapName { get; set; }

    /// <summary>
    /// Gets or sets the slot key that identifies the metallic map on this material's shader.
    /// </summary>
    public string? MetallicMapName { get; set; }

    /// <summary>
    /// Gets or sets the slot key that identifies the gloss map on this material's shader.
    /// </summary>
    public string? GlossMapName { get; set; }

    /// <summary>
    /// Gets or sets the slot key that identifies the roughness map on this material's shader.
    /// </summary>
    public string? RoughnessMapName { get; set; }

    /// <summary>
    /// Gets or sets the slot key that identifies the normal map on this material's shader.
    /// </summary>
    public string? NormalMapName { get; set; }

    /// <summary>
    /// Gets or sets the slot key that identifies the emissive map on this material's shader.
    /// </summary>
    public string? EmissiveMapName { get; set; }

    /// <summary>
    /// Gets or sets the slot key that identifies the emissive mask map on this material's shader.
    /// </summary>
    public string? EmissiveMaskMapName { get; set; }

    /// <summary>
    /// Gets or sets the slot key that identifies the ambient occlusion map on this material's shader.
    /// </summary>
    public string? AmbientOcclusionMapName { get; set; }

    /// <summary>
    /// Gets or sets the slot key that identifies the cavity map on this material's shader.
    /// </summary>
    public string? CavityMapName { get; set; }

    /// <summary>
    /// Gets or sets the slot key that identifies the anisotropy map on this material's shader.
    /// </summary>
    public string? AnisotropyMapName { get; set; }

    // ── Color / scalar properties ───────────────────────────────────────────

    /// <summary>
    /// Gets or sets the diffuse color.
    /// </summary>
    public Vector4? DiffuseColor { get; set; }

    /// <summary>
    /// Gets or sets the specular color.
    /// </summary>
    public Vector4? SpecularColor { get; set; }

    /// <summary>
    /// Gets or sets the metallic color.
    /// </summary>
    public Vector4? MetallicColor { get; set; }

    /// <summary>
    /// Gets or sets the gloss color.
    /// </summary>
    public Vector4? GlossColor { get; set; }

    /// <summary>
    /// Gets or sets the roughness color.
    /// </summary>
    public Vector4? RoughnessColor { get; set; }

    /// <summary>
    /// Gets or sets the emissive color.
    /// </summary>
    public Vector4? EmissiveColor { get; set; }

    /// <summary>
    /// Gets or sets the ambient occlusion color.
    /// </summary>
    public Vector4? AmbientOcclusionColor { get; set; }

    /// <summary>
    /// Gets or sets the cavity color.
    /// </summary>
    public Vector4? CavityColor { get; set; }

    /// <summary>
    /// Gets or sets the anisotropy color.
    /// </summary>
    public Vector4? AnisotropyColor { get; set; }

    /// <summary>
    /// Gets or sets the shininess value.
    /// </summary>
    public float? Shininess { get; set; }

    /// <summary>
    /// Gets or sets the specular strength.
    /// </summary>
    public float? SpecularStrength { get; set; }

    /// <summary>
    /// Gets or sets the specular power.
    /// </summary>
    public float? SpecularPower
    {
        get => Shininess;
        set => Shininess = value;
    }

    /// <summary>
    /// Gets or sets the metallic factor (0.0 = dielectric, 1.0 = metallic).
    /// Used for IBL and PBR rendering.
    /// </summary>
    public float? Metallic { get; set; } = 0;

    /// <summary>
    /// Gets or sets the roughness factor (0.0 = smooth, 1.0 = rough).
    /// Used for IBL and PBR rendering.
    /// </summary>
    public float? Roughness { get; set; } = 0;

    /// <summary>
    /// Gets or sets a value indicating whether the material should render both front and back faces.
    /// </summary>
    public bool DoubleSided { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether the specular mask is stored in the alpha channel of the diffuse texture.
    /// </summary>
    public bool SpecularMaskInDiffuseAlpha { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether a specular color map is used in rendering.
    /// </summary>
    public bool UseSpecularColorMap { get; set; }

    /// <summary>
    /// Gets or sets the material type name used to resolve renderer material behavior.
    /// </summary>
    public string? Type { get; set; }

    // ── Typed *Map accessors ────────────────────────────────────────────────

    /// <summary>
    /// Gets or sets the diffuse <see cref="Texture"/> connected at <see cref="DiffuseMapName"/>.
    /// The setter requires <see cref="DiffuseMapName"/> to be set first; passing <see langword="null"/> disconnects.
    /// </summary>
    /// <exception cref="InvalidOperationException">
    /// Thrown by the setter when <see cref="DiffuseMapName"/> is <see langword="null"/> and a non-null value is supplied.
    /// </exception>
    public Texture? DiffuseMap
    {
        get => DiffuseMapName is not null && TryGetTexture(DiffuseMapName, out Texture? t) ? t : null;
        set => SetMapTexture(DiffuseMapName, nameof(DiffuseMapName), value);
    }

    /// <summary>
    /// Gets or sets the normal <see cref="Texture"/> connected at <see cref="NormalMapName"/>.
    /// The setter requires <see cref="NormalMapName"/> to be set first; passing <see langword="null"/> disconnects.
    /// </summary>
    /// <exception cref="InvalidOperationException">
    /// Thrown by the setter when <see cref="NormalMapName"/> is <see langword="null"/> and a non-null value is supplied.
    /// </exception>
    public Texture? NormalMap
    {
        get => NormalMapName is not null && TryGetTexture(NormalMapName, out Texture? t) ? t : null;
        set => SetMapTexture(NormalMapName, nameof(NormalMapName), value);
    }

    /// <summary>
    /// Gets or sets the specular <see cref="Texture"/> connected at <see cref="SpecularMapName"/>.
    /// The setter requires <see cref="SpecularMapName"/> to be set first; passing <see langword="null"/> disconnects.
    /// </summary>
    /// <exception cref="InvalidOperationException">
    /// Thrown by the setter when <see cref="SpecularMapName"/> is <see langword="null"/> and a non-null value is supplied.
    /// </exception>
    public Texture? SpecularMap
    {
        get => SpecularMapName is not null && TryGetTexture(SpecularMapName, out Texture? t) ? t : null;
        set => SetMapTexture(SpecularMapName, nameof(SpecularMapName), value);
    }

    /// <summary>
    /// Gets or sets the metallic <see cref="Texture"/> connected at <see cref="MetallicMapName"/>.
    /// The setter requires <see cref="MetallicMapName"/> to be set first; passing <see langword="null"/> disconnects.
    /// </summary>
    /// <exception cref="InvalidOperationException">
    /// Thrown by the setter when <see cref="MetallicMapName"/> is <see langword="null"/> and a non-null value is supplied.
    /// </exception>
    public Texture? MetallicMap
    {
        get => MetallicMapName is not null && TryGetTexture(MetallicMapName, out Texture? t) ? t : null;
        set => SetMapTexture(MetallicMapName, nameof(MetallicMapName), value);
    }

    /// <summary>
    /// Gets or sets the roughness <see cref="Texture"/> connected at <see cref="RoughnessMapName"/>.
    /// The setter requires <see cref="RoughnessMapName"/> to be set first; passing <see langword="null"/> disconnects.
    /// </summary>
    /// <exception cref="InvalidOperationException">
    /// Thrown by the setter when <see cref="RoughnessMapName"/> is <see langword="null"/> and a non-null value is supplied.
    /// </exception>
    public Texture? RoughnessMap
    {
        get => RoughnessMapName is not null && TryGetTexture(RoughnessMapName, out Texture? t) ? t : null;
        set => SetMapTexture(RoughnessMapName, nameof(RoughnessMapName), value);
    }

    /// <summary>
    /// Gets or sets the gloss <see cref="Texture"/> connected at <see cref="GlossMapName"/>.
    /// The setter requires <see cref="GlossMapName"/> to be set first; passing <see langword="null"/> disconnects.
    /// </summary>
    /// <exception cref="InvalidOperationException">
    /// Thrown by the setter when <see cref="GlossMapName"/> is <see langword="null"/> and a non-null value is supplied.
    /// </exception>
    public Texture? GlossMap
    {
        get => GlossMapName is not null && TryGetTexture(GlossMapName, out Texture? t) ? t : null;
        set => SetMapTexture(GlossMapName, nameof(GlossMapName), value);
    }

    /// <summary>
    /// Gets or sets the emissive <see cref="Texture"/> connected at <see cref="EmissiveMapName"/>.
    /// The setter requires <see cref="EmissiveMapName"/> to be set first; passing <see langword="null"/> disconnects.
    /// </summary>
    /// <exception cref="InvalidOperationException">
    /// Thrown by the setter when <see cref="EmissiveMapName"/> is <see langword="null"/> and a non-null value is supplied.
    /// </exception>
    public Texture? EmissiveMap
    {
        get => EmissiveMapName is not null && TryGetTexture(EmissiveMapName, out Texture? t) ? t : null;
        set => SetMapTexture(EmissiveMapName, nameof(EmissiveMapName), value);
    }

    /// <summary>
    /// Gets or sets the emissive mask <see cref="Texture"/> connected at <see cref="EmissiveMaskMapName"/>.
    /// The setter requires <see cref="EmissiveMaskMapName"/> to be set first; passing <see langword="null"/> disconnects.
    /// </summary>
    /// <exception cref="InvalidOperationException">
    /// Thrown by the setter when <see cref="EmissiveMaskMapName"/> is <see langword="null"/> and a non-null value is supplied.
    /// </exception>
    public Texture? EmissiveMaskMap
    {
        get => EmissiveMaskMapName is not null && TryGetTexture(EmissiveMaskMapName, out Texture? t) ? t : null;
        set => SetMapTexture(EmissiveMaskMapName, nameof(EmissiveMaskMapName), value);
    }

    /// <summary>
    /// Gets or sets the ambient occlusion <see cref="Texture"/> connected at <see cref="AmbientOcclusionMapName"/>.
    /// The setter requires <see cref="AmbientOcclusionMapName"/> to be set first; passing <see langword="null"/> disconnects.
    /// </summary>
    /// <exception cref="InvalidOperationException">
    /// Thrown by the setter when <see cref="AmbientOcclusionMapName"/> is <see langword="null"/> and a non-null value is supplied.
    /// </exception>
    public Texture? AmbientOcclusionMap
    {
        get => AmbientOcclusionMapName is not null && TryGetTexture(AmbientOcclusionMapName, out Texture? t) ? t : null;
        set => SetMapTexture(AmbientOcclusionMapName, nameof(AmbientOcclusionMapName), value);
    }

    /// <summary>
    /// Gets or sets the cavity <see cref="Texture"/> connected at <see cref="CavityMapName"/>.
    /// The setter requires <see cref="CavityMapName"/> to be set first; passing <see langword="null"/> disconnects.
    /// </summary>
    /// <exception cref="InvalidOperationException">
    /// Thrown by the setter when <see cref="CavityMapName"/> is <see langword="null"/> and a non-null value is supplied.
    /// </exception>
    public Texture? CavityMap
    {
        get => CavityMapName is not null && TryGetTexture(CavityMapName, out Texture? t) ? t : null;
        set => SetMapTexture(CavityMapName, nameof(CavityMapName), value);
    }

    /// <summary>
    /// Gets or sets the anisotropy <see cref="Texture"/> connected at <see cref="AnisotropyMapName"/>.
    /// The setter requires <see cref="AnisotropyMapName"/> to be set first; passing <see langword="null"/> disconnects.
    /// </summary>
    /// <exception cref="InvalidOperationException">
    /// Thrown by the setter when <see cref="AnisotropyMapName"/> is <see langword="null"/> and a non-null value is supplied.
    /// </exception>
    public Texture? AnisotropyMap
    {
        get => AnisotropyMapName is not null && TryGetTexture(AnisotropyMapName, out Texture? t) ? t : null;
        set => SetMapTexture(AnisotropyMapName, nameof(AnisotropyMapName), value);
    }

    // ── Connection list ─────────────────────────────────────────────────────

    /// <summary>
    /// Gets the live texture binding list. The reference is stable until a <see cref="Connect"/>
    /// or <see cref="Disconnect"/> call; use <see cref="Version"/> to detect mutations.
    /// </summary>
    public IReadOnlyList<MaterialTextureBinding> Textures => _connections;

    /// <summary>
    /// Initializes a new instance of the <see cref="Material"/> class with default values.
    /// </summary>
    public Material() : this(string.Empty) { }

    // ── Public API ──────────────────────────────────────────────────────────

    /// <inheritdoc/>
    public override IRenderHandle? CreateRenderHandle(IGraphicsDevice graphicsDevice, IMaterialTypeRegistry materialTypes)
        => EnsureGraphicsHandle(graphicsDevice);

    internal MaterialRenderHandle EnsureGraphicsHandle(IGraphicsDevice graphicsDevice)
    {
        ArgumentNullException.ThrowIfNull(graphicsDevice);

        if (GraphicsHandle is MaterialRenderHandle existingHandle && existingHandle.IsOwnedBy(graphicsDevice))
        {
            return existingHandle;
        }

        if (GraphicsHandle is not null)
        {
            GraphicsHandle.Release();
            GraphicsHandle.Dispose();
        }

        MaterialRenderHandle materialHandle = new(graphicsDevice, this);
        GraphicsHandle = materialHandle;
        return materialHandle;
    }

    /// <summary>
    /// Connects <paramref name="texture"/> to this material under <paramref name="slotKey"/>.
    /// If a binding for that key already exists it is replaced in-place (preserving the numeric
    /// slot index); otherwise a new binding is appended and the next-free numeric index is assigned.
    /// </summary>
    /// <param name="slotKey">The shader slot key (e.g. <c>"diffuse"</c>, <c>"BaseMetalMap"</c>).</param>
    /// <param name="texture">The texture to bind.</param>
    public void Connect(string slotKey, Texture texture)
    {
        ArgumentNullException.ThrowIfNull(slotKey);
        ArgumentNullException.ThrowIfNull(texture);

        for (int i = 0; i < _connections.Count; i++)
        {
            if (string.Equals(_connections[i].SamplerUniform, slotKey, StringComparison.Ordinal))
            {
                _connections[i] = new MaterialTextureBinding(texture, _connections[i].Slot, slotKey);
                _version++;
                return;
            }
        }

        int numericSlot = int.TryParse(slotKey, out int parsed) ? parsed : _connections.Count;
        string samplerUniform = int.TryParse(slotKey, out _) ? $"Texture{numericSlot}" : slotKey;
        _connections.Add(new MaterialTextureBinding(texture, numericSlot, samplerUniform));
        _version++;
    }

    /// <summary>
    /// Removes the binding for <paramref name="slotKey"/> if one exists.
    /// Has no effect when the slot key is not connected.
    /// </summary>
    /// <param name="slotKey">The shader slot key to disconnect.</param>
    public void Disconnect(string slotKey)
    {
        ArgumentNullException.ThrowIfNull(slotKey);

        for (int i = 0; i < _connections.Count; i++)
        {
            if (string.Equals(_connections[i].SamplerUniform, slotKey, StringComparison.Ordinal))
            {
                _connections.RemoveAt(i);
                _version++;
                return;
            }
        }
    }

    /// <summary>
    /// Returns the texture connected at <paramref name="slotKey"/>.
    /// </summary>
    /// <param name="slotKey">The shader slot key to look up.</param>
    /// <param name="texture">The connected texture if found; otherwise <see langword="null"/>.</param>
    /// <returns><see langword="true"/> when a binding for <paramref name="slotKey"/> exists.</returns>
    public bool TryGetTexture(string slotKey, [NotNullWhen(true)] out Texture? texture)
    {
        foreach (MaterialTextureBinding binding in _connections)
        {
            if (string.Equals(binding.SamplerUniform, slotKey, StringComparison.Ordinal))
            {
                texture = binding.Texture;
                return true;
            }
        }

        texture = null;
        return false;
    }

    /// <summary>
    /// Returns the texture map slot key for the given semantic name, or <see langword="null"/>
    /// when the slot is not assigned. Used by schema-driven renderers to resolve material
    /// texture references dynamically.
    /// </summary>
    /// <param name="slotName">The semantic slot name (e.g. <c>"diffuse"</c>, <c>"normal"</c>).</param>
    /// <returns>The slot key string stored on this material for the given semantic, or <see langword="null"/>.</returns>
    public string? GetMapName(string slotName) => slotName switch
    {
        "diffuse"    => DiffuseMapName,
        "normal"     => NormalMapName,
        "specular"   => SpecularMapName,
        "metallic"   => MetallicMapName,
        "emissive"   => EmissiveMapName,
        "roughness"  => RoughnessMapName,
        "gloss"      => GlossMapName,
        "ao"         => AmbientOcclusionMapName,
        "cavity"     => CavityMapName,
        "anisotropy" => AnisotropyMapName,
        _            => null,
    };

    // ── TryGet*/Get* typed helpers ──────────────────────────────────────────

    /// <summary>
    /// Attempts to retrieve the diffuse map texture connected at <see cref="DiffuseMapName"/>.
    /// </summary>
    /// <param name="texture">The texture if connected; otherwise <see langword="null"/>.</param>
    /// <returns><see langword="true"/> when a diffuse texture is connected.</returns>
    public bool TryGetDiffuseMap([NotNullWhen(true)] out Texture? texture)
        => DiffuseMapName is not null ? TryGetTexture(DiffuseMapName, out texture) : NullOut(out texture);

    /// <summary>
    /// Returns the diffuse map texture connected at <see cref="DiffuseMapName"/>.
    /// </summary>
    /// <returns>The connected diffuse texture.</returns>
    /// <exception cref="InvalidOperationException">Thrown when <see cref="DiffuseMapName"/> is not set.</exception>
    /// <exception cref="KeyNotFoundException">Thrown when no texture is connected at the slot key.</exception>
    public Texture GetDiffuseMap() => GetRequiredTexture(DiffuseMapName, nameof(DiffuseMapName));

    /// <summary>
    /// Attempts to retrieve the normal map texture connected at <see cref="NormalMapName"/>.
    /// </summary>
    /// <param name="texture">The texture if connected; otherwise <see langword="null"/>.</param>
    /// <returns><see langword="true"/> when a normal texture is connected.</returns>
    public bool TryGetNormalMap([NotNullWhen(true)] out Texture? texture)
        => NormalMapName is not null ? TryGetTexture(NormalMapName, out texture) : NullOut(out texture);

    /// <summary>
    /// Returns the normal map texture connected at <see cref="NormalMapName"/>.
    /// </summary>
    /// <returns>The connected normal texture.</returns>
    /// <exception cref="InvalidOperationException">Thrown when <see cref="NormalMapName"/> is not set.</exception>
    /// <exception cref="KeyNotFoundException">Thrown when no texture is connected at the slot key.</exception>
    public Texture GetNormalMap() => GetRequiredTexture(NormalMapName, nameof(NormalMapName));

    /// <summary>
    /// Attempts to retrieve the specular map texture connected at <see cref="SpecularMapName"/>.
    /// </summary>
    /// <param name="texture">The texture if connected; otherwise <see langword="null"/>.</param>
    /// <returns><see langword="true"/> when a specular texture is connected.</returns>
    public bool TryGetSpecularMap([NotNullWhen(true)] out Texture? texture)
        => SpecularMapName is not null ? TryGetTexture(SpecularMapName, out texture) : NullOut(out texture);

    /// <summary>
    /// Returns the specular map texture connected at <see cref="SpecularMapName"/>.
    /// </summary>
    /// <returns>The connected specular texture.</returns>
    /// <exception cref="InvalidOperationException">Thrown when <see cref="SpecularMapName"/> is not set.</exception>
    /// <exception cref="KeyNotFoundException">Thrown when no texture is connected at the slot key.</exception>
    public Texture GetSpecularMap() => GetRequiredTexture(SpecularMapName, nameof(SpecularMapName));

    /// <summary>
    /// Attempts to retrieve the metallic map texture connected at <see cref="MetallicMapName"/>.
    /// </summary>
    /// <param name="texture">The texture if connected; otherwise <see langword="null"/>.</param>
    /// <returns><see langword="true"/> when a metallic texture is connected.</returns>
    public bool TryGetMetallicMap([NotNullWhen(true)] out Texture? texture)
        => MetallicMapName is not null ? TryGetTexture(MetallicMapName, out texture) : NullOut(out texture);

    /// <summary>
    /// Returns the metallic map texture connected at <see cref="MetallicMapName"/>.
    /// </summary>
    /// <returns>The connected metallic texture.</returns>
    /// <exception cref="InvalidOperationException">Thrown when <see cref="MetallicMapName"/> is not set.</exception>
    /// <exception cref="KeyNotFoundException">Thrown when no texture is connected at the slot key.</exception>
    public Texture GetMetallicMap() => GetRequiredTexture(MetallicMapName, nameof(MetallicMapName));

    /// <summary>
    /// Attempts to retrieve the roughness map texture connected at <see cref="RoughnessMapName"/>.
    /// </summary>
    /// <param name="texture">The texture if connected; otherwise <see langword="null"/>.</param>
    /// <returns><see langword="true"/> when a roughness texture is connected.</returns>
    public bool TryGetRoughnessMap([NotNullWhen(true)] out Texture? texture)
        => RoughnessMapName is not null ? TryGetTexture(RoughnessMapName, out texture) : NullOut(out texture);

    /// <summary>
    /// Returns the roughness map texture connected at <see cref="RoughnessMapName"/>.
    /// </summary>
    /// <returns>The connected roughness texture.</returns>
    /// <exception cref="InvalidOperationException">Thrown when <see cref="RoughnessMapName"/> is not set.</exception>
    /// <exception cref="KeyNotFoundException">Thrown when no texture is connected at the slot key.</exception>
    public Texture GetRoughnessMap() => GetRequiredTexture(RoughnessMapName, nameof(RoughnessMapName));

    /// <summary>
    /// Attempts to retrieve the gloss map texture connected at <see cref="GlossMapName"/>.
    /// </summary>
    /// <param name="texture">The texture if connected; otherwise <see langword="null"/>.</param>
    /// <returns><see langword="true"/> when a gloss texture is connected.</returns>
    public bool TryGetGlossMap([NotNullWhen(true)] out Texture? texture)
        => GlossMapName is not null ? TryGetTexture(GlossMapName, out texture) : NullOut(out texture);

    /// <summary>
    /// Returns the gloss map texture connected at <see cref="GlossMapName"/>.
    /// </summary>
    /// <returns>The connected gloss texture.</returns>
    /// <exception cref="InvalidOperationException">Thrown when <see cref="GlossMapName"/> is not set.</exception>
    /// <exception cref="KeyNotFoundException">Thrown when no texture is connected at the slot key.</exception>
    public Texture GetGlossMap() => GetRequiredTexture(GlossMapName, nameof(GlossMapName));

    /// <summary>
    /// Attempts to retrieve the emissive map texture connected at <see cref="EmissiveMapName"/>.
    /// </summary>
    /// <param name="texture">The texture if connected; otherwise <see langword="null"/>.</param>
    /// <returns><see langword="true"/> when an emissive texture is connected.</returns>
    public bool TryGetEmissiveMap([NotNullWhen(true)] out Texture? texture)
        => EmissiveMapName is not null ? TryGetTexture(EmissiveMapName, out texture) : NullOut(out texture);

    /// <summary>
    /// Returns the emissive map texture connected at <see cref="EmissiveMapName"/>.
    /// </summary>
    /// <returns>The connected emissive texture.</returns>
    /// <exception cref="InvalidOperationException">Thrown when <see cref="EmissiveMapName"/> is not set.</exception>
    /// <exception cref="KeyNotFoundException">Thrown when no texture is connected at the slot key.</exception>
    public Texture GetEmissiveMap() => GetRequiredTexture(EmissiveMapName, nameof(EmissiveMapName));

    /// <summary>
    /// Attempts to retrieve the emissive mask map texture connected at <see cref="EmissiveMaskMapName"/>.
    /// </summary>
    /// <param name="texture">The texture if connected; otherwise <see langword="null"/>.</param>
    /// <returns><see langword="true"/> when an emissive mask texture is connected.</returns>
    public bool TryGetEmissiveMaskMap([NotNullWhen(true)] out Texture? texture)
        => EmissiveMaskMapName is not null ? TryGetTexture(EmissiveMaskMapName, out texture) : NullOut(out texture);

    /// <summary>
    /// Returns the emissive mask map texture connected at <see cref="EmissiveMaskMapName"/>.
    /// </summary>
    /// <returns>The connected emissive mask texture.</returns>
    /// <exception cref="InvalidOperationException">Thrown when <see cref="EmissiveMaskMapName"/> is not set.</exception>
    /// <exception cref="KeyNotFoundException">Thrown when no texture is connected at the slot key.</exception>
    public Texture GetEmissiveMaskMap() => GetRequiredTexture(EmissiveMaskMapName, nameof(EmissiveMaskMapName));

    /// <summary>
    /// Attempts to retrieve the ambient occlusion map texture connected at <see cref="AmbientOcclusionMapName"/>.
    /// </summary>
    /// <param name="texture">The texture if connected; otherwise <see langword="null"/>.</param>
    /// <returns><see langword="true"/> when an ambient occlusion texture is connected.</returns>
    public bool TryGetAmbientOcclusionMap([NotNullWhen(true)] out Texture? texture)
        => AmbientOcclusionMapName is not null ? TryGetTexture(AmbientOcclusionMapName, out texture) : NullOut(out texture);

    /// <summary>
    /// Returns the ambient occlusion map texture connected at <see cref="AmbientOcclusionMapName"/>.
    /// </summary>
    /// <returns>The connected ambient occlusion texture.</returns>
    /// <exception cref="InvalidOperationException">Thrown when <see cref="AmbientOcclusionMapName"/> is not set.</exception>
    /// <exception cref="KeyNotFoundException">Thrown when no texture is connected at the slot key.</exception>
    public Texture GetAmbientOcclusionMap() => GetRequiredTexture(AmbientOcclusionMapName, nameof(AmbientOcclusionMapName));

    /// <summary>
    /// Attempts to retrieve the cavity map texture connected at <see cref="CavityMapName"/>.
    /// </summary>
    /// <param name="texture">The texture if connected; otherwise <see langword="null"/>.</param>
    /// <returns><see langword="true"/> when a cavity texture is connected.</returns>
    public bool TryGetCavityMap([NotNullWhen(true)] out Texture? texture)
        => CavityMapName is not null ? TryGetTexture(CavityMapName, out texture) : NullOut(out texture);

    /// <summary>
    /// Returns the cavity map texture connected at <see cref="CavityMapName"/>.
    /// </summary>
    /// <returns>The connected cavity texture.</returns>
    /// <exception cref="InvalidOperationException">Thrown when <see cref="CavityMapName"/> is not set.</exception>
    /// <exception cref="KeyNotFoundException">Thrown when no texture is connected at the slot key.</exception>
    public Texture GetCavityMap() => GetRequiredTexture(CavityMapName, nameof(CavityMapName));

    /// <summary>
    /// Attempts to retrieve the anisotropy map texture connected at <see cref="AnisotropyMapName"/>.
    /// </summary>
    /// <param name="texture">The texture if connected; otherwise <see langword="null"/>.</param>
    /// <returns><see langword="true"/> when an anisotropy texture is connected.</returns>
    public bool TryGetAnisotropyMap([NotNullWhen(true)] out Texture? texture)
        => AnisotropyMapName is not null ? TryGetTexture(AnisotropyMapName, out texture) : NullOut(out texture);

    /// <summary>
    /// Returns the anisotropy map texture connected at <see cref="AnisotropyMapName"/>.
    /// </summary>
    /// <returns>The connected anisotropy texture.</returns>
    /// <exception cref="InvalidOperationException">Thrown when <see cref="AnisotropyMapName"/> is not set.</exception>
    /// <exception cref="KeyNotFoundException">Thrown when no texture is connected at the slot key.</exception>
    public Texture GetAnisotropyMap() => GetRequiredTexture(AnisotropyMapName, nameof(AnisotropyMapName));

    // ── Private helpers ─────────────────────────────────────────────────────

    private void SetMapTexture(string? slotKey, string slotKeyPropertyName, Texture? value)
    {
        if (value is null)
        {
            if (slotKey is not null)
            {
                Disconnect(slotKey);
            }

            return;
        }

        if (slotKey is null)
        {
            throw new InvalidOperationException($"Set {slotKeyPropertyName} first to specify the slot key before assigning a texture.");
        }

        Connect(slotKey, value);
    }

    private Texture GetRequiredTexture(string? slotKey, string slotKeyPropertyName)
    {
        if (slotKey is null)
        {
            throw new InvalidOperationException($"{slotKeyPropertyName} is not set on material '{Name}'.");
        }

        if (!TryGetTexture(slotKey, out Texture? texture))
        {
            throw new KeyNotFoundException($"No texture connected at slot '{slotKey}' on material '{Name}'.");
        }

        return texture;
    }

    private static bool NullOut([NotNullWhen(true)] out Texture? texture)
    {
        texture = null;
        return false;
    }
}