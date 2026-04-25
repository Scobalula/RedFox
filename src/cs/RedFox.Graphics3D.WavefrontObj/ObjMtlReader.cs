using System.Globalization;
using System.Numerics;

namespace RedFox.Graphics3D.WavefrontObj;

/// <summary>
/// Reads Wavefront MTL (.mtl) material library files and populates <see cref="Material"/> nodes
/// with texture paths and slot assignments.
/// Supports common material statements: newmtl, map_Kd, map_Ks, map_Bump/bump, map_Ke, map_Ns, and map_d.
/// </summary>
public static class ObjMtlReader
{
    /// <summary>
    /// Reads materials from the specified MTL stream and merges them into the provided material dictionary.
    /// Materials are keyed by name; if a material with the same name already exists, its texture fields are updated.
    /// </summary>
    /// <param name="stream">The input stream containing MTL data.</param>
    /// <param name="materials">A dictionary of materials keyed by name. New materials are added; existing ones are updated.</param>
    public static void Read(Stream stream, Dictionary<string, Material> materials)
        => Read(stream, materials, baseDirectory: null);

    /// <summary>
    /// Reads materials from the specified MTL stream and resolves relative texture paths
    /// against the supplied base directory when available.
    /// </summary>
    public static void Read(Stream stream, Dictionary<string, Material> materials, string? baseDirectory)
    {
        using StreamReader reader = new(stream, leaveOpen: true);

        Material? current = null;

        while (reader.ReadLine() is { } line)
        {
            ReadOnlySpan<char> span = line.AsSpan().Trim();
            if (span.IsEmpty || span[0] == '#')
            {
                continue;
            }

            if (span.StartsWith("newmtl "))
            {
                string matName = span[7..].Trim().ToString();
                if (!materials.TryGetValue(matName, out current))
                {
                    current = new Material(matName);
                    materials[matName] = current;
                }
            }
            else if (current is not null)
            {
                if (span.StartsWith("map_Kd "))
                {
                    string texPath = ExtractTexturePath(span[7..]);
                    current.DiffuseMapName = Path.GetFileNameWithoutExtension(texPath);
                    EnsureTexture(current, texPath, "diffuse", baseDirectory);
                }
                else if (span.StartsWith("Ks "))
                {
                    if (TryParseColor(span[3..], out Vector4 specularColor))
                        current.SpecularColor = specularColor;
                }
                else if (span.StartsWith("Ns "))
                {
                    if (TryParseScalar(span[3..], out float shininess))
                    {
                        current.Shininess = shininess;
                        current.Roughness = ConvertPhongShininessToRoughness(shininess);
                    }
                }
                else if (span.StartsWith("map_Ks "))
                {
                    string texPath = ExtractTexturePath(span[7..]);
                    current.SpecularMapName = Path.GetFileNameWithoutExtension(texPath);
                    EnsureTexture(current, texPath, "specular", baseDirectory);
                }
                else if (span.StartsWith("map_Bump ") || span.StartsWith("bump "))
                {
                    int offset = span.StartsWith("map_Bump ") ? 9 : 5;
                    string texPath = ExtractTexturePath(span[offset..]);
                    current.NormalMapName = Path.GetFileNameWithoutExtension(texPath);
                    EnsureTexture(current, texPath, "normal", baseDirectory);
                }
                else if (span.StartsWith("map_Ke "))
                {
                    string texPath = ExtractTexturePath(span[7..]);
                    current.EmissiveMapName = Path.GetFileNameWithoutExtension(texPath);
                    EnsureTexture(current, texPath, "emissive", baseDirectory);
                }
                else if (span.StartsWith("map_Ns "))
                {
                    string texPath = ExtractTexturePath(span[7..]);
                    current.GlossMapName = Path.GetFileNameWithoutExtension(texPath);
                    EnsureTexture(current, texPath, "gloss", baseDirectory);
                }
                else if (span.StartsWith("map_d "))
                {
                    // Opacity map — treat as cavity since OBJ has no dedicated slot
                    string texPath = ExtractTexturePath(span[6..]);
                    current.CavityMapName = Path.GetFileNameWithoutExtension(texPath);
                    EnsureTexture(current, texPath, "cavity", baseDirectory);
                }
            }
        }
    }

    /// <summary>
    /// Extracts the texture file path from an MTL map statement, skipping any inline options (e.g., -bm 1.0).
    /// </summary>
    private static string ExtractTexturePath(ReadOnlySpan<char> value)
    {
        value = value.Trim();

        // Skip over inline options that start with '-' (e.g., -bm 1.0 filename.tga)
        while (value.Length > 0 && value[0] == '-')
        {
            // Skip the option flag
            int spaceAfterFlag = value.IndexOf(' ');
            if (spaceAfterFlag < 0)
            {
                break;
            }

            value = value[(spaceAfterFlag + 1)..].TrimStart();

            // Skip the option value (may be a number or filename)
            if (value.Length > 0 && value[0] != '-')
            {
                // Check if this looks like a numeric option value
                int nextSpace = value.IndexOf(' ');
                if (nextSpace >= 0)
                {
                    ReadOnlySpan<char> potentialNumber = value[..nextSpace];
                    if (float.TryParse(potentialNumber, NumberStyles.Float, CultureInfo.InvariantCulture, out _))
                    {
                        value = value[(nextSpace + 1)..].TrimStart();
                    }
                }
            }
        }

        return value.ToString();
    }

    private static bool TryParseColor(ReadOnlySpan<char> value, out Vector4 color)
    {
        color = default;
        string[] parts = value.ToString().Split(' ', StringSplitOptions.RemoveEmptyEntries);
        if (parts.Length < 3)
            return false;

        if (!float.TryParse(parts[0], NumberStyles.Float, CultureInfo.InvariantCulture, out float r) ||
            !float.TryParse(parts[1], NumberStyles.Float, CultureInfo.InvariantCulture, out float g) ||
            !float.TryParse(parts[2], NumberStyles.Float, CultureInfo.InvariantCulture, out float b))
        {
            return false;
        }

        color = new Vector4(r, g, b, 1.0f);
        return true;
    }

    private static bool TryParseScalar(ReadOnlySpan<char> value, out float scalar)
    {
        string trimmed = value.ToString().Trim();
        return float.TryParse(trimmed, NumberStyles.Float, CultureInfo.InvariantCulture, out scalar);
    }

    private static float ConvertPhongShininessToRoughness(float shininess)
    {
        float safeShininess = MathF.Max(shininess, 0.0f);
        return Math.Clamp(MathF.Sqrt(2.0f / (safeShininess + 2.0f)), 0.0f, 1.0f);
    }

    private static void EnsureTexture(Material material, string texturePath, string slot, string? baseDirectory)
    {
        string texName = Path.GetFileNameWithoutExtension(texturePath);
        string? resolvedPath = ResolveTexturePath(texturePath, baseDirectory);

        if (material.TryFindTexture(texName, StringComparison.OrdinalIgnoreCase, out Texture? texture))
        {
            texture.ResolvedFilePath = resolvedPath;
            return;
        }

        material.AddNode(new Texture(texturePath, slot)
        {
            ResolvedFilePath = resolvedPath,
        });
    }

    private static string? ResolveTexturePath(string texturePath, string? baseDirectory)
    {
        if (string.IsNullOrWhiteSpace(texturePath) || texturePath.StartsWith("data:", StringComparison.OrdinalIgnoreCase))
            return null;

        if (Path.IsPathRooted(texturePath))
            return Path.GetFullPath(texturePath);

        if (!string.IsNullOrWhiteSpace(baseDirectory))
            return Path.GetFullPath(Path.Combine(baseDirectory, texturePath));

        return null;
    }
}
