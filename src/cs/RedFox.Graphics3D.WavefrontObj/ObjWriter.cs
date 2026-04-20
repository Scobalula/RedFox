using System.Globalization;
using System.Numerics;
using RedFox.Graphics3D.IO;

namespace RedFox.Graphics3D.WavefrontObj;

/// <summary>
/// Writes scene meshes and materials to the Wavefront OBJ (.obj) text format.
/// Supports vertex positions (v), texture coordinates (vt), normals (vn), indexed faces (f),
/// object/group names (o), material references (usemtl), and material library declarations (mtllib).
/// </summary>
public sealed class ObjWriter
{
    private readonly Stream _stream;
    private readonly string _name;
    private readonly SceneTranslatorOptions _options;

    /// <summary>
    /// Initializes a new instance of the <see cref="ObjWriter"/> class.
    /// </summary>
    /// <param name="stream">The output stream to write OBJ data to.</param>
    /// <param name="name">The scene or file name used in the output header.</param>
    /// <param name="options">Options that control how the scene data is written.</param>
    public ObjWriter(Stream stream, string name, SceneTranslatorOptions options)
    {
        _stream = stream;
        _name = name;
        _options = options;
    }

    /// <summary>
    /// Writes all meshes and material references from the scene to the OBJ stream.
    /// Returns the set of materials referenced by exported meshes so that an accompanying
    /// MTL file can be written.
    /// </summary>
    /// <param name="scene">The scene to export.</param>
    /// <param name="mtlFileName">
    /// The filename (not path) of the associated material library,
    /// or <see langword="null"/> to omit the mtllib directive.
    /// </param>
    /// <returns>An ordered collection of materials referenced by the exported meshes.</returns>
    public IReadOnlyList<Material> Write(Scene scene, string? mtlFileName)
        => Write(new SceneTranslationSelection(scene, SceneNodeFlags.None), mtlFileName);

    /// <summary>
    /// Writes all meshes and material references from the selected scene view to the OBJ stream.
    /// Returns the set of materials referenced by exported meshes so that an accompanying
    /// MTL file can be written.
    /// </summary>
    /// <param name="selection">The filtered scene selection to export.</param>
    /// <param name="mtlFileName">
    /// The filename (not path) of the associated material library,
    /// or <see langword="null"/> to omit the mtllib directive.
    /// </param>
    /// <returns>An ordered collection of materials referenced by the exported meshes.</returns>
    public IReadOnlyList<Material> Write(SceneTranslationSelection selection, string? mtlFileName)
    {
        ArgumentNullException.ThrowIfNull(selection);

        using StreamWriter writer = new(_stream, leaveOpen: true);
        writer.NewLine = "\n";

        writer.WriteLine($"# Wavefront OBJ exported by RedFox");
        writer.WriteLine($"# Name: {_name}");
        writer.WriteLine();

        if (mtlFileName is not null)
        {
            writer.WriteLine($"mtllib {mtlFileName}");
            writer.WriteLine();
        }

        Mesh[] meshes = selection.GetDescendants<Mesh>();

        // Track global running offsets for v/vt/vn (OBJ uses 1-based global indices).
        int positionOffset = 0;
        int texCoordOffset = 0;
        int normalOffset = 0;

        List<Material> referencedMaterials = [];
        HashSet<string> seenMaterialNames = new(StringComparer.Ordinal);

        foreach (Mesh mesh in meshes)
        {
            writer.WriteLine($"o {mesh.Name}");

            int vertexCount = mesh.VertexCount;
            bool hasUVs = mesh.UVLayers is not null && mesh.UVLayerCount > 0;
            bool hasNormals = mesh.Normals is not null;
            bool useRaw = _options.WriteRawVertices;

            // Write positions
            for (int i = 0; i < vertexCount; i++)
            {
                Vector3 pos = mesh.GetVertexPosition(i, useRaw);
                writer.WriteLine(string.Create(CultureInfo.InvariantCulture, $"v {pos.X:G9} {pos.Y:G9} {pos.Z:G9}"));
            }

            // Write texture coordinates
            if (hasUVs)
            {
                for (int i = 0; i < vertexCount; i++)
                {
                    Vector2 uv = mesh.UVLayers!.GetVector2(i, 0);
                    writer.WriteLine(string.Create(CultureInfo.InvariantCulture, $"vt {uv.X:G9} {uv.Y:G9}"));
                }
            }

            // Write normals
            if (hasNormals)
            {
                for (int i = 0; i < vertexCount; i++)
                {
                    Vector3 normal = mesh.GetVertexNormal(i, useRaw);
                    writer.WriteLine(string.Create(CultureInfo.InvariantCulture, $"vn {normal.X:G9} {normal.Y:G9} {normal.Z:G9}"));
                }
            }

            // Write material reference
            if (mesh.Materials is { Count: > 0 })
            {
                Material mat = mesh.Materials[0];
                if (!selection.Includes(mat))
                {
                    throw new InvalidDataException(
                        $"Cannot write OBJ: mesh '{mesh.Name}' references material '{mat.Name}' that is not included in the export selection.");
                }

                writer.WriteLine($"usemtl {mat.Name}");

                if (seenMaterialNames.Add(mat.Name))
                {
                    referencedMaterials.Add(mat);
                }
            }

            // Write faces (1-based with global offsets)
            WriteFaces(writer, mesh, positionOffset, texCoordOffset, normalOffset, hasUVs, hasNormals);

            positionOffset += vertexCount;
            if (hasUVs)
            {
                texCoordOffset += vertexCount;
            }
            if (hasNormals)
            {
                normalOffset += vertexCount;
            }

            writer.WriteLine();
        }

        return referencedMaterials;
    }

    private static void WriteFaces(
        StreamWriter writer,
        Mesh mesh,
        int positionOffset,
        int texCoordOffset,
        int normalOffset,
        bool hasUVs,
        bool hasNormals)
    {
        if (mesh.FaceIndices is null)
        {
            return;
        }

        int faceCount = mesh.FaceCount;

        for (int face = 0; face < faceCount; face++)
        {
            int i0 = mesh.FaceIndices.Get<int>(face * 3, 0, 0);
            int i1 = mesh.FaceIndices.Get<int>(face * 3 + 1, 0, 0);
            int i2 = mesh.FaceIndices.Get<int>(face * 3 + 2, 0, 0);

            writer.Write("f ");
            WriteFaceVertex(writer, i0, positionOffset, texCoordOffset, normalOffset, hasUVs, hasNormals);
            writer.Write(' ');
            WriteFaceVertex(writer, i1, positionOffset, texCoordOffset, normalOffset, hasUVs, hasNormals);
            writer.Write(' ');
            WriteFaceVertex(writer, i2, positionOffset, texCoordOffset, normalOffset, hasUVs, hasNormals);
            writer.WriteLine();
        }
    }

    private static void WriteFaceVertex(
        StreamWriter writer,
        int localIndex,
        int positionOffset,
        int texCoordOffset,
        int normalOffset,
        bool hasUVs,
        bool hasNormals)
    {
        int p = localIndex + positionOffset + 1; // 1-based

        if (hasUVs && hasNormals)
        {
            int t = localIndex + texCoordOffset + 1;
            int n = localIndex + normalOffset + 1;
            writer.Write(string.Create(CultureInfo.InvariantCulture, $"{p}/{t}/{n}"));
        }
        else if (hasUVs)
        {
            int t = localIndex + texCoordOffset + 1;
            writer.Write(string.Create(CultureInfo.InvariantCulture, $"{p}/{t}"));
        }
        else if (hasNormals)
        {
            int n = localIndex + normalOffset + 1;
            writer.Write(string.Create(CultureInfo.InvariantCulture, $"{p}//{n}"));
        }
        else
        {
            writer.Write(string.Create(CultureInfo.InvariantCulture, $"{p}"));
        }
    }
}
