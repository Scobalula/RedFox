using System.Globalization;
using System.Numerics;

namespace RedFox.Graphics3D.Formats.WavefrontObj;

/// <summary>
/// Reads Wavefront MTL (.mtl) material library files and populates <see cref="Material"/> nodes with texture paths and slot assignments.
/// </summary>
public static class ObjMtlReader
{
    /// <summary>
    /// Reads materials from the specified MTL stream and merges them into the provided material dictionary.
    /// </summary>
    /// <param name="stream">The input stream containing MTL data.</param>
    /// <param name="materials">A dictionary of materials keyed by name. New materials are added; existing ones are updated.</param>
    public static void Read(Stream stream, Dictionary<string, Material> materials) => Read(stream, materials, baseDirectory: null);

    /// <summary>
    /// Reads materials from the specified MTL stream and resolves relative texture paths against the supplied base directory when available.
    /// </summary>
    public static void Read(Stream stream, Dictionary<string, Material> materials, string? baseDirectory)
    {
        using StreamReader reader = new(stream, leaveOpen: true);

        Material? current = null;

        while (reader.ReadLine() is { } line)
        {
            ReadOnlySpan<char> span	= line.AsSpan().Trim();
            int comment				= span.IndexOf('#');
            if (comment >= 0)
                span = span[..comment].TrimEnd();
            if (span.IsEmpty)
            {
                continue;
            }

            int separator				= span.IndexOfAny(' ', '\t');
            ReadOnlySpan<char> command	= separator < 0 ? span : span[..separator];
            ReadOnlySpan<char> value	= separator < 0 ? [] : span[(separator + 1)..].TrimStart();

            if (command.SequenceEqual("newmtl"))
            {
                string matName = value.Trim().ToString();
                if (!materials.TryGetValue(matName, out current))
                {
                    current = new Material(matName);
                    materials[matName] = current;
                }
            }
            else if (current is not null)
            {
                if (command.SequenceEqual("Kd") && TryParseColor(value, out Vector4 diffuse))
                    current.DiffuseColor = diffuse;
                else if (command.SequenceEqual("Ke") && TryParseColor(value, out Vector4 emissive))
                    current.EmissiveColor = emissive;
                else if (command.SequenceEqual("map_Kd"))
                {
                    string texPath = ExtractTexturePath(value);
                    current.DiffuseMapName = "diffuse";
                    EnsureTexture(current, texPath, "diffuse", baseDirectory);
                }
                else if (command.SequenceEqual("Ks"))
                {
                    if (TryParseColor(value, out Vector4 specularColor))
                        current.SpecularColor = specularColor;
                }
                else if (command.SequenceEqual("Ns"))
                {
                    if (TryParseScalar(value, out float shininess))
                    {
                        current.Shininess = shininess;
                        current.Roughness = ConvertPhongShininessToRoughness(shininess);
                    }
                }
                else if (command.SequenceEqual("map_Ks"))
                {
                    string texPath = ExtractTexturePath(value);
                    current.SpecularMapName = "specular";
                    EnsureTexture(current, texPath, "specular", baseDirectory);
                }
                else if (command.SequenceEqual("map_Bump"))
                {
                    string texPath = ExtractTexturePath(value);
                    current.NormalMapName = "normal";
                    EnsureTexture(current, texPath, "normal", baseDirectory);
                }
                else if (command.SequenceEqual("bump"))
                {
                    string texPath = ExtractTexturePath(value);
                    current.NormalMapName = "bump_map";
                    EnsureTexture(current, texPath, "bump_map", baseDirectory);
                }
                else if (command.SequenceEqual("map_Ke"))
                {
                    string texPath = ExtractTexturePath(value);
                    current.EmissiveMapName = "emissive";
                    EnsureTexture(current, texPath, "emissive", baseDirectory);
                }
                else if (command.SequenceEqual("map_Ns"))
                {
                    string texPath = ExtractTexturePath(value);
                    current.GlossMapName = "gloss";
                    EnsureTexture(current, texPath, "gloss", baseDirectory);
                }
                else if (command.SequenceEqual("map_d"))
                {

                    string texPath = ExtractTexturePath(value);
                    current.CavityMapName = "cavity";
                    EnsureTexture(current, texPath, "cavity", baseDirectory);
                }
            }
        }
    }

    private static string ExtractTexturePath(ReadOnlySpan<char> value)
    {
        value = value.Trim();
        while (!value.IsEmpty && value[0] == '-')
        {
            int end = value.IndexOfAny(' ', '\t');
            if (end < 0)
                throw new InvalidDataException("Missing MTL texture option arguments.");
            ReadOnlySpan<char> option = value[..end];
            value = value[(end + 1)..].TrimStart();
            int argumentCount = option switch
            {
                "-o" or "-s" or "-t" => 3,
                "-mm" => 2,
                "-blendu" or "-blendv" or "-cc" or "-clamp" or "-texres" or "-bm" or "-imfchan" or "-type" => 1,
                _ => throw new NotSupportedException($"Unsupported MTL texture option '{option}'."),
            };
            bool vector = option is "-o" or "-s" or "-t";
            for (int i = 0; i < argumentCount; i++)
            {
                end = value.IndexOfAny(' ', '\t');
                ReadOnlySpan<char> argument = end < 0 ? value : value[..end];
                if (vector && !float.TryParse(argument, NumberStyles.Float, CultureInfo.InvariantCulture, out _))
                {
                    if (i == 0)
                        throw new InvalidDataException("Missing MTL vector option argument.");
                    break;
                }
                if (argument.IsEmpty || end < 0)
                    throw new InvalidDataException("Missing MTL texture path.");
                value = value[(end + 1)..].TrimStart();
            }
        }
        return value.Trim('"').ToString();
    }

    private static bool TryParseColor(ReadOnlySpan<char> value, out Vector4 color)
    {
        color = default;
        string[] parts = value.ToString().Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries);
        if (parts.Length < 3)
            return false;

        if (!float.TryParse(parts[0], NumberStyles.Float, CultureInfo.InvariantCulture, out float r) || !float.TryParse(parts[1], NumberStyles.Float, CultureInfo.InvariantCulture, out float g) || !float.TryParse(parts[2], NumberStyles.Float, CultureInfo.InvariantCulture, out float b))
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
        string? resolvedPath = FilePathResolver.ResolveReferencePath(texturePath, baseDirectory);

        if (material.TryGetTexture(slot, out Texture? existing))
        {
            existing.ResolvedFilePath = resolvedPath;
            return;
        }

        var texture = new Texture(texturePath) { ResolvedFilePath = resolvedPath };
        material.AddNode(texture);
        material.Connect(slot, texture);
    }

}
