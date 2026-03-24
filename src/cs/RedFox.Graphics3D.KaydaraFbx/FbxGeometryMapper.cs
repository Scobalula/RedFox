using System.Buffers;
using System.Numerics;
using RedFox.Graphics3D.Buffers;

namespace RedFox.Graphics3D.KaydaraFbx;

/// <summary>
/// Maps FBX geometry nodes to and from RedFox mesh data.
/// </summary>
public static class FbxGeometryMapper
{
    /// <summary>
    /// Imports geometry data from an FBX geometry node into a mesh.
    /// </summary>
    /// <param name="mesh">The destination mesh.</param>
    /// <param name="geometry">The source FBX geometry node.</param>
    /// <returns>Per-triangle material indices when present; otherwise an empty array.</returns>
    public static int[] ImportGeometry(Mesh mesh, FbxNode geometry)
    {
        double[] vertices = FbxSceneMapper.GetNodeArray<double>(geometry, "Vertices");
        int[] polygonVertexIndices = FbxSceneMapper.GetNodeArray<int>(geometry, "PolygonVertexIndex");
        if (vertices.Length == 0 || polygonVertexIndices.Length == 0)
        {
            return [];
        }

        int vertexCount = vertices.Length / 3;
        float[] positionData = new float[vertexCount * 3];
        for (int i = 0; i < positionData.Length; i++)
        {
            positionData[i] = (float)vertices[i];
        }

        MeshTriangulationResult triangulation = MeshTriangulator.TriangulateFbxPolygonVertexIndices(polygonVertexIndices);

        mesh.Positions = new DataBuffer<float>(positionData, 1, 3);
        mesh.FaceIndices = new DataBuffer<int>(triangulation.TriangleIndices, 1, 1);

        ImportNormals(mesh, geometry, polygonVertexIndices, vertexCount);
        ImportUvLayers(mesh, geometry, polygonVertexIndices, vertexCount);
        return ImportPerTriangleMaterialIndices(geometry, triangulation.TrianglesPerFace);
    }

    /// <summary>
    /// Exports a mesh into an FBX geometry node.
    /// </summary>
    /// <param name="id">The FBX object identifier.</param>
    /// <param name="mesh">The source mesh.</param>
    /// <returns>The generated FBX geometry node.</returns>
    public static FbxNode ExportGeometry(long id, Mesh mesh)
    {
        FbxNode geometry = new("Geometry");
        geometry.Properties.Add(new FbxProperty('L', id));
        geometry.Properties.Add(new FbxProperty('S', mesh.Name + "\0\u0001Geometry"));
        geometry.Properties.Add(new FbxProperty('S', "Mesh"));

        geometry.Children.Add(new FbxNode("Vertices")
        {
            Properties = { new FbxProperty('d', ExtractPositions(mesh)) },
        });

        geometry.Children.Add(new FbxNode("PolygonVertexIndex")
        {
            Properties = { new FbxProperty('i', ExtractPolygonVertexIndex(mesh)) },
        });

        int[] edges = ExtractEdges(mesh);
        if (edges.Length > 0)
        {
            geometry.Children.Add(new FbxNode("Edges")
            {
                Properties = { new FbxProperty('i', edges) },
            });
        }

        geometry.Children.Add(new FbxNode("GeometryVersion") { Properties = { new FbxProperty('I', 124) } });

        if (mesh.Normals is not null)
        {
            FbxNode layerNormals = new("LayerElementNormal");
            layerNormals.Properties.Add(new FbxProperty('I', 0));
            double[] normals = ExtractNormals(mesh);
            layerNormals.Children.Add(new FbxNode("Version") { Properties = { new FbxProperty('I', 102) } });
            layerNormals.Children.Add(new FbxNode("Name") { Properties = { new FbxProperty('S', string.Empty) } });
            layerNormals.Children.Add(new FbxNode("MappingInformationType") { Properties = { new FbxProperty('S', "ByVertice") } });
            layerNormals.Children.Add(new FbxNode("ReferenceInformationType") { Properties = { new FbxProperty('S', "Direct") } });
            layerNormals.Children.Add(new FbxNode("Normals") { Properties = { new FbxProperty('d', normals) } });
            layerNormals.Children.Add(new FbxNode("NormalsW") { Properties = { new FbxProperty('d', CreateFilledArray(normals.Length / 3, 1.0)) } });
            geometry.Children.Add(layerNormals);
        }

        if (mesh.UVLayers is not null)
        {
            for (int layerIndex = 0; layerIndex < mesh.UVLayerCount; layerIndex++)
            {
                FbxNode layerUv = new("LayerElementUV");
                layerUv.Properties.Add(new FbxProperty('I', layerIndex));
                double[] uvLayer = ExtractUvLayer(mesh, layerIndex);
                layerUv.Children.Add(new FbxNode("Version") { Properties = { new FbxProperty('I', 101) } });
                layerUv.Children.Add(new FbxNode("Name") { Properties = { new FbxProperty('S', layerIndex == 0 ? "map1" : $"UVChannel_{layerIndex}") } });
                layerUv.Children.Add(new FbxNode("MappingInformationType") { Properties = { new FbxProperty('S', "ByPolygonVertex") } });
                layerUv.Children.Add(new FbxNode("ReferenceInformationType") { Properties = { new FbxProperty('S', "IndexToDirect") } });
                layerUv.Children.Add(new FbxNode("UV") { Properties = { new FbxProperty('d', uvLayer) } });
                layerUv.Children.Add(new FbxNode("UVIndex") { Properties = { new FbxProperty('i', ExtractPolygonVertexUvIndices(mesh)) } });
                geometry.Children.Add(layerUv);
            }
        }

        if (mesh.Materials is { Count: > 0 })
        {
            FbxNode layerMaterial = new("LayerElementMaterial");
            layerMaterial.Properties.Add(new FbxProperty('I', 0));
            layerMaterial.Children.Add(new FbxNode("Version") { Properties = { new FbxProperty('I', 101) } });
            layerMaterial.Children.Add(new FbxNode("Name") { Properties = { new FbxProperty('S', string.Empty) } });
            layerMaterial.Children.Add(new FbxNode("MappingInformationType") { Properties = { new FbxProperty('S', "AllSame") } });
            layerMaterial.Children.Add(new FbxNode("ReferenceInformationType") { Properties = { new FbxProperty('S', "IndexToDirect") } });
            layerMaterial.Children.Add(new FbxNode("Materials") { Properties = { new FbxProperty('i', new[] { 0 }) } });
            geometry.Children.Add(layerMaterial);
        }

        FbxNode layer = new("Layer");
        layer.Properties.Add(new FbxProperty('I', 0));
        layer.Children.Add(new FbxNode("Version") { Properties = { new FbxProperty('I', 100) } });
        if (mesh.Normals is not null)
        {
            FbxNode normalsElement = new("LayerElement");
            normalsElement.Children.Add(new FbxNode("Type") { Properties = { new FbxProperty('S', "LayerElementNormal") } });
            normalsElement.Children.Add(new FbxNode("TypedIndex") { Properties = { new FbxProperty('I', 0) } });
            layer.Children.Add(normalsElement);
        }

        if (mesh.UVLayers is not null)
        {
            FbxNode uvElement = new("LayerElement");
            uvElement.Children.Add(new FbxNode("Type") { Properties = { new FbxProperty('S', "LayerElementUV") } });
            uvElement.Children.Add(new FbxNode("TypedIndex") { Properties = { new FbxProperty('I', 0) } });
            layer.Children.Add(uvElement);
        }

        if (mesh.Materials is { Count: > 0 })
        {
            FbxNode materialElement = new("LayerElement");
            materialElement.Children.Add(new FbxNode("Type") { Properties = { new FbxProperty('S', "LayerElementMaterial") } });
            materialElement.Children.Add(new FbxNode("TypedIndex") { Properties = { new FbxProperty('I', 0) } });
            layer.Children.Add(materialElement);
        }

        geometry.Children.Add(layer);

        return geometry;
    }

    /// <summary>
    /// Imports normal vectors from an FBX geometry node into the target mesh.
    /// </summary>
    /// <param name="mesh">The destination mesh.</param>
    /// <param name="geometry">The source FBX geometry node.</param>
    /// <param name="polygonVertexIndices">The raw polygon vertex index array.</param>
    /// <param name="vertexCount">The number of vertices in the mesh.</param>
    public static void ImportNormals(Mesh mesh, FbxNode geometry, int[] polygonVertexIndices, int vertexCount)
    {
        FbxNode? normalNode = geometry.Children.FirstOrDefault(child => string.Equals(child.Name, "LayerElementNormal", StringComparison.Ordinal));
        if (normalNode is null)
        {
            return;
        }

        double[] normals = FbxSceneMapper.GetNodeArray<double>(normalNode, "Normals");
        if (normals.Length == 0)
        {
            return;
        }

        string mapping = FbxSceneMapper.GetNodeString(normalNode, "MappingInformationType") ?? "ByPolygonVertex";
        string reference = FbxSceneMapper.GetNodeString(normalNode, "ReferenceInformationType") ?? "Direct";
        int[] normalIndices = FbxSceneMapper.GetNodeArray<int>(normalNode, "NormalsIndex");

        Vector3[] sums = new Vector3[vertexCount];
        int[] counts = new int[vertexCount];

        int polygonVertexCounter = 0;
        for (int i = 0; i < polygonVertexIndices.Length; i++)
        {
            int encodedVertex = polygonVertexIndices[i];
            int vertexIndex = encodedVertex < 0 ? -encodedVertex - 1 : encodedVertex;

            int mappedIndex = ResolveMappedIndex(mapping, reference, polygonVertexCounter, vertexIndex, normalIndices);
            Vector3 normal = ReadVector3(normals, mappedIndex);
            if (normal.LengthSquared() > 0f)
            {
                normal = Vector3.Normalize(normal);
            }

            sums[vertexIndex] += normal;
            counts[vertexIndex]++;
            polygonVertexCounter++;
        }

        float[] output = new float[vertexCount * 3];
        for (int vertexIndex = 0; vertexIndex < vertexCount; vertexIndex++)
        {
            Vector3 normal = counts[vertexIndex] == 0 ? Vector3.UnitZ : Vector3.Normalize(sums[vertexIndex] / counts[vertexIndex]);
            int baseOffset = vertexIndex * 3;
            output[baseOffset] = normal.X;
            output[baseOffset + 1] = normal.Y;
            output[baseOffset + 2] = normal.Z;
        }

        mesh.Normals = new DataBuffer<float>(output, 1, 3);
    }

    /// <summary>
    /// Imports UV layers from an FBX geometry node into the target mesh.
    /// </summary>
    /// <param name="mesh">The destination mesh.</param>
    /// <param name="geometry">The source FBX geometry node.</param>
    /// <param name="polygonVertexIndices">The raw polygon vertex index array.</param>
    /// <param name="vertexCount">The number of vertices in the mesh.</param>
    public static void ImportUvLayers(Mesh mesh, FbxNode geometry, int[] polygonVertexIndices, int vertexCount)
    {
        FbxNode[] uvLayerNodes = geometry.Children.Where(child => string.Equals(child.Name, "LayerElementUV", StringComparison.Ordinal)).ToArray();
        if (uvLayerNodes.Length == 0)
        {
            return;
        }

        int maxLayerIndex = 0;
        Dictionary<int, FbxNode> layerMap = [];

        for (int i = 0; i < uvLayerNodes.Length; i++)
        {
            FbxNode layerNode = uvLayerNodes[i];
            int layerIndex = layerNode.Properties.Count > 0 ? (int)layerNode.Properties[0].AsInt64() : i;
            maxLayerIndex = Math.Max(maxLayerIndex, layerIndex);
            layerMap[layerIndex] = layerNode;
        }

        int layerCount = maxLayerIndex + 1;
        float[] uvData = new float[vertexCount * layerCount * 2];
        bool[] uvWritten = new bool[vertexCount * layerCount];

        foreach ((int layerIndex, FbxNode layerNode) in layerMap)
        {
            double[] uvs = FbxSceneMapper.GetNodeArray<double>(layerNode, "UV");
            int[] uvIndices = FbxSceneMapper.GetNodeArray<int>(layerNode, "UVIndex");
            string mapping = FbxSceneMapper.GetNodeString(layerNode, "MappingInformationType") ?? "ByPolygonVertex";
            string reference = FbxSceneMapper.GetNodeString(layerNode, "ReferenceInformationType") ?? "IndexToDirect";

            int polygonVertexCounter = 0;
            for (int i = 0; i < polygonVertexIndices.Length; i++)
            {
                int encodedVertex = polygonVertexIndices[i];
                int vertexIndex = encodedVertex < 0 ? -encodedVertex - 1 : encodedVertex;
                int mappedIndex = ResolveMappedIndex(mapping, reference, polygonVertexCounter, vertexIndex, uvIndices);

                if (mappedIndex < 0 || (mappedIndex * 2 + 1) >= uvs.Length)
                {
                    polygonVertexCounter++;
                    continue;
                }

                int writtenIndex = (vertexIndex * layerCount) + layerIndex;
                if (!uvWritten[writtenIndex])
                {
                    uvWritten[writtenIndex] = true;
                    int outputOffset = writtenIndex * 2;
                    uvData[outputOffset] = (float)uvs[mappedIndex * 2];
                    uvData[outputOffset + 1] = (float)uvs[mappedIndex * 2 + 1];
                }

                polygonVertexCounter++;
            }
        }

        mesh.UVLayers = new DataBuffer<float>(uvData, layerCount, 2);
    }

    /// <summary>
    /// Imports per-triangle material indices from an FBX geometry node.
    /// </summary>
    /// <param name="geometry">The source FBX geometry node.</param>
    /// <param name="trianglesPerFace">The number of triangles produced per source polygon.</param>
    /// <returns>Per-triangle material indices when present; otherwise an empty array.</returns>
    public static int[] ImportPerTriangleMaterialIndices(FbxNode geometry, int[] trianglesPerFace)
    {
        FbxNode? materialNode = geometry.Children.FirstOrDefault(child => string.Equals(child.Name, "LayerElementMaterial", StringComparison.Ordinal));
        if (materialNode is null)
        {
            return [];
        }

        int[] perFaceMaterials = FbxSceneMapper.GetNodeArray<int>(materialNode, "Materials");
        if (perFaceMaterials.Length == 0)
        {
            return [];
        }

        return MeshTriangulator.ExpandPerFaceValuesToTriangles(perFaceMaterials, trianglesPerFace);
    }

    /// <summary>
    /// Resolves the mapped index for a given polygon-vertex counter and vertex index using the
    /// specified FBX mapping and reference information types.
    /// </summary>
    /// <param name="mapping">The FBX mapping information type string.</param>
    /// <param name="reference">The FBX reference information type string.</param>
    /// <param name="polygonVertexIndex">The current polygon-vertex counter.</param>
    /// <param name="vertexIndex">The control-point (vertex) index.</param>
    /// <param name="indexArray">The optional index redirect array.</param>
    /// <returns>The resolved data index, or <c>-1</c> when out of range.</returns>
    public static int ResolveMappedIndex(string mapping, string reference, int polygonVertexIndex, int vertexIndex, int[] indexArray)
    {
        bool byVertex = string.Equals(mapping, "ByVertex", StringComparison.OrdinalIgnoreCase)
            || string.Equals(mapping, "ByVertice", StringComparison.OrdinalIgnoreCase)
            || string.Equals(mapping, "ByControlPoint", StringComparison.OrdinalIgnoreCase);

        int directIndex = byVertex ? vertexIndex : polygonVertexIndex;
        bool isIndexed = string.Equals(reference, "IndexToDirect", StringComparison.OrdinalIgnoreCase)
            || string.Equals(reference, "Index", StringComparison.OrdinalIgnoreCase);

        if (!isIndexed)
        {
            return directIndex;
        }

        if ((uint)directIndex >= (uint)indexArray.Length)
        {
            return -1;
        }

        return indexArray[directIndex];
    }

    /// <summary>
    /// Reads a <see cref="Vector3"/> from a flat double array at the given element index.
    /// </summary>
    /// <param name="source">The source double array (stride 3).</param>
    /// <param name="index">The element index.</param>
    /// <returns>The vector, or <see cref="Vector3.Zero"/> when out of range.</returns>
    public static Vector3 ReadVector3(double[] source, int index)
    {
        int offset = index * 3;
        if (index < 0 || offset + 2 >= source.Length)
        {
            return Vector3.Zero;
        }

        return new Vector3((float)source[offset], (float)source[offset + 1], (float)source[offset + 2]);
    }

    /// <summary>
    /// Extracts all vertex positions from a mesh as a flat double array.
    /// </summary>
    /// <param name="mesh">The source mesh.</param>
    /// <returns>Flat XYZ double array, or an empty array when no positions are present.</returns>
    public static double[] ExtractPositions(Mesh mesh)
    {
        if (mesh.Positions is null)
        {
            return [];
        }

        double[] result = new double[mesh.VertexCount * 3];
        Span<Matrix4x4> stackSkinTransforms = stackalloc Matrix4x4[128];
        Matrix4x4[]? rentedSkinTransforms = null;
        ReadOnlySpan<Matrix4x4> skinTransforms = GetSkinTransforms(mesh, stackSkinTransforms, out rentedSkinTransforms);

        try
        {
            for (int vertexIndex = 0; vertexIndex < mesh.VertexCount; vertexIndex++)
            {
                int offset = vertexIndex * 3;
                Vector3 position = skinTransforms.Length > 0
                    ? mesh.GetVertexPosition(vertexIndex, skinTransforms)
                    : mesh.GetVertexPosition(vertexIndex);
                result[offset] = position.X;
                result[offset + 1] = position.Y;
                result[offset + 2] = position.Z;
            }
        }
        finally
        {
            if (rentedSkinTransforms is not null)
            {
                ArrayPool<Matrix4x4>.Shared.Return(rentedSkinTransforms);
            }
        }

        return result;
    }

    /// <summary>
    /// Extracts face indices from a mesh in the FBX polygon-vertex-index encoding where the last
    /// index of each polygon is stored as the bitwise NOT of the vertex index.
    /// </summary>
    /// <param name="mesh">The source mesh.</param>
    /// <returns>The encoded polygon vertex index array.</returns>
    public static int[] ExtractPolygonVertexIndex(Mesh mesh)
    {
        if (mesh.FaceIndices is null || mesh.FaceIndices.ElementCount == 0)
        {
            return [];
        }

        if (mesh.FaceIndices.ElementCount % 3 != 0)
        {
            throw new InvalidDataException($"Mesh '{mesh.Name}' must contain triangle-list indices to export FBX.");
        }

        int[] triangleIndices = new int[mesh.FaceIndices.ElementCount];
        for (int i = 0; i < triangleIndices.Length; i++)
        {
            int value = mesh.FaceIndices.Get<int>(i, 0, 0);
            if (i % 3 == 2)
            {
                value = -value - 1;
            }

            triangleIndices[i] = value;
        }

        return triangleIndices;
    }

    /// <summary>
    /// Extracts unique FBX edge representatives as polygon-vertex slot indices.
    /// </summary>
    /// <param name="mesh">The source mesh.</param>
    /// <returns>The edge slot array, or an empty array when no index data is present.</returns>
    public static int[] ExtractEdges(Mesh mesh)
    {
        if (mesh.FaceIndices is null || mesh.FaceIndices.ElementCount == 0)
        {
            return [];
        }

        if (mesh.FaceIndices.ElementCount % 3 != 0)
        {
            throw new InvalidDataException($"Mesh '{mesh.Name}' must contain triangle-list indices to export FBX.");
        }

        Dictionary<long, int> edges = [];
        for (int triangleBaseIndex = 0; triangleBaseIndex < mesh.FaceIndices.ElementCount; triangleBaseIndex += 3)
        {
            int vertex0 = mesh.FaceIndices.Get<int>(triangleBaseIndex, 0, 0);
            int vertex1 = mesh.FaceIndices.Get<int>(triangleBaseIndex + 1, 0, 0);
            int vertex2 = mesh.FaceIndices.Get<int>(triangleBaseIndex + 2, 0, 0);

            AddEdge(edges, vertex0, vertex1, triangleBaseIndex);
            AddEdge(edges, vertex1, vertex2, triangleBaseIndex + 1);
            AddEdge(edges, vertex2, vertex0, triangleBaseIndex + 2);
        }

        return edges.Values.ToArray();
    }

    /// <summary>
    /// Extracts normal vectors from a mesh as a flat double array.
    /// </summary>
    /// <param name="mesh">The source mesh.</param>
    /// <returns>Flat XYZ double array, or an empty array when no normals are present.</returns>
    public static double[] ExtractNormals(Mesh mesh)
    {
        if (mesh.Normals is null)
        {
            return [];
        }

        double[] result = new double[mesh.VertexCount * 3];
        Span<Matrix4x4> stackSkinTransforms = stackalloc Matrix4x4[128];
        Matrix4x4[]? rentedSkinTransforms = null;
        ReadOnlySpan<Matrix4x4> skinTransforms = GetSkinTransforms(mesh, stackSkinTransforms, out rentedSkinTransforms);

        try
        {
            for (int vertexIndex = 0; vertexIndex < mesh.VertexCount; vertexIndex++)
            {
                int offset = vertexIndex * 3;
                Vector3 normal = skinTransforms.Length > 0
                    ? mesh.GetVertexNormal(vertexIndex, skinTransforms)
                    : mesh.GetVertexNormal(vertexIndex);
                result[offset] = normal.X;
                result[offset + 1] = normal.Y;
                result[offset + 2] = normal.Z;
            }
        }
        finally
        {
            if (rentedSkinTransforms is not null)
            {
                ArrayPool<Matrix4x4>.Shared.Return(rentedSkinTransforms);
            }
        }

        return result;
    }

    /// <summary>
    /// Extracts a single UV layer from a mesh as a flat double array.
    /// </summary>
    /// <param name="mesh">The source mesh.</param>
    /// <param name="layerIndex">The zero-based UV layer index to extract.</param>
    /// <returns>Flat UV double array, or an empty array when no UV data is present.</returns>
    public static double[] ExtractUvLayer(Mesh mesh, int layerIndex)
    {
        if (mesh.UVLayers is null)
        {
            return [];
        }

        double[] result = new double[mesh.VertexCount * 2];
        for (int vertexIndex = 0; vertexIndex < mesh.VertexCount; vertexIndex++)
        {
            int offset = vertexIndex * 2;
            Vector2 uv = mesh.UVLayers.GetVector2(vertexIndex, layerIndex);
            result[offset] = uv.X;
            result[offset + 1] = uv.Y;
        }

        return result;
    }

    /// <summary>
    /// Extracts normals per polygon vertex to match Maya-style FBX mesh layer encoding.
    /// </summary>
    /// <param name="mesh">The source mesh.</param>
    /// <returns>Flat XYZ double array in polygon-vertex order.</returns>
    public static double[] ExtractPolygonVertexNormals(Mesh mesh)
    {
        if (mesh.Normals is null || mesh.FaceIndices is null)
        {
            return [];
        }

        double[] result = new double[mesh.FaceIndices.ElementCount * 3];
        Span<Matrix4x4> stackSkinTransforms = stackalloc Matrix4x4[128];
        Matrix4x4[]? rentedSkinTransforms = null;
        ReadOnlySpan<Matrix4x4> skinTransforms = GetSkinTransforms(mesh, stackSkinTransforms, out rentedSkinTransforms);

        try
        {
            for (int faceIndex = 0; faceIndex < mesh.FaceIndices.ElementCount; faceIndex++)
            {
                int vertexIndex = mesh.FaceIndices.Get<int>(faceIndex, 0, 0);
                Vector3 normal = skinTransforms.Length > 0
                    ? mesh.GetVertexNormal(vertexIndex, skinTransforms)
                    : mesh.GetVertexNormal(vertexIndex);
                int offset = faceIndex * 3;
                result[offset] = normal.X;
                result[offset + 1] = normal.Y;
                result[offset + 2] = normal.Z;
            }
        }
        finally
        {
            if (rentedSkinTransforms is not null)
            {
                ArrayPool<Matrix4x4>.Shared.Return(rentedSkinTransforms);
            }
        }

        return result;
    }

    /// <summary>
    /// Adds a unique undirected edge keyed by its control-point pair.
    /// </summary>
    /// <param name="edges">The unique edge map.</param>
    /// <param name="vertex0">The first control-point index.</param>
    /// <param name="vertex1">The second control-point index.</param>
    /// <param name="polygonVertexSlot">The polygon-vertex slot representing this edge.</param>
    public static void AddEdge(Dictionary<long, int> edges, int vertex0, int vertex1, int polygonVertexSlot)
    {
        int minVertex = Math.Min(vertex0, vertex1);
        int maxVertex = Math.Max(vertex0, vertex1);
        long edgeKey = ((long)minVertex << 32) | (uint)maxVertex;
        if (!edges.ContainsKey(edgeKey))
        {
            edges.Add(edgeKey, polygonVertexSlot);
        }
    }

    /// <summary>
    /// Extracts a UV layer per polygon vertex to match Maya-style FBX mesh layer encoding.
    /// </summary>
    /// <param name="mesh">The source mesh.</param>
    /// <param name="layerIndex">The UV layer index.</param>
    /// <returns>Flat UV double array in polygon-vertex order.</returns>
    public static double[] ExtractPolygonVertexUvLayer(Mesh mesh, int layerIndex)
    {
        if (mesh.UVLayers is null || mesh.FaceIndices is null)
        {
            return [];
        }

        double[] result = new double[mesh.FaceIndices.ElementCount * 2];
        for (int faceIndex = 0; faceIndex < mesh.FaceIndices.ElementCount; faceIndex++)
        {
            int vertexIndex = mesh.FaceIndices.Get<int>(faceIndex, 0, 0);
            Vector2 uv = mesh.UVLayers.GetVector2(vertexIndex, layerIndex);
            int offset = faceIndex * 2;
            result[offset] = uv.X;
            result[offset + 1] = uv.Y;
        }

        return result;
    }

    /// <summary>
    /// Extracts polygon-vertex UV indices that reference the per-vertex UV direct array.
    /// </summary>
    /// <param name="mesh">The source mesh.</param>
    /// <returns>The UV index array in polygon-vertex order.</returns>
    public static int[] ExtractPolygonVertexUvIndices(Mesh mesh)
    {
        if (mesh.FaceIndices is null)
        {
            return [];
        }

        int[] result = new int[mesh.FaceIndices.ElementCount];
        for (int faceIndex = 0; faceIndex < mesh.FaceIndices.ElementCount; faceIndex++)
        {
            result[faceIndex] = mesh.FaceIndices.Get<int>(faceIndex, 0, 0);
        }

        return result;
    }

    /// <summary>
    /// Creates a sequential integer array from zero to count minus one.
    /// </summary>
    /// <param name="count">The element count.</param>
    /// <returns>The generated index array.</returns>
    public static int[] CreateSequentialIndices(int count)
    {
        int[] result = new int[count];
        for (int i = 0; i < count; i++)
        {
            result[i] = i;
        }

        return result;
    }

    /// <summary>
    /// Creates a filled double array with a repeated value.
    /// </summary>
    /// <param name="count">The element count.</param>
    /// <param name="value">The repeated value.</param>
    /// <returns>The filled array.</returns>
    public static double[] CreateFilledArray(int count, double value)
    {
        double[] result = new double[count];
        for (int i = 0; i < count; i++)
        {
            result[i] = value;
        }

        return result;
    }

    /// <summary>
    /// Resolves precomputed skin transforms aligned to the mesh skin palette for export-time geometry evaluation.
    /// </summary>
    /// <param name="mesh">The source mesh.</param>
    /// <param name="stackSkinTransforms">A caller-provided stack buffer used for small palettes.</param>
    /// <param name="rentedSkinTransforms">Receives a rented array when the mesh palette exceeds the stack buffer.</param>
    /// <returns>A span containing the resolved skin transforms, or an empty span when the mesh is not skinned.</returns>
    public static ReadOnlySpan<Matrix4x4> GetSkinTransforms(Mesh mesh, Span<Matrix4x4> stackSkinTransforms, out Matrix4x4[]? rentedSkinTransforms)
    {
        rentedSkinTransforms = null;

        int skinnedBoneCount = mesh.SkinnedBones?.Count ?? 0;
        if (skinnedBoneCount == 0)
        {
            return [];
        }

        Span<Matrix4x4> skinTransforms = skinnedBoneCount <= stackSkinTransforms.Length
            ? stackSkinTransforms[..skinnedBoneCount]
            : (rentedSkinTransforms = ArrayPool<Matrix4x4>.Shared.Rent(skinnedBoneCount)).AsSpan(0, skinnedBoneCount);

        mesh.CopySkinTransforms(skinTransforms);
        return skinTransforms;
    }
}
