using System.Globalization;
using System.Numerics;
using RedFox.Graphics3D.Buffers;
using RedFox.Graphics3D.IO;

namespace RedFox.Graphics3D.WavefrontObj;

/// <summary>
/// Reads Wavefront OBJ (.obj) files and populates a <see cref="Scene"/> with the parsed geometry and materials.
/// Supports vertex positions (v), texture coordinates (vt), normals (vn), faces (f),
/// groups (g/o), material references (usemtl), and material library declarations (mtllib).
/// </summary>
public sealed class ObjReader
{
    private readonly Stream _stream;
    private readonly SceneTranslatorOptions _options;
    private readonly string _name;

    /// <summary>
    /// Initializes a new instance of the <see cref="ObjReader"/> class.
    /// </summary>
    /// <param name="stream">The input stream containing OBJ data.</param>
    /// <param name="name">The name used for the root model node.</param>
    /// <param name="options">Options that control how the scene data is read.</param>
    public ObjReader(Stream stream, string name, SceneTranslatorOptions options)
    {
        _stream = stream;
        _name = name;
        _options = options;
    }

    /// <summary>
    /// Reads the OBJ data from the stream and populates the provided scene.
    /// </summary>
    /// <param name="scene">The scene to populate with parsed geometry and materials.</param>
    /// <returns>A list of material library paths referenced by the OBJ file, or an empty list if none.</returns>
    public IReadOnlyList<string> Read(Scene scene)
    {
        // Global lists: OBJ indices are 1-based and reference into these.
        List<Vector3> globalPositions = [];
        List<Vector2> globalTexCoords = [];
        List<Vector3> globalNormals = [];

        // Current mesh state
        List<Vector3> meshPositions = [];
        List<Vector2> meshTexCoords = [];
        List<Vector3> meshNormals = [];
        List<int> meshFaceIndices = [];
        // Maps global vertex tuple (posIdx, texIdx, normalIdx) to local mesh vertex index.
        Dictionary<(int Pos, int Tex, int Normal), int> meshVertexMap = [];

        string currentGroupName = "default";
        string? currentMaterialName = null;
        Dictionary<string, Material> materialsByName = [];
        List<string> mtllibPaths = [];

        Model model = scene.RootNode.AddNode(new Model { Name = _name });
        Mesh? currentMesh = null;
        int meshIndex = 0;

        using StreamReader reader = new(_stream, leaveOpen: true);

        while (reader.ReadLine() is { } line)
        {
            ReadOnlySpan<char> span = line.AsSpan().Trim();
            if (span.IsEmpty || span[0] == '#')
            {
                continue;
            }

            if (span.StartsWith("vt "))
            {
                globalTexCoords.Add(ParseTexCoord(span[3..]));
            }
            else if (span.StartsWith("vn "))
            {
                globalNormals.Add(ParseVector3(span[3..]));
            }
            else if (span.StartsWith("v "))
            {
                globalPositions.Add(ParseVector3(span[2..]));
            }
            else if (span.StartsWith("f "))
            {
                if (currentMesh is null)
                {
                    currentMesh = CreateMesh(model, currentGroupName, ref meshIndex);
                }

                ParseFace(
                    span[2..],
                    globalPositions,
                    globalTexCoords,
                    globalNormals,
                    meshPositions,
                    meshTexCoords,
                    meshNormals,
                    meshFaceIndices,
                    meshVertexMap);
            }
            else if (span.StartsWith("g ") || span.StartsWith("o "))
            {
                // Flush current mesh if it has any faces
                if (currentMesh is not null && meshFaceIndices.Count > 0)
                {
                    FinalizeMesh(
                        currentMesh,
                        meshPositions,
                        meshTexCoords,
                        meshNormals,
                        meshFaceIndices,
                        currentMaterialName,
                        materialsByName);
                }

                currentGroupName = span[2..].Trim().ToString();
                currentMesh = CreateMesh(model, currentGroupName, ref meshIndex);
                meshPositions = [];
                meshTexCoords = [];
                meshNormals = [];
                meshFaceIndices = [];
                meshVertexMap = [];
            }
            else if (span.StartsWith("usemtl "))
            {
                ReadOnlySpan<char> matName = span[7..].Trim();

                if (currentMesh is not null && meshFaceIndices.Count > 0)
                {
                    FinalizeMesh(
                        currentMesh,
                        meshPositions,
                        meshTexCoords,
                        meshNormals,
                        meshFaceIndices,
                        currentMaterialName,
                        materialsByName);

                    currentMesh = CreateMesh(model, currentGroupName, ref meshIndex);
                    meshPositions = [];
                    meshTexCoords = [];
                    meshNormals = [];
                    meshFaceIndices = [];
                    meshVertexMap = [];
                }

                currentMaterialName = matName.ToString();
            }
            else if (span.StartsWith("mtllib "))
            {
                mtllibPaths.Add(span[7..].Trim().ToString());
            }
        }

        // Flush the last mesh
        if (currentMesh is not null && meshFaceIndices.Count > 0)
        {
            FinalizeMesh(
                currentMesh,
                meshPositions,
                meshTexCoords,
                meshNormals,
                meshFaceIndices,
                currentMaterialName,
                materialsByName);
        }

        if (_options.Get<bool>(ObjTranslator.MergeStaticMeshesOption))
        {
            MergeMeshesByMaterial(model);
        }

        // Load and attach referenced material libraries
        foreach (Material mat in materialsByName.Values)
        {
            model.AddNode(mat);
        }

        return mtllibPaths;
    }

    private static Mesh CreateMesh(Model model, string groupName, ref int meshIndex)
    {
        string meshName = string.IsNullOrWhiteSpace(groupName)
            ? $"mesh_{meshIndex}"
            : $"mesh_{groupName}";

        // Avoid duplicate names by appending the mesh index as a suffix.
        if (model.TryFindDescendant<Mesh>(meshName, out _))
        {
            meshName = $"{meshName}_{meshIndex}";
        }

        Mesh mesh = model.AddNode(new Mesh { Name = meshName });
        meshIndex++;
        return mesh;
    }

    private static void MergeMeshesByMaterial(Model model)
    {
        List<Mesh> sourceMeshes = [];
        foreach (Mesh mesh in model.EnumerateChildren<Mesh>())
        {
            sourceMeshes.Add(mesh);
        }

        if (sourceMeshes.Count <= 1)
        {
            return;
        }

        Dictionary<string, MeshMergeBucket> buckets = new(StringComparer.Ordinal);
        for (int meshIndex = 0; meshIndex < sourceMeshes.Count; meshIndex++)
        {
            Mesh mesh = sourceMeshes[meshIndex];
            if (mesh.Positions is not { ElementCount: > 0 } positions)
            {
                continue;
            }

            Material? material = mesh.Materials is { Count: > 0 } materials ? materials[0] : null;
            string materialKey = material?.Name ?? string.Empty;
            if (!buckets.TryGetValue(materialKey, out MeshMergeBucket? bucket))
            {
                bucket = new MeshMergeBucket(material);
                buckets.Add(materialKey, bucket);
            }

            AppendMesh(bucket, mesh, positions);
        }

        if (buckets.Count == 0)
        {
            return;
        }

        model.ClearNodes();
        int mergedMeshIndex = 0;
        foreach (MeshMergeBucket bucket in buckets.Values)
        {
            Mesh mergedMesh = model.AddNode(new Mesh { Name = CreateMergedMeshName(bucket, mergedMeshIndex++) });
            mergedMesh.Positions = CreateVector3Buffer(bucket.Positions);
            if (bucket.HasCompleteNormals)
            {
                mergedMesh.Normals = CreateVector3Buffer(bucket.Normals);
            }

            if (bucket.HasCompleteTexCoords)
            {
                mergedMesh.UVLayers = CreateVector2Buffer(bucket.TexCoords);
            }

            if (bucket.FaceIndices.Count > 0)
            {
                mergedMesh.FaceIndices = CreateIndexBuffer(bucket.FaceIndices, bucket.Positions.Count);
            }

            if (bucket.Material is not null)
            {
                mergedMesh.Materials = [bucket.Material];
            }
        }
    }

    private static void AppendMesh(MeshMergeBucket bucket, Mesh mesh, DataBuffer positions)
    {
        int baseVertex = bucket.Positions.Count;
        int vertexCount = positions.ElementCount;
        for (int vertexIndex = 0; vertexIndex < vertexCount; vertexIndex++)
        {
            bucket.Positions.Add(positions.GetVector3(vertexIndex, 0));
        }

        AppendOptionalNormals(bucket, mesh.Normals, vertexCount);
        AppendOptionalTexCoords(bucket, mesh.UVLayers, vertexCount);

        if (mesh.FaceIndices is { ElementCount: > 0 } faceIndices)
        {
            for (int index = 0; index < faceIndices.ElementCount; index++)
            {
                bucket.FaceIndices.Add(baseVertex + faceIndices.Get<int>(index, 0, 0));
            }

            return;
        }

        for (int vertexIndex = 0; vertexIndex < vertexCount; vertexIndex++)
        {
            bucket.FaceIndices.Add(baseVertex + vertexIndex);
        }
    }

    private static void AppendOptionalNormals(MeshMergeBucket bucket, DataBuffer? normals, int vertexCount)
    {
        if (!bucket.HasCompleteNormals)
        {
            return;
        }

        if (normals is null || normals.ElementCount != vertexCount)
        {
            bucket.Normals.Clear();
            bucket.HasCompleteNormals = false;
            return;
        }

        for (int vertexIndex = 0; vertexIndex < vertexCount; vertexIndex++)
        {
            bucket.Normals.Add(normals.GetVector3(vertexIndex, 0));
        }
    }

    private static void AppendOptionalTexCoords(MeshMergeBucket bucket, DataBuffer? texCoords, int vertexCount)
    {
        if (!bucket.HasCompleteTexCoords)
        {
            return;
        }

        if (texCoords is null || texCoords.ElementCount != vertexCount)
        {
            bucket.TexCoords.Clear();
            bucket.HasCompleteTexCoords = false;
            return;
        }

        for (int vertexIndex = 0; vertexIndex < vertexCount; vertexIndex++)
        {
            bucket.TexCoords.Add(texCoords.GetVector2(vertexIndex, 0));
        }
    }

    private static string CreateMergedMeshName(MeshMergeBucket bucket, int meshIndex)
    {
        if (bucket.Material is null || string.IsNullOrWhiteSpace(bucket.Material.Name))
        {
            return meshIndex == 0 ? "mesh_default" : $"mesh_default_{meshIndex}";
        }

        return $"mesh_{bucket.Material.Name}";
    }

    private static DataBuffer CreateIndexBuffer(List<int> faceIndices, int vertexCount)
    {
        if (vertexCount <= ushort.MaxValue)
        {
            ushort[] indices = new ushort[faceIndices.Count];
            for (int index = 0; index < faceIndices.Count; index++)
            {
                indices[index] = (ushort)faceIndices[index];
            }

            return new DataBuffer<ushort>(indices, 1, 1);
        }

        return new DataBuffer<int>([.. faceIndices], 1, 1);
    }

    private static void FinalizeMesh(
        Mesh mesh,
        List<Vector3> positions,
        List<Vector2> texCoords,
        List<Vector3> normals,
        List<int> faceIndices,
        string? materialName,
        Dictionary<string, Material> materialsByName)
    {
        int vertexCount = positions.Count;

        mesh.Positions = CreateVector3Buffer(positions);

        if (texCoords.Count == vertexCount)
        {
            mesh.UVLayers = CreateVector2Buffer(texCoords);
        }

        if (normals.Count == vertexCount)
        {
            mesh.Normals = CreateVector3Buffer(normals);
        }

        if (faceIndices.Count > 0)
        {
            if (vertexCount <= ushort.MaxValue)
            {
                ushort[] indices = new ushort[faceIndices.Count];
                for (int i = 0; i < faceIndices.Count; i++)
                {
                    indices[i] = (ushort)faceIndices[i];
                }

                mesh.FaceIndices = new DataBuffer<ushort>(indices, 1, 1);
            }
            else
            {
                int[] indices = [.. faceIndices];
                mesh.FaceIndices = new DataBuffer<int>(indices, 1, 1);
            }
        }

        if (materialName is not null)
        {
            if (!materialsByName.TryGetValue(materialName, out Material? material))
            {
                material = new Material(materialName);
                materialsByName[materialName] = material;
            }

            mesh.Materials = [material];
        }
    }

    private static DataBuffer<float> CreateVector3Buffer(List<Vector3> list)
    {
        float[] data = new float[list.Count * 3];
        for (int i = 0; i < list.Count; i++)
        {
            Vector3 v = list[i];
            int offset = i * 3;
            data[offset] = v.X;
            data[offset + 1] = v.Y;
            data[offset + 2] = v.Z;
        }

        return new DataBuffer<float>(data, 1, 3);
    }

    private static DataBuffer<float> CreateVector2Buffer(List<Vector2> list)
    {
        float[] data = new float[list.Count * 2];
        for (int i = 0; i < list.Count; i++)
        {
            Vector2 v = list[i];
            int offset = i * 2;
            data[offset] = v.X;
            data[offset + 1] = v.Y;
        }

        return new DataBuffer<float>(data, 1, 2);
    }

    private static void ParseFace(
        ReadOnlySpan<char> faceData,
        List<Vector3> globalPositions,
        List<Vector2> globalTexCoords,
        List<Vector3> globalNormals,
        List<Vector3> meshPositions,
        List<Vector2> meshTexCoords,
        List<Vector3> meshNormals,
        List<int> meshFaceIndices,
        Dictionary<(int Pos, int Tex, int Normal), int> meshVertexMap)
    {
        // Parse all face vertices first, then fan-triangulate if polygon.
        Span<int> faceVertexIndices = stackalloc int[64];
        int faceVertexCount = 0;

        bool hasTexCoords = globalTexCoords.Count > 0;
        bool hasNormals = globalNormals.Count > 0;

        ReadOnlySpan<char> remaining = faceData.Trim();

        while (!remaining.IsEmpty)
        {
            int spaceIndex = remaining.IndexOf(' ');
            ReadOnlySpan<char> vertexToken = spaceIndex >= 0 ? remaining[..spaceIndex] : remaining;
            remaining = spaceIndex >= 0 ? remaining[(spaceIndex + 1)..].TrimStart() : [];

            if (vertexToken.IsEmpty)
            {
                continue;
            }

            ParseFaceVertex(
                vertexToken,
                globalPositions,
                globalTexCoords,
                globalNormals,
                meshPositions,
                meshTexCoords,
                meshNormals,
                meshVertexMap,
                hasTexCoords,
                hasNormals,
                out int localIndex);

            faceVertexIndices[faceVertexCount++] = localIndex;
        }

        // Fan triangulation: vertex 0 is the pivot.
        for (int i = 1; i < faceVertexCount - 1; i++)
        {
            meshFaceIndices.Add(faceVertexIndices[0]);
            meshFaceIndices.Add(faceVertexIndices[i]);
            meshFaceIndices.Add(faceVertexIndices[i + 1]);
        }
    }

    private static void ParseFaceVertex(
        ReadOnlySpan<char> token,
        List<Vector3> globalPositions,
        List<Vector2> globalTexCoords,
        List<Vector3> globalNormals,
        List<Vector3> meshPositions,
        List<Vector2> meshTexCoords,
        List<Vector3> meshNormals,
        Dictionary<(int Pos, int Tex, int Normal), int> meshVertexMap,
        bool hasTexCoords,
        bool hasNormals,
        out int localIndex)
    {
        int posIdx = 0;
        int texIdx = 0;
        int normalIdx = 0;

        int firstSlash = token.IndexOf('/');
        if (firstSlash < 0)
        {
            // v only
            posIdx = ParseObjIndex(token, globalPositions.Count);
        }
        else
        {
            posIdx = ParseObjIndex(token[..firstSlash], globalPositions.Count);
            ReadOnlySpan<char> afterFirst = token[(firstSlash + 1)..];

            int secondSlash = afterFirst.IndexOf('/');
            if (secondSlash < 0)
            {
                // v/vt
                texIdx = ParseObjIndex(afterFirst, globalTexCoords.Count);
            }
            else
            {
                // v/vt/vn or v//vn
                if (secondSlash > 0)
                {
                    texIdx = ParseObjIndex(afterFirst[..secondSlash], globalTexCoords.Count);
                }

                ReadOnlySpan<char> normalPart = afterFirst[(secondSlash + 1)..];
                if (!normalPart.IsEmpty)
                {
                    normalIdx = ParseObjIndex(normalPart, globalNormals.Count);
                }
            }
        }

        var key = (posIdx, texIdx, normalIdx);
        if (meshVertexMap.TryGetValue(key, out localIndex))
        {
            return;
        }

        localIndex = meshPositions.Count;
        meshVertexMap[key] = localIndex;

        meshPositions.Add(globalPositions[posIdx]);

        if (hasTexCoords && texIdx >= 0 && texIdx < globalTexCoords.Count)
        {
            meshTexCoords.Add(globalTexCoords[texIdx]);
        }

        if (hasNormals && normalIdx >= 0 && normalIdx < globalNormals.Count)
        {
            meshNormals.Add(globalNormals[normalIdx]);
        }
    }

    /// <summary>
    /// Parses a 1-based OBJ index (possibly negative for relative indexing) and returns a 0-based index.
    /// </summary>
    private static int ParseObjIndex(ReadOnlySpan<char> span, int count)
    {
        int value = int.Parse(span, NumberStyles.Integer, CultureInfo.InvariantCulture);
        return value < 0 ? count + value : value - 1;
    }

    private static Vector3 ParseVector3(ReadOnlySpan<char> span)
    {
        span = span.Trim();

        int i0 = span.IndexOf(' ');
        float x = float.Parse(span[..i0], NumberStyles.Float, CultureInfo.InvariantCulture);
        span = span[(i0 + 1)..].TrimStart();

        int i1 = span.IndexOf(' ');
        float y;
        float z;

        if (i1 < 0)
        {
            y = float.Parse(span, NumberStyles.Float, CultureInfo.InvariantCulture);
            z = 0f;
        }
        else
        {
            y = float.Parse(span[..i1], NumberStyles.Float, CultureInfo.InvariantCulture);
            z = float.Parse(span[(i1 + 1)..].TrimStart(), NumberStyles.Float, CultureInfo.InvariantCulture);
        }

        return new Vector3(x, y, z);
    }

    private static Vector2 ParseTexCoord(ReadOnlySpan<char> span)
    {
        span = span.Trim();

        int i0 = span.IndexOf(' ');
        if (i0 < 0)
        {
            float uOnly = float.Parse(span, NumberStyles.Float, CultureInfo.InvariantCulture);
            return new Vector2(uOnly, 0f);
        }

        float u = float.Parse(span[..i0], NumberStyles.Float, CultureInfo.InvariantCulture);

        ReadOnlySpan<char> rest = span[(i0 + 1)..].TrimStart();
        int i1 = rest.IndexOf(' ');
        float v = i1 < 0
            ? float.Parse(rest, NumberStyles.Float, CultureInfo.InvariantCulture)
            : float.Parse(rest[..i1], NumberStyles.Float, CultureInfo.InvariantCulture);

        return new Vector2(u, v);
    }

    private sealed class MeshMergeBucket
    {
        public MeshMergeBucket(Material? material)
        {
            Material = material;
        }

        public Material? Material { get; }

        public List<Vector3> Positions { get; } = [];

        public List<Vector3> Normals { get; } = [];

        public List<Vector2> TexCoords { get; } = [];

        public List<int> FaceIndices { get; } = [];

        public bool HasCompleteNormals { get; set; } = true;

        public bool HasCompleteTexCoords { get; set; } = true;
    }
}
