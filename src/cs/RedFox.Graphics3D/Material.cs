using System.Diagnostics.CodeAnalysis;
using System.Numerics;

namespace RedFox.Graphics3D;

/// <summary>
/// Represents a material node that defines surface properties and texture references.
/// </summary>
public class Material(string name) : SceneNode(name)
{
    /// <summary>
    /// Initializes a new instance of the Material class with default values.
    /// </summary>
    public Material() : this(string.Empty) { }

    /// <summary>
    /// Gets or sets the name of the diffuse <see cref="Texture"/> node.
    /// </summary>
    public string? DiffuseMapName { get; set; }

    /// <summary>
    /// Gets or sets the name of the specular <see cref="Texture"/> node.
    /// </summary>
    public string? SpecularMapName { get; set; }

    /// <summary>
    /// Gets or sets the name of the metallic <see cref="Texture"/> node.
    /// </summary>
    public string? MetallicMapName { get; set; }

    /// <summary>
    /// Gets or sets the name of the gloss <see cref="Texture"/> node.
    /// </summary>
    public string? GlossMapName { get; set; }

    /// <summary>
    /// Gets or sets the name of the roughness <see cref="Texture"/> node.
    /// </summary>
    public string? RoughnessMapName { get; set; }

    /// <summary>
    /// Gets or sets the name of the normal <see cref="Texture"/> node.
    /// </summary>
    public string? NormalMapName { get; set; }

    /// <summary>
    /// Gets or sets the name of the emissive <see cref="Texture"/> node.
    /// </summary>
    public string? EmissiveMapName { get; set; }
    
    /// <summary>
    /// Gets or sets the name of the emissive mask <see cref="Texture"/> node.
    /// </summary>
    public string? EmissiveMaskMapName { get; set; }

    /// <summary>
    /// Gets or sets the name of the ambient occlusion <see cref="Texture"/> node.
    /// </summary>
    public string? AmbientOcclusionMapName { get; set; }

    /// <summary>
    /// Gets or sets the name of the cavity <see cref="Texture"/> node.
    /// </summary>
    public string? CavityMapName { get; set; }

    /// <summary>
    /// Gets or sets the name of the anisotropy <see cref="Texture"/> node.
    /// </summary>
    public string? AnisotropyMapName { get; set; }

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
    /// Gets or sets a value indicating whether the specular mask is stored in the alpha channel of the diffuse texture.
    /// </summary>
    public bool SpecularMaskInDiffuseAlpha { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether a specular color map is used in rendering.
    /// </summary>
    public bool UseSpecularColorMap { get; set; }

    /// <summary>
    /// Returns the texture map name for the given semantic slot, or <see langword="null"/>
    /// if the slot is not assigned. Used by schema-driven renderers to resolve
    /// material texture references dynamically.
    /// </summary>
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

    /// <summary>
    /// Attempts to retrieve the diffuse map texture associated with this instance.
    /// </summary>
    /// <param name="texture">The texture if found, otherwise null.</param>
    /// <returns>true if a texture with the specified name is found; otherwise, false.</returns>
    public bool TryGetDiffuseMap([NotNullWhen(true)] out Texture? texture)
    {
        if (DiffuseMapName is null)
        {
            texture = null;
            return false;
        }

        return TryFindTexture(DiffuseMapName, out texture);
    }

    /// <summary>
    /// Attempts to retrieve the diffuse map texture associated with this instance.
    /// </summary>
    /// <returns>The texture associated with the diffuse map of the material.</returns>
    /// <exception cref="InvalidOperationException">Thrown if the diffuse map name is not set for this material.</exception>
    public Texture GetDiffuseMap()
    {
        if (DiffuseMapName is null)
            throw new InvalidOperationException($"{nameof(DiffuseMapName)} is not set on material '{Name}'.");

        return FindTexture(DiffuseMapName);
    }

    /// <summary>
    /// Attempts to retrieve the specular map texture associated with this instance.
    /// </summary>
    /// <param name="texture">The texture if found, otherwise null.</param>
    /// <returns>true if a texture with the specified name is found; otherwise, false.</returns>
    public bool TryGetSpecularMap([NotNullWhen(true)] out Texture? texture)
    {
        if (SpecularMapName is null)
        {
            texture = null;
            return false;
        }

        return TryFindTexture(SpecularMapName, out texture);
    }

    /// <summary>
    /// Retrieves the specular map texture associated with this instance.
    /// </summary>
    /// <returns>The texture associated with the specular map of the material.</returns>
    /// <exception cref="InvalidOperationException">Thrown if the specular map name is not set for this material.</exception>
    public Texture GetSpecularMap()
    {
        if (SpecularMapName is null)
            throw new InvalidOperationException($"{nameof(SpecularMapName)} is not set on material '{Name}'.");

        return FindTexture(SpecularMapName);
    }

    /// <summary>
    /// Attempts to retrieve the metallic map texture associated with this instance.
    /// </summary>
    /// <param name="texture">The texture if found, otherwise null.</param>
    /// <returns>true if a texture with the specified name is found; otherwise, false.</returns>
    public bool TryGetMetallicMap([NotNullWhen(true)] out Texture? texture)
    {
        if (MetallicMapName is null)
        {
            texture = null;
            return false;
        }

        return TryFindTexture(MetallicMapName, out texture);
    }


    /// <summary>
    /// Retrieves the metallic map texture associated with this instance.
    /// </summary>
    /// <returns>The texture associated with the metallic map of the material.</returns>
    /// <exception cref="InvalidOperationException">Thrown if the metallic map name is not set for this material.</exception>
    public Texture GetMetallicMap()
    {
        if (MetallicMapName is null)
            throw new InvalidOperationException($"{nameof(MetallicMapName)} is not set on material '{Name}'.");

        return FindTexture(MetallicMapName);
    }


    /// <summary>
    /// Attempts to retrieve the gloss map texture associated with this instance.
    /// </summary>
    /// <param name="texture">The texture if found, otherwise null.</param>
    /// <returns>true if a texture with the specified name is found; otherwise, false.</returns>
    public bool TryGetGlossMap([NotNullWhen(true)] out Texture? texture)
    {
        if (GlossMapName is null)
        {
            texture = null;
            return false;
        }

        return TryFindTexture(GlossMapName, out texture);
    }


    /// <summary>
    /// Retrieves the gloss map texture associated with this instance.
    /// </summary>
    /// <returns>The texture associated with the gloss map of the material.</returns>
    /// <exception cref="InvalidOperationException">Thrown if the gloss map name is not set for this material.</exception>
    public Texture GetGlossMap()
    {
        if (GlossMapName is null)
            throw new InvalidOperationException($"{nameof(GlossMapName)} is not set on material '{Name}'.");

        return FindTexture(GlossMapName);
    }


    /// <summary>
    /// Attempts to retrieve the roughness map texture associated with this instance.
    /// </summary>
    /// <param name="texture">The texture if found, otherwise null.</param>
    /// <returns>true if a texture with the specified name is found; otherwise, false.</returns>
    public bool TryGetRoughnessMap([NotNullWhen(true)] out Texture? texture)
    {
        if (RoughnessMapName is null)
        {
            texture = null;
            return false;
        }

        return TryFindTexture(RoughnessMapName, out texture);
    }

    /// <summary>
    /// Retrieves the roughness map texture associated with this instance.
    /// </summary>
    /// <returns>The texture associated with the roughness map of the material.</returns>
    /// <exception cref="InvalidOperationException">Thrown if the roughness map name is not set for this material.</exception>
    public Texture GetRoughnessMap()
    {
        if (RoughnessMapName is null)
            throw new InvalidOperationException($"{nameof(RoughnessMapName)} is not set on material '{Name}'.");

        return FindTexture(RoughnessMapName);
    }

    /// <summary>
    /// Attempts to retrieve the normal map texture associated with this instance.
    /// </summary>
    /// <param name="texture">The texture if found, otherwise null.</param>
    /// <returns>true if a texture with the specified name is found; otherwise, false.</returns>
    public bool TryGetNormalMap([NotNullWhen(true)] out Texture? texture)
    {
        if (NormalMapName is null)
        {
            texture = null;
            return false;
        }

        return TryFindTexture(NormalMapName, out texture);
    }

    /// <summary>
    /// Retrieves the normal map texture associated with this instance.
    /// </summary>
    /// <returns>The texture associated with the normal map of the material.</returns>
    /// <exception cref="InvalidOperationException">Thrown if the normal map name is not set for this material.</exception>
    public Texture GetNormalMap()
    {
        if (NormalMapName is null)
            throw new InvalidOperationException($"{nameof(NormalMapName)} is not set on material '{Name}'.");

        return FindTexture(NormalMapName);
    }

    /// <summary>
    /// Attempts to retrieve the emissive map texture associated with this instance.
    /// </summary>
    /// <param name="texture">The texture if found, otherwise null.</param>
    /// <returns>true if a texture with the specified name is found; otherwise, false.</returns>
    public bool TryGetEmissiveMap([NotNullWhen(true)] out Texture? texture)
    {
        if (EmissiveMapName is null)
        {
            texture = null;
            return false;
        }

        return TryFindTexture(EmissiveMapName, out texture);
    }

    /// <summary>
    /// Retrieves the emissive map texture associated with this instance.
    /// </summary>
    /// <returns>The texture associated with the emissive map of the material.</returns>
    /// <exception cref="InvalidOperationException">Thrown if the emissive map name is not set for this material.</exception>
    public Texture GetEmissiveMap()
    {
        if (EmissiveMapName is null)
            throw new InvalidOperationException($"{nameof(EmissiveMapName)} is not set on material '{Name}'.");

        return FindTexture(EmissiveMapName);
    }

    /// <summary>
    /// Attempts to retrieve the emissive mask map texture associated with this instance.
    /// </summary>
    /// <param name="texture">The texture if found, otherwise null.</param>
    /// <returns>true if a texture with the specified name is found; otherwise, false.</returns>
    public bool TryGetEmissiveMaskMap([NotNullWhen(true)] out Texture? texture)
    {
        if (EmissiveMaskMapName is null)
        {
            texture = null;
            return false;
        }

        return TryFindTexture(EmissiveMaskMapName, out texture);
    }

    /// <summary>
    /// Retrieves the emissive mask map texture associated with this instance.
    /// </summary>
    /// <returns>The texture associated with the emissive mask map of the material.</returns>
    /// <exception cref="InvalidOperationException">Thrown if the emissive mask map name is not set for this material.</exception>
    public Texture GetEmissiveMaskMap()
    {
        if (EmissiveMaskMapName is null)
            throw new InvalidOperationException($"{nameof(EmissiveMaskMapName)} is not set on material '{Name}'.");

        return FindTexture(EmissiveMaskMapName);
    }

    /// <summary>
    /// Attempts to retrieve the ambient occlusion map texture associated with this instance.
    /// </summary>
    /// <param name="texture">The texture if found, otherwise null.</param>
    /// <returns>true if a texture with the specified name is found; otherwise, false.</returns>
    public bool TryGetAmbientOcclusionMap([NotNullWhen(true)] out Texture? texture)
    {
        if (AmbientOcclusionMapName is null)
        {
            texture = null;
            return false;
        }

        return TryFindTexture(AmbientOcclusionMapName, out texture);
    }

    /// <summary>
    /// Retrieves the ambient occlusion map texture associated with this instance.
    /// </summary>
    /// <returns>The texture associated with the ambient occlusion map of the material.</returns>
    /// <exception cref="InvalidOperationException">Thrown if the ambient occlusion map name is not set for this material.</exception>
    public Texture GetAmbientOcclusionMap()
    {
        if (AmbientOcclusionMapName is null)
            throw new InvalidOperationException($"{nameof(AmbientOcclusionMapName)} is not set on material '{Name}'.");

        return FindTexture(AmbientOcclusionMapName);
    }

    /// <summary>
    /// Attempts to retrieve the cavity map texture associated with this instance.
    /// </summary>
    /// <param name="texture">The texture if found, otherwise null.</param>
    /// <returns>true if a texture with the specified name is found; otherwise, false.</returns>
    public bool TryGetCavityMap([NotNullWhen(true)] out Texture? texture)
    {
        if (CavityMapName is null)
        {
            texture = null;
            return false;
        }

        return TryFindTexture(CavityMapName, out texture);
    }

    /// <summary>
    /// Retrieves the cavity map texture associated with this instance.
    /// </summary>
    /// <returns>The texture associated with the cavity map of the material.</returns>
    /// <exception cref="InvalidOperationException">Thrown if the cavity map name is not set for this material.</exception>
    public Texture GetCavityMap()
    {
        if (CavityMapName is null)
            throw new InvalidOperationException($"{nameof(CavityMapName)} is not set on material '{Name}'.");

        return FindTexture(CavityMapName);
    }

    /// <summary>
    /// Attempts to retrieve the anisotropy map texture associated with this instance.
    /// </summary>
    /// <param name="texture">The texture if found, otherwise null.</param>
    /// <returns>true if a texture with the specified name is found; otherwise, false.</returns>
    public bool TryGetAnisotropyMap([NotNullWhen(true)] out Texture? texture)
    {
        if (AnisotropyMapName is null)
        {
            texture = null;
            return false;
        }

        return TryFindTexture(AnisotropyMapName, out texture);
    }

    /// <summary>
    /// Retrieves the anisotropy map texture associated with this instance.
    /// </summary>
    /// <returns>The texture associated with the anisotropy map of the material.</returns>
    /// <exception cref="InvalidOperationException">Thrown if the anisotropy map name is not set for this material.</exception>
    public Texture GetAnisotropyMap()
    {
        if (AnisotropyMapName is null)
            throw new InvalidOperationException($"{nameof(AnisotropyMapName)} is not set on material '{Name}'.");

        return FindTexture(AnisotropyMapName);
    }

    /// <summary>
    /// Returns an enumerable collection of all descendant textures in the hierarchy.
    /// </summary>
    /// <returns>An enumerable collection of <see cref="Texture"/> objects.</returns>
    public IEnumerable<Texture> EnumerateTextures() => EnumerateDescendants().OfType<Texture>();

    /// <summary>
    /// Returns an array containing all descendant textures in the hierarchy.
    /// </summary>
    /// <returns>An array of <see cref="Texture"/> objects.</returns>
    public Texture[] GetTextures() => [.. EnumerateTextures()];

    /// <summary>
    /// Attempts to find a texture with the specified name.
    /// </summary>
    /// <param name="name">The name of the texture to locate.</param>
    /// <param name="texture">The texture if found, otherwise null.</param>
    /// <returns>true if a texture with the specified name is found; otherwise, false.</returns>
    public bool TryFindTexture(string name, [NotNullWhen(true)] out Texture? texture)
        => TryFindTexture(name, StringComparison.CurrentCulture, out texture);

    /// <summary>
    /// Attempts to find a texture with the specified name.
    /// </summary>
    /// <param name="name">The name of the mesh to locate.</param>
    /// <param name="comparisonType">A value that specifies the rules for the string comparison used to match the mesh name.</param>
    /// <param name="mesh">The mesh if found, otherwise null.</param>
    /// <returns>true if a texture with the specified name is found; otherwise, false.</returns>
    public bool TryFindTexture(string name, StringComparison comparisonType, [NotNullWhen(true)] out Texture? texture)
        => TryFindDescendant(name, comparisonType, out texture);

    /// <summary>
    /// Attempts to find a texture with the specified name.
    /// </summary>
    /// <param name="name">The name of the texture to locate.</param>
    /// <returns>A texture object that matches the specified name.</returns>
    public Texture FindTexture(string name) => FindTexture(name, StringComparison.CurrentCulture);

    /// <summary>
    /// Attempts to find a texture with the specified name.
    /// </summary>
    /// <param name="name">The name of the texture to locate.</param>
    /// <param name="comparisonType">A value that specifies the rules for the string comparison used to match the texture name.</param>
    /// <returns>A texture object that matches the specified name.</returns>
    /// <exception cref="KeyNotFoundException">Thrown if a texture with the specified name is not found for this material.</exception>
    private Texture FindTexture(string name, StringComparison comparisonType)
    {
        if (TryFindDescendant(name, comparisonType, out Texture? node))
            return node;

        throw new KeyNotFoundException($"A texture with the name: {name} was not found in: {Name}");
    }
}
