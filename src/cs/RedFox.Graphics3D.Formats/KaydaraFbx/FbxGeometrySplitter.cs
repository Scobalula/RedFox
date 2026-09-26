using System.Numerics;
using System.Text;
using RedFox.Graphics3D.Buffers;

namespace RedFox.Graphics3D.Formats.KaydaraFbx;

internal static class FbxGeometrySplitter
{
    internal static void SplitVertices(Mesh mesh, FbxNode geometry)
    {
        int[] polygonVertexIndices = FbxSceneMapper.GetNodeArray<int>(geometry, "PolygonVertexIndex");

        FbxNode? normalNode = geometry.Children.FirstOrDefault(child => string.Equals(child.Name, "LayerElementNormal", StringComparison.Ordinal));
        FbxNode? tangentNode = geometry.Children.FirstOrDefault(child => string.Equals(child.Name, "LayerElementTangent", StringComparison.Ordinal));
        FbxNode[] colorNodes = geometry.Children.Where(child => string.Equals(child.Name, "LayerElementColor", StringComparison.Ordinal)).ToArray();
        FbxNode[] uvNodes = geometry.Children.Where(child => string.Equals(child.Name, "LayerElementUV", StringComparison.Ordinal)).ToArray();

        if (normalNode is null && tangentNode is null && colorNodes.Length == 0 && uvNodes.Length == 0)
        {
            return;
        }

        string normalMapping = normalNode is null ? string.Empty : FbxSceneMapper.GetNodeString(normalNode, "MappingInformationType") ?? "ByPolygonVertex";
        string normalReference = normalNode is null ? string.Empty : FbxSceneMapper.GetNodeString(normalNode, "ReferenceInformationType") ?? "Direct";
        int[] normalIndices = normalNode is null ? [] : FbxSceneMapper.GetNodeArray<int>(normalNode, "NormalsIndex");

        string tangentMapping = tangentNode is null ? string.Empty : FbxSceneMapper.GetNodeString(tangentNode, "MappingInformationType") ?? "ByPolygonVertex";
        string tangentReference = tangentNode is null ? string.Empty : FbxSceneMapper.GetNodeString(tangentNode, "ReferenceInformationType") ?? "Direct";
        int[] tangentIndices = tangentNode is null ? [] : FbxSceneMapper.GetNodeArray<int>(tangentNode, "TangentsIndex");

        List<(int LayerIndex, int[] Indices, string Mapping, string Reference)> colorLayers = GetLayers(colorNodes, "ColorIndex", "ByPolygonVertex", "Direct");
        List<(int LayerIndex, int[] Indices, string Mapping, string Reference)> uvLayers = GetLayers(uvNodes, "UVIndex", "ByPolygonVertex", "IndexToDirect");

        Dictionary<string, int> verticesByAttributes = new(StringComparer.Ordinal);
        List<int> sourceVertexIndices = [];
        List<int> sourceCornerIndices = [];
        int[] splitVertexByCorner = new int[polygonVertexIndices.Length];
        StringBuilder keyBuilder = new();

        for (int cornerIndex = 0; cornerIndex < polygonVertexIndices.Length; cornerIndex++)
        {
            int encodedVertex = polygonVertexIndices[cornerIndex];
            int sourceVertexIndex = encodedVertex < 0 ? -encodedVertex - 1 : encodedVertex;
            keyBuilder.Clear();
            keyBuilder.Append(sourceVertexIndex);
            keyBuilder.Append('|');
            keyBuilder.Append(normalNode is null ? -1 : FbxGeometryMapper.ResolveMappedIndex(normalMapping, normalReference, cornerIndex, sourceVertexIndex, normalIndices));
            keyBuilder.Append('|');
            keyBuilder.Append(tangentNode is null ? -1 : FbxGeometryMapper.ResolveMappedIndex(tangentMapping, tangentReference, cornerIndex, sourceVertexIndex, tangentIndices));

            AppendLayerIndices(keyBuilder, colorLayers, cornerIndex, sourceVertexIndex);
            AppendLayerIndices(keyBuilder, uvLayers, cornerIndex, sourceVertexIndex);

            string key = keyBuilder.ToString();
            if (!verticesByAttributes.TryGetValue(key, out int splitVertexIndex))
            {
                splitVertexIndex = sourceVertexIndices.Count;
                verticesByAttributes.Add(key, splitVertexIndex);
                sourceVertexIndices.Add(sourceVertexIndex);
                sourceCornerIndices.Add(cornerIndex);
            }

            splitVertexByCorner[cornerIndex] = splitVertexIndex;
        }

        List<int> triangleIndices = [];
        List<int> faceCorners = [];
        for (int cornerIndex = 0; cornerIndex < polygonVertexIndices.Length; cornerIndex++)
        {
            faceCorners.Add(cornerIndex);
            if (polygonVertexIndices[cornerIndex] >= 0)
            {
                continue;
            }

            for (int triangleIndex = 1; triangleIndex < faceCorners.Count - 1; triangleIndex++)
            {
                triangleIndices.Add(splitVertexByCorner[faceCorners[0]]);
                triangleIndices.Add(splitVertexByCorner[faceCorners[triangleIndex]]);
                triangleIndices.Add(splitVertexByCorner[faceCorners[triangleIndex + 1]]);
            }

            faceCorners.Clear();
        }

        ExpandVertexData(mesh, geometry, sourceVertexIndices, sourceCornerIndices);
        mesh.FaceIndices = new DataBuffer<int>(triangleIndices.ToArray(), 1, 1);
    }

    private static List<(int LayerIndex, int[] Indices, string Mapping, string Reference)> GetLayers(FbxNode[] nodes, string indexName, string defaultMapping, string defaultReference)
    {
        List<(int LayerIndex, int[] Indices, string Mapping, string Reference)> layers = [];
        foreach (FbxNode node in nodes)
        {
            int layerIndex = node.Properties.Count > 0 ? (int)node.Properties[0].AsInt64() : layers.Count;
            layers.Add((layerIndex, FbxSceneMapper.GetNodeArray<int>(node, indexName), FbxSceneMapper.GetNodeString(node, "MappingInformationType") ?? defaultMapping, FbxSceneMapper.GetNodeString(node, "ReferenceInformationType") ?? defaultReference));
        }

        return layers;
    }

    private static void AppendLayerIndices(StringBuilder keyBuilder, List<(int LayerIndex, int[] Indices, string Mapping, string Reference)> layers, int cornerIndex, int sourceVertexIndex)
    {
        foreach ((int _, int[] indices, string mapping, string reference) in layers)
        {
            keyBuilder.Append('|');
            keyBuilder.Append(FbxGeometryMapper.ResolveMappedIndex(mapping, reference, cornerIndex, sourceVertexIndex, indices));
        }
    }

    private static void ExpandVertexData(Mesh mesh, FbxNode geometry, List<int> sourceVertexIndices, List<int> sourceCornerIndices)
    {
        FbxNode? normalNode = geometry.Children.FirstOrDefault(child => string.Equals(child.Name, "LayerElementNormal", StringComparison.Ordinal));
        FbxNode? tangentNode = geometry.Children.FirstOrDefault(child => string.Equals(child.Name, "LayerElementTangent", StringComparison.Ordinal));
        FbxNode[] colorNodes = geometry.Children.Where(child => string.Equals(child.Name, "LayerElementColor", StringComparison.Ordinal)).ToArray();
        FbxNode[] uvNodes = geometry.Children.Where(child => string.Equals(child.Name, "LayerElementUV", StringComparison.Ordinal)).ToArray();

        List<(int LayerIndex, double[] Values, int[] Indices, string Mapping, string Reference)> colorLayers = GetValueLayers(colorNodes, "Colors", "ColorIndex", "ByPolygonVertex", "Direct");
        List<(int LayerIndex, double[] Values, int[] Indices, string Mapping, string Reference)> uvLayers = GetValueLayers(uvNodes, "UV", "UVIndex", "ByPolygonVertex", "IndexToDirect");

        float[] positions = new float[sourceVertexIndices.Count * 3];
        for (int vertexIndex = 0; vertexIndex < sourceVertexIndices.Count; vertexIndex++)
        {
            Vector3 position = mesh.Positions!.GetVector3(sourceVertexIndices[vertexIndex], 0);
            int offset = vertexIndex * 3;
            positions[offset] = position.X;
            positions[offset + 1] = position.Y;
            positions[offset + 2] = position.Z;
        }

        mesh.Positions = new DataBuffer<float>(positions, 1, 3);

        if (normalNode is not null)
        {
            ExpandNormals(mesh, normalNode, sourceVertexIndices, sourceCornerIndices);
        }
        if (tangentNode is not null)
        {
            ExpandTangents(mesh, tangentNode, sourceVertexIndices, sourceCornerIndices);
        }
        if (colorLayers.Count > 0)
        {
            ExpandColorLayers(mesh, colorLayers, sourceVertexIndices, sourceCornerIndices);
        }
        if (uvLayers.Count > 0)
        {
            ExpandUvLayers(mesh, uvLayers, sourceVertexIndices, sourceCornerIndices);
        }

        ExpandSkinData(mesh, sourceVertexIndices);
        ExpandMorphData(mesh, sourceVertexIndices);
    }

    private static List<(int LayerIndex, double[] Values, int[] Indices, string Mapping, string Reference)> GetValueLayers(FbxNode[] nodes, string valueName, string indexName, string defaultMapping, string defaultReference)
    {
        List<(int LayerIndex, double[] Values, int[] Indices, string Mapping, string Reference)> layers = [];
        foreach (FbxNode node in nodes)
        {
            int layerIndex = node.Properties.Count > 0 ? (int)node.Properties[0].AsInt64() : layers.Count;
            layers.Add((layerIndex, FbxSceneMapper.GetNodeArray<double>(node, valueName), FbxSceneMapper.GetNodeArray<int>(node, indexName), FbxSceneMapper.GetNodeString(node, "MappingInformationType") ?? defaultMapping, FbxSceneMapper.GetNodeString(node, "ReferenceInformationType") ?? defaultReference));
        }

        return layers;
    }

    private static void ExpandNormals(Mesh mesh, FbxNode normalNode, List<int> sourceVertexIndices, List<int> sourceCornerIndices)
    {
        double[] values = FbxSceneMapper.GetNodeArray<double>(normalNode, "Normals");
        string mapping = FbxSceneMapper.GetNodeString(normalNode, "MappingInformationType") ?? "ByPolygonVertex";
        string reference = FbxSceneMapper.GetNodeString(normalNode, "ReferenceInformationType") ?? "Direct";
        int[] indices = FbxSceneMapper.GetNodeArray<int>(normalNode, "NormalsIndex");
        float[] normals = new float[sourceVertexIndices.Count * 3];
        for (int vertexIndex = 0; vertexIndex < sourceVertexIndices.Count; vertexIndex++)
        {
            int mappedIndex = FbxGeometryMapper.ResolveMappedIndex(mapping, reference, sourceCornerIndices[vertexIndex], sourceVertexIndices[vertexIndex], indices);
            Vector3 normal = FbxGeometryMapper.ReadVector3(values, mappedIndex);
            if (normal.LengthSquared() > 0f)
            {
                normal = Vector3.Normalize(normal);
            }
            else
            {
                normal = Vector3.UnitZ;
            }

            int offset = vertexIndex * 3;
            normals[offset] = normal.X;
            normals[offset + 1] = normal.Y;
            normals[offset + 2] = normal.Z;
        }
        mesh.Normals = new DataBuffer<float>(normals, 1, 3);
    }

    private static void ExpandTangents(Mesh mesh, FbxNode tangentNode, List<int> sourceVertexIndices, List<int> sourceCornerIndices)
    {
        double[] values = FbxSceneMapper.GetNodeArray<double>(tangentNode, "Tangents");
        double[] signs = FbxSceneMapper.GetNodeArray<double>(tangentNode, "TangentsW");
        string mapping = FbxSceneMapper.GetNodeString(tangentNode, "MappingInformationType") ?? "ByPolygonVertex";
        string reference = FbxSceneMapper.GetNodeString(tangentNode, "ReferenceInformationType") ?? "Direct";
        int[] indices = FbxSceneMapper.GetNodeArray<int>(tangentNode, "TangentsIndex");
        float[] tangents = new float[sourceVertexIndices.Count * 4];
        for (int vertexIndex = 0; vertexIndex < sourceVertexIndices.Count; vertexIndex++)
        {
            int mappedIndex = FbxGeometryMapper.ResolveMappedIndex(mapping, reference, sourceCornerIndices[vertexIndex], sourceVertexIndices[vertexIndex], indices);
            Vector3 direction = FbxGeometryMapper.ReadVector3(values, mappedIndex);
            if (direction.LengthSquared() > 0f)
            {
                direction = Vector3.Normalize(direction);
            }
            int offset = vertexIndex * 4;
            tangents[offset] = direction.X;
            tangents[offset + 1] = direction.Y;
            tangents[offset + 2] = direction.Z;
            tangents[offset + 3] = mappedIndex >= 0 && mappedIndex * 3 + 2 < values.Length ? (mappedIndex < signs.Length ? (float)signs[mappedIndex] : 1f) : 0f;
        }

        mesh.Tangents = new DataBuffer<float>(tangents, 1, 4);
    }

    private static void ExpandColorLayers(Mesh mesh, List<(int LayerIndex, double[] Values, int[] Indices, string Mapping, string Reference)> layers, List<int> sourceVertexIndices, List<int> sourceCornerIndices)
    {
        int layerCount = layers.Max(layer => layer.LayerIndex) + 1;
        float[] colors = new float[sourceVertexIndices.Count * layerCount * 4];
        foreach ((int layerIndex, double[] values, int[] indices, string mapping, string reference) in layers)
        {
            for (int vertexIndex = 0; vertexIndex < sourceVertexIndices.Count; vertexIndex++)
            {
                int mappedIndex = FbxGeometryMapper.ResolveMappedIndex(mapping, reference, sourceCornerIndices[vertexIndex], sourceVertexIndices[vertexIndex], indices);
                Vector4 color = FbxGeometryMapper.ReadVector4(values, mappedIndex, 4);
                int offset = (vertexIndex * layerCount + layerIndex) * 4;
                colors[offset] = color.X;
                colors[offset + 1] = color.Y;
                colors[offset + 2] = color.Z;
                colors[offset + 3] = color.W;
            }
        }

        mesh.ColorLayers = new DataBuffer<float>(colors, layerCount, 4);
    }

    private static void ExpandUvLayers(Mesh mesh, List<(int LayerIndex, double[] Values, int[] Indices, string Mapping, string Reference)> layers, List<int> sourceVertexIndices, List<int> sourceCornerIndices)
    {
        int layerCount = layers.Max(layer => layer.LayerIndex) + 1;
        float[] uvs = new float[sourceVertexIndices.Count * layerCount * 2];
        foreach ((int layerIndex, double[] values, int[] indices, string mapping, string reference) in layers)
        {
            for (int vertexIndex = 0; vertexIndex < sourceVertexIndices.Count; vertexIndex++)
            {
                int mappedIndex = FbxGeometryMapper.ResolveMappedIndex(mapping, reference, sourceCornerIndices[vertexIndex], sourceVertexIndices[vertexIndex], indices);
                if (mappedIndex < 0 || mappedIndex * 2 + 1 >= values.Length)
                {
                    continue;
                }

                int offset = (vertexIndex * layerCount + layerIndex) * 2;
                uvs[offset] = (float)values[mappedIndex * 2];
                uvs[offset + 1] = (float)values[mappedIndex * 2 + 1];
            }
        }

        mesh.UVLayers = new DataBuffer<float>(uvs, layerCount, 2);
    }

    private static void ExpandSkinData(Mesh mesh, List<int> sourceVertexIndices)
    {
        if (mesh.Skin is not { } skin)
        {
            return;
        }

        int influenceCount = skin.InfluenceCount;
        ushort[] boneIndices = new ushort[sourceVertexIndices.Count * influenceCount];
        float[] boneWeights = new float[sourceVertexIndices.Count * influenceCount];
        for (int vertexIndex = 0; vertexIndex < sourceVertexIndices.Count; vertexIndex++)
        {
            int sourceVertexIndex = sourceVertexIndices[vertexIndex];
            for (int influenceIndex = 0; influenceIndex < influenceCount; influenceIndex++)
            {
                int offset = vertexIndex * influenceCount + influenceIndex;
                boneIndices[offset] = (ushort)skin.BoneIndices.Get<int>(sourceVertexIndex, influenceIndex, 0);
                boneWeights[offset] = skin.BoneWeights.Get<float>(sourceVertexIndex, influenceIndex, 0);
            }
        }

        skin.BoneIndices = new DataBuffer<ushort>(boneIndices, influenceCount, 1);
        skin.BoneWeights = new DataBuffer<float>(boneWeights, influenceCount, 1);
    }

    private static void ExpandMorphData(Mesh mesh, List<int> sourceVertexIndices)
    {
        if (mesh.Morph is not { } morph)
        {
            return;
        }

        Morph expandedMorph = new(morph.TargetNames, ExpandMorphBuffer(morph.DeltaPositions, sourceVertexIndices), ExpandMorphBuffer(morph.DeltaNormals, sourceVertexIndices), ExpandMorphBuffer(morph.DeltaTangents, sourceVertexIndices))
        {
            Name = morph.Name,
        };
        Array.Copy(morph.Weights, expandedMorph.Weights, morph.Weights.Length);
        mesh.Morph = expandedMorph;
    }

    private static DataBuffer<float>? ExpandMorphBuffer(DataBuffer? source, List<int> sourceVertexIndices)
    {
        if (source is null)
        {
            return null;
        }

        float[] values = new float[sourceVertexIndices.Count * source.ValueCount * source.ComponentCount];
        for (int vertexIndex = 0; vertexIndex < sourceVertexIndices.Count; vertexIndex++)
        {
            for (int valueIndex = 0; valueIndex < source.ValueCount; valueIndex++)
            {
                for (int componentIndex = 0; componentIndex < source.ComponentCount; componentIndex++)
                {
                    int offset = (vertexIndex * source.ValueCount + valueIndex) * source.ComponentCount + componentIndex;
                    values[offset] = source.Get<float>(sourceVertexIndices[vertexIndex], valueIndex, componentIndex);
                }
            }
        }

        return new DataBuffer<float>(values, source.ValueCount, source.ComponentCount);
    }
}
