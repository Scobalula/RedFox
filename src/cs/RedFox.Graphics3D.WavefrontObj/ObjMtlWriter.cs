using System.Globalization;

namespace RedFox.Graphics3D.WavefrontObj;

/// <summary>
/// Writes Wavefront MTL (.mtl) material library files from a collection of <see cref="Material"/> nodes.
/// Exports texture map references including diffuse (map_Kd), specular (map_Ks),
/// normal/bump (map_Bump), emissive (map_Ke), gloss (map_Ns), and cavity/opacity (map_d).
/// </summary>
public static class ObjMtlWriter
{
    /// <summary>
    /// Writes the specified materials to the MTL stream.
    /// </summary>
    /// <param name="stream">The output stream to write MTL data to.</param>
    /// <param name="materials">The materials to export.</param>
    public static void Write(Stream stream, IReadOnlyList<Material> materials)
    {
        using StreamWriter writer = new(stream, leaveOpen: true);
        writer.NewLine = "\n";

        writer.WriteLine("# Wavefront MTL exported by RedFox");
        writer.WriteLine();

        for (int i = 0; i < materials.Count; i++)
        {
            Material mat = materials[i];

            writer.WriteLine($"newmtl {mat.Name}");

            // Default illumination model and base colors
            writer.WriteLine(string.Create(CultureInfo.InvariantCulture, $"Ka {0.2f:G9} {0.2f:G9} {0.2f:G9}"));
            writer.WriteLine(string.Create(CultureInfo.InvariantCulture, $"Kd {0.8f:G9} {0.8f:G9} {0.8f:G9}"));
            writer.WriteLine(string.Create(CultureInfo.InvariantCulture, $"Ks {0.0f:G9} {0.0f:G9} {0.0f:G9}"));
            writer.WriteLine("illum 2");

            WriteTextureMap(writer, "map_Kd", mat, mat.DiffuseMapName);
            WriteTextureMap(writer, "map_Ks", mat, mat.SpecularMapName);
            WriteTextureMap(writer, "map_Bump", mat, mat.NormalMapName);
            WriteTextureMap(writer, "map_Ke", mat, mat.EmissiveMapName);
            WriteTextureMap(writer, "map_Ns", mat, mat.GlossMapName);
            WriteTextureMap(writer, "map_d", mat, mat.CavityMapName);

            if (i < materials.Count - 1)
            {
                writer.WriteLine();
            }
        }
    }

    private static void WriteTextureMap(StreamWriter writer, string mapType, Material material, string? mapName)
    {
        if (mapName is null)
        {
            return;
        }

        // Try to find the actual texture node to get the file path
        if (material.TryFindTexture(mapName, StringComparison.OrdinalIgnoreCase, out Texture? texture))
        {
            writer.WriteLine($"{mapType} {texture.FilePath}");
        }
        else
        {
            writer.WriteLine($"{mapType} {mapName}");
        }
    }
}
