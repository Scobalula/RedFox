using System.Numerics;
using RedFox.Graphics3D;

namespace RedFox.Graphics3D.OpenGL;

/// <summary>
/// Provides helper methods for resolving PBR metallic and roughness values
/// from a <see cref="Material"/>'s scalar and colour properties.
/// </summary>
public static class PbrMaterialFactors
{
    /// <summary>
    /// Resolves the metallic and roughness factors from the given material,
    /// clamping both values to the [0, 1] range.
    /// </summary>
    /// <param name="material">
    /// The material to read PBR factors from, or <c>null</c> to use default values.
    /// </param>
    /// <returns>
    /// A tuple containing the resolved <c>Metallic</c> and <c>Roughness</c> values.
    /// </returns>
    public static (float Metallic, float Roughness) Resolve(Material? material)
    {
        float metallic = ResolveNullable(material?.Metallic, material?.MetallicColor, fallback: 0f);
        float roughness = ResolveRoughness(material);

        metallic = Math.Clamp(metallic, 0.0f, 1.0f);
        roughness = Math.Clamp(roughness, 0.0f, 1.0f);

        return (metallic, roughness);
    }

    private static float ResolveNullable(float? scalar, Vector4? color, float fallback)
    {
        if (scalar.HasValue)
            return scalar.Value;

        if (color.HasValue)
            return color.Value.X;

        return fallback;
    }

    private static float ResolveRoughness(Material? material)
    {
        if (material?.Roughness.HasValue == true)
            return material.Roughness.Value;

        if (material?.RoughnessColor.HasValue == true)
            return material.RoughnessColor.Value.X;

        if (material?.GlossColor.HasValue == true)
            return 1.0f - material.GlossColor.Value.X;

        return 0.5f;
    }
}
