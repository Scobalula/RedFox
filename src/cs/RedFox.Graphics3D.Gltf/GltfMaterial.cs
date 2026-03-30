namespace RedFox.Graphics3D.Gltf;

/// <summary>
/// Describes a PBR metallic-roughness material as defined by the glTF 2.0 specification.
/// </summary>
public sealed class GltfMaterial
{
    /// <summary>
    /// Gets or sets the optional name of this material.
    /// </summary>
    public string? Name { get; set; }

    /// <summary>
    /// Gets or sets the base color factor (RGBA, linear). Defaults to opaque white.
    /// </summary>
    public float[] BaseColorFactor { get; set; } = [1f, 1f, 1f, 1f];

    /// <summary>
    /// Gets or sets the index of the base color texture, or -1 if none.
    /// </summary>
    public int BaseColorTextureIndex { get; set; } = -1;

    /// <summary>
    /// Gets or sets the UV coordinate set for the base color texture.
    /// </summary>
    public int BaseColorTextureTexCoord { get; set; }

    /// <summary>
    /// Gets or sets the metallic factor. Defaults to 1.0.
    /// </summary>
    public float MetallicFactor { get; set; } = 1f;

    /// <summary>
    /// Gets or sets the roughness factor. Defaults to 1.0.
    /// </summary>
    public float RoughnessFactor { get; set; } = 1f;

    /// <summary>
    /// Gets or sets the index of the metallic-roughness texture, or -1 if none.
    /// </summary>
    public int MetallicRoughnessTextureIndex { get; set; } = -1;

    /// <summary>
    /// Gets or sets the index of the normal map texture, or -1 if none.
    /// </summary>
    public int NormalTextureIndex { get; set; } = -1;

    /// <summary>
    /// Gets or sets the scale factor for the normal map.
    /// </summary>
    public float NormalTextureScale { get; set; } = 1f;

    /// <summary>
    /// Gets or sets the index of the occlusion texture, or -1 if none.
    /// </summary>
    public int OcclusionTextureIndex { get; set; } = -1;

    /// <summary>
    /// Gets or sets the strength of the occlusion effect.
    /// </summary>
    public float OcclusionTextureStrength { get; set; } = 1f;

    /// <summary>
    /// Gets or sets the emissive factor (RGB, linear). Defaults to black (no emission).
    /// </summary>
    public float[] EmissiveFactor { get; set; } = [0f, 0f, 0f];

    /// <summary>
    /// Gets or sets the index of the emissive texture, or -1 if none.
    /// </summary>
    public int EmissiveTextureIndex { get; set; } = -1;

    /// <summary>
    /// Gets or sets the alpha rendering mode ("OPAQUE", "MASK", or "BLEND").
    /// </summary>
    public string AlphaMode { get; set; } = "OPAQUE";

    /// <summary>
    /// Gets or sets the alpha cutoff threshold for MASK mode.
    /// </summary>
    public float AlphaCutoff { get; set; } = 0.5f;

    /// <summary>
    /// Gets or sets whether the material is double-sided.
    /// </summary>
    public bool DoubleSided { get; set; }
}
