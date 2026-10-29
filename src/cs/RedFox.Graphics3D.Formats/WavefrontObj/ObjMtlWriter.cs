using System.Globalization;
using System.Numerics;

namespace RedFox.Graphics3D.Formats.WavefrontObj;

/// <summary>
/// Writes Wavefront MTL (.mtl) material library files from a collection of <see cref="Material"/> nodes.
/// </summary>
public static class ObjMtlWriter
{
    /// <summary>
    /// Writes the specified materials to the MTL stream.
    /// </summary>
    /// <param name="stream">The output stream to write MTL data to.</param>
    /// <param name="materials">The materials to export.</param>
    public static void Write(Stream stream, IReadOnlyList<Material> materials) => Write(stream, materials, null);

    /// <summary>
    /// Writes the specified materials to the MTL stream.
    /// </summary>
    /// <param name="stream">The output stream to write MTL data to.</param>
    /// <param name="materials">The materials to export.</param>
    /// <param name="targetDirectory">The directory the MTL file is written to, used to relativize texture paths.</param>
    public static void Write(Stream stream, IReadOnlyList<Material> materials, string? targetDirectory)
    {
        using StreamWriter writer = new(stream, leaveOpen: true);
        writer.NewLine = "\n";

        writer.WriteLine("# Wavefront MTL exported by RedFox");
        writer.WriteLine();

        HashSet<string> names = new(StringComparer.Ordinal) { "off" };
        for (int i = 0; i < materials.Count; i++)
        {
            Material mat = materials[i];

            writer.WriteLine($"newmtl {GetMaterialName(mat, names)}");

            Vector4 diffuse		= mat.DiffuseColor ?? new Vector4(0.8f, 0.8f, 0.8f, 1f);
            Vector4 specular	= mat.SpecularColor ?? Vector4.Zero;
            writer.WriteLine(string.Create(CultureInfo.InvariantCulture, $"Kd {diffuse.X:G9} {diffuse.Y:G9} {diffuse.Z:G9}"));
            writer.WriteLine(string.Create(CultureInfo.InvariantCulture, $"Ks {specular.X:G9} {specular.Y:G9} {specular.Z:G9}"));
            if (mat.EmissiveColor is Vector4 emissive)
                writer.WriteLine(string.Create(CultureInfo.InvariantCulture, $"Ke {emissive.X:G9} {emissive.Y:G9} {emissive.Z:G9}"));
            if (mat.Shininess is float shininess)
                writer.WriteLine(string.Create(CultureInfo.InvariantCulture, $"Ns {shininess:G9}"));
            writer.WriteLine("illum 2");

            WriteTextureMap(writer, "map_Kd", mat, mat.DiffuseMapName, targetDirectory);
            WriteTextureMap(writer, "map_Ks", mat, mat.SpecularMapName, targetDirectory);
            WriteTextureMap(writer, "map_Bump", mat, mat.NormalMapName, targetDirectory);
            WriteTextureMap(writer, "map_Ke", mat, mat.EmissiveMapName, targetDirectory);
            WriteTextureMap(writer, "map_Ns", mat, mat.GlossMapName, targetDirectory);
            WriteTextureMap(writer, "map_d", mat, mat.CavityMapName, targetDirectory);

            if (i < materials.Count - 1)
            {
                writer.WriteLine();
            }
        }
    }

    internal static string GetMaterialName(Material material, HashSet<string> names)
    {
        string name = material.Name;
        for (int suffix = 1; !names.Add(name); suffix++)
            name = $"{material.Name}_{suffix}";
        return name;
    }

    private static void WriteTextureMap(StreamWriter writer, string mapType, Material material, string? slotKey, string? targetDirectory)
    {
        if (slotKey is null)
        {
            return;
        }

        if (material.TryGetTexture(slotKey, out Texture? texture))
        {
            writer.WriteLine($"{mapType} {texture.GetPortableFilePath(targetDirectory)}");
        }
        else
        {
            writer.WriteLine($"{mapType} {slotKey}");
        }
    }
}
