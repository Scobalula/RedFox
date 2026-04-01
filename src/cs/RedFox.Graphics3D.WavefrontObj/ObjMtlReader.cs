using System.Globalization;

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
