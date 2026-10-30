using System.Globalization;
using System.Numerics;
using RedFox.Graphics3D.IO;

namespace RedFox.Graphics3D.Formats.WavefrontObj;

/// <summary>
/// Writes scene meshes and materials to the Wavefront OBJ (.obj) text format.
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
    /// </summary>
    /// <param name="scene">The scene to export.</param>
    /// <param name="mtlFileName">
    /// The filename (not path) of the associated material library,
    /// or <see langword="null"/> to omit the mtllib directive.
    /// </param>
    /// <returns>An ordered collection of materials referenced by the exported meshes.</returns>
    public IReadOnlyList<Material> Write(Scene scene, string? mtlFileName) => Write(scene, mtlFileName, CancellationToken.None);

    /// <summary>
    /// Writes data with cancellation support.
    /// </summary>
    /// <param name="scene">The scene to write.</param>
    /// <param name="mtlFileName">The material library filename, or null to omit it.</param>
    /// <param name="token">The cancellation token.</param>
    public IReadOnlyList<Material> Write(Scene scene, string? mtlFileName, CancellationToken token) => Write(new SceneTranslationSelection(scene, SceneNodeFlags.None), mtlFileName, token);

    /// <summary>
    /// Writes all meshes and material references from the selected scene view to the OBJ stream.
    /// </summary>
    /// <param name="selection">The filtered scene selection to export.</param>
    /// <param name="mtlFileName">
    /// The filename (not path) of the associated material library,
    /// or <see langword="null"/> to omit the mtllib directive.
    /// </param>
    /// <returns>An ordered collection of materials referenced by the exported meshes.</returns>
    public IReadOnlyList<Material> Write(SceneTranslationSelection selection, string? mtlFileName) => Write(selection, mtlFileName, CancellationToken.None);

    /// <summary>
    /// Writes data with cancellation support.
    /// </summary>
    /// <param name="selection">The selected scene data to write.</param>
    /// <param name="mtlFileName">The material library filename, or null to omit it.</param>
    /// <param name="token">The cancellation token.</param>
    public IReadOnlyList<Material> Write(SceneTranslationSelection selection, string? mtlFileName, CancellationToken token)
    {
        token.ThrowIfCancellationRequested();
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

        int positionOffset	= 0;
        int texCoordOffset	= 0;
        int normalOffset	= 0;

        List<Material> referencedMaterials	= [];
        HashSet<string> usedMaterialNames	= new(StringComparer.Ordinal) { "off" };
        Dictionary<Material, string> materialNames = [];

        foreach (Mesh mesh in meshes)
        {
            token.ThrowIfCancellationRequested();
            writer.WriteLine($"o {mesh.Name}");

            int vertexCount						= mesh.VertexCount;
            bool hasUVs							= mesh.UVLayers is not null && mesh.UVLayerCount > 0;
            bool hasNormals						= mesh.Normals is not null;
            bool useRaw							= _options.WriteRawVertices;
            Matrix4x4[] skinTransforms			= [];
            Matrix4x4[] skinNormalTransforms	= [];
            if (!useRaw && mesh.Skin is not null)
            {
                skinTransforms = new Matrix4x4[mesh.Skin.Bones.Count];
                mesh.CopySkinTransforms(skinTransforms);
                skinNormalTransforms = new Matrix4x4[skinTransforms.Length];
                for (int i = 0; i < skinTransforms.Length; i++)
                    skinNormalTransforms[i] = Matrix4x4.Invert(skinTransforms[i], out Matrix4x4 inverse) ? Matrix4x4.Transpose(inverse) : skinTransforms[i];
            }

            for (int i = 0; i < vertexCount; i++)
            {
                if ((i & 1023) == 0)
                    token.ThrowIfCancellationRequested();
                Vector3 pos = useRaw ? mesh.GetVertexPosition(i, raw: true) : mesh.GetVertexPosition(i, skinTransforms);
                writer.WriteLine(string.Create(CultureInfo.InvariantCulture, $"v {pos.X:G9} {pos.Y:G9} {pos.Z:G9}"));
            }

            if (hasUVs && mesh.UVLayers is { } uvLayers)
            {
                for (int i = 0; i < vertexCount; i++)
                {
                    if ((i & 1023) == 0)
                        token.ThrowIfCancellationRequested();
                    Vector2 uv = uvLayers.GetVector2(i, 0);
                    writer.WriteLine(string.Create(CultureInfo.InvariantCulture, $"vt {uv.X:G9} {1f - uv.Y:G9}"));
                }
            }

            if (hasNormals)
            {
                for (int i = 0; i < vertexCount; i++)
                {
                    if ((i & 1023) == 0)
                        token.ThrowIfCancellationRequested();
                    Vector3 normal = useRaw ? mesh.GetVertexNormal(i, raw: true) : mesh.GetVertexNormal(i, skinTransforms, skinNormalTransforms);
                    writer.WriteLine(string.Create(CultureInfo.InvariantCulture, $"vn {normal.X:G9} {normal.Y:G9} {normal.Z:G9}"));
                }
            }

            List<(int StartIndex, int IndexCount)>? ranges = mesh.MaterialIndexRanges;
            if (ranges is { Count: > 0 } && ranges.Count != mesh.Materials?.Count)
                throw new InvalidDataException("Inconsistent OBJ material ranges.");
            int rangeCount		= ranges is { Count: > 0 } ? ranges.Count : 1;
            int[]? rangeOrder	= null;
            if (ranges is { Count: > 1 })
            {
                rangeOrder = Enumerable.Range(0, ranges.Count).ToArray();
                Array.Sort(rangeOrder, (a, b) => ranges[a].StartIndex.CompareTo(ranges[b].StartIndex));
            }
            int cursor = 0;
            for (int i = 0; i < rangeCount; i++)
            {
                int rangeIndex = rangeOrder?[i] ?? i;
                (int start, int count) = ranges is { Count: > 0 } ? ranges[rangeIndex] : (0, mesh.IndexCount);
                if (start < cursor)
                    throw new InvalidDataException("Overlapping OBJ material ranges.");
                if (start > cursor)
                {
                    writer.WriteLine("usemtl off");
                    WriteFaces(writer, mesh, positionOffset, texCoordOffset, normalOffset, hasUVs, hasNormals, cursor, start - cursor);
                }
                Material? material = mesh.Materials is { Count: > 0 } ? mesh.Materials[rangeIndex] : null;
                if (material is null)
                {
                    writer.WriteLine("usemtl off");
                }
                else
                {
                    if (!selection.Includes(material))
                        throw new InvalidDataException($"Cannot write OBJ: mesh '{mesh.Name}' references material '{material.Name}' that is not included in the export selection.");
                    if (!materialNames.TryGetValue(material, out string? materialName))
                    {
                        materialName = ObjMtlWriter.GetMaterialName(material, usedMaterialNames);
                        materialNames.Add(material, materialName);
                        referencedMaterials.Add(material);
                    }
                    writer.WriteLine($"usemtl {materialName}");
                }
                WriteFaces(writer, mesh, positionOffset, texCoordOffset, normalOffset, hasUVs, hasNormals, start, count);
                cursor = checked(start + count);
            }
            if (cursor < mesh.IndexCount)
            {
                writer.WriteLine("usemtl off");
                WriteFaces(writer, mesh, positionOffset, texCoordOffset, normalOffset, hasUVs, hasNormals, cursor, mesh.IndexCount - cursor);
            }

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

        writer.Flush();
        token.ThrowIfCancellationRequested();
        return referencedMaterials;
    }

    private static void WriteFaces(StreamWriter writer, Mesh mesh, int positionOffset, int texCoordOffset, int normalOffset, bool hasUVs, bool hasNormals, int start, int count)
    {
        if (mesh.FaceIndices is null)
        {
            return;
        }

        if (start < 0 || count < 0 || start % 3 != 0 || count % 3 != 0 || (long)start + count > mesh.IndexCount)
            throw new InvalidDataException("Invalid OBJ triangle range.");
        int faceCount = count / 3;

        for (int face = 0; face < faceCount; face++)
        {
            int i0= mesh.FaceIndices.Get<int>(start + face * 3, 0, 0);
            int i1= mesh.FaceIndices.Get<int>(start + face * 3 + 1, 0, 0);
            int i2= mesh.FaceIndices.Get<int>(start + face * 3 + 2, 0, 0);

            writer.Write("f ");
            WriteFaceVertex(writer, i0, positionOffset, texCoordOffset, normalOffset, hasUVs, hasNormals);
            writer.Write(' ');
            WriteFaceVertex(writer, i1, positionOffset, texCoordOffset, normalOffset, hasUVs, hasNormals);
            writer.Write(' ');
            WriteFaceVertex(writer, i2, positionOffset, texCoordOffset, normalOffset, hasUVs, hasNormals);
            writer.WriteLine();
        }
    }

    private static void WriteFaceVertex(StreamWriter writer, int localIndex, int positionOffset, int texCoordOffset, int normalOffset, bool hasUVs, bool hasNormals)
    {
        int p = localIndex + positionOffset + 1;

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
