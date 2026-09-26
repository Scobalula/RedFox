using System.Numerics;
using RedFox.Graphics3D.Buffers;

namespace RedFox.Graphics3D.Formats.KaydaraFbx;

/// <summary>
/// Maps FBX blend shape deformers to and from RedFox morph data.
/// </summary>
public static class FbxMorphMapper
{
    private const string BlendShapeType = "BlendShape";

    private const string ChannelType = "BlendShapeChannel";

    private const string ShapeType = "Shape";

    /// <summary>
    /// Imports FBX blend shape deformers into mesh morphs. Each blend shape channel becomes one morph target.
    /// </summary>
    /// <param name="meshesByModelId">Mesh map keyed by FBX model id.</param>
    /// <param name="objectsById">Object map keyed by FBX object id.</param>
    /// <param name="connections">FBX connection list.</param>
    public static void ImportMorphs(Dictionary<long, Mesh> meshesByModelId, Dictionary<long, FbxNode> objectsById, IReadOnlyList<FbxConnection> connections)
    {
        Dictionary<long, List<long>> geometryToBlendShapes = [];
        Dictionary<long, List<long>> blendShapeToChannels = [];
        Dictionary<long, long> channelToShape = [];

        foreach (FbxConnection connection in connections)
        {
            if (!string.Equals(connection.ConnectionType, "OO", StringComparison.Ordinal) || !objectsById.TryGetValue(connection.ChildId, out FbxNode? child))
                continue;

            if (IsObjectOfType(child, "Deformer", BlendShapeType))
                GetOrAdd(geometryToBlendShapes, connection.ParentId).Add(connection.ChildId);
            else if (IsObjectOfType(child, "Deformer", ChannelType))
                GetOrAdd(blendShapeToChannels, connection.ParentId).Add(connection.ChildId);
            else if (IsObjectOfType(child, "Geometry", ShapeType))
                channelToShape[connection.ParentId] = connection.ChildId;
        }

        foreach ((long geometryId, List<long> blendShapeIds) in geometryToBlendShapes)
        {
            if (!meshesByModelId.TryGetValue(FbxSkinningMapper.ResolveGeometryOwnerModelId(geometryId, connections), out Mesh? mesh) || mesh.VertexCount == 0)
                continue;

            List<(FbxNode Channel, FbxNode Shape)> targets = [];

            foreach (long blendShapeId in blendShapeIds)
            {
                foreach (long channelId in blendShapeToChannels.GetValueOrDefault(blendShapeId) ?? [])
                {
                    if (channelToShape.TryGetValue(channelId, out long shapeId))
                        targets.Add((objectsById[channelId], objectsById[shapeId]));
                }
            }

            if (targets.Count == 0)
                continue;

            mesh.Morph = CreateMorph(mesh.VertexCount, targets);
            mesh.Morph.Name = FbxSceneMapper.GetNodeObjectName(objectsById[blendShapeIds[0]]);
        }
    }

    /// <summary>
    /// Exports a mesh morph as an FBX blend shape deformer with one channel and shape per target.
    /// </summary>
    /// <param name="objectsNode">The FBX Objects node.</param>
    /// <param name="connectionsNode">The FBX Connections node.</param>
    /// <param name="mesh">The source mesh.</param>
    /// <param name="geometryId">The target geometry object id.</param>
    /// <param name="nextId">The mutable object id counter.</param>
    public static void ExportMorph(FbxNode objectsNode, FbxNode connectionsNode, Mesh mesh, long geometryId, ref long nextId)
    {
        if (mesh.Morph is not { TargetCount: > 0 } morph)
            return;

        long blendShapeId = nextId++;
        objectsNode.Children.Add(CreateObject("Deformer", blendShapeId, morph.Name ?? mesh.Name + "_BlendShape", "Deformer", BlendShapeType));
        FbxSceneMapper.AddConnection(connectionsNode, "OO", blendShapeId, geometryId);

        for (int targetIndex = 0; targetIndex < morph.TargetCount; targetIndex++)
        {
            string targetName = morph.TargetNames[targetIndex];
            double deformPercent = morph.Weights[targetIndex] * 100.0;

            long channelId = nextId++;
            FbxNode channel = CreateObject("Deformer", channelId, targetName, "SubDeformer", ChannelType);
            channel.Children.Add(new FbxNode("DeformPercent") { Properties = { new FbxProperty('D', deformPercent) } });
            channel.Children.Add(new FbxNode("FullWeights") { Properties = { new FbxProperty('d', new[] { 100.0 }) } });
            FbxSceneMapper.AddDoubleProperty(channel.AddChild("Properties70"), "DeformPercent", "Number", deformPercent);
            objectsNode.Children.Add(channel);

            long shapeId = nextId++;
            objectsNode.Children.Add(CreateShape(shapeId, targetName, morph, targetIndex));

            FbxSceneMapper.AddConnection(connectionsNode, "OO", channelId, blendShapeId);
            FbxSceneMapper.AddConnection(connectionsNode, "OO", shapeId, channelId);
        }
    }

    private static Morph CreateMorph(int vertexCount, List<(FbxNode Channel, FbxNode Shape)> targets)
    {
        int targetCount = targets.Count;
        bool hasNormals = targets.Exists(target => target.Shape.FirstChild("Normals") is not null);
        float[] positionDeltas = new float[vertexCount * targetCount * 3];
        float[]? normalDeltas = hasNormals ? new float[vertexCount * targetCount * 3] : null;
        string[] targetNames = new string[targetCount];

        for (int targetIndex = 0; targetIndex < targetCount; targetIndex++)
        {
            (FbxNode channel, FbxNode shape) = targets[targetIndex];
            targetNames[targetIndex] = FbxSceneMapper.GetNodeObjectName(channel);

            int[] indices = FbxSceneMapper.GetNodeArray<int>(shape, "Indexes");
            ReadShapeDeltas(indices, FbxSceneMapper.GetNodeArray<double>(shape, "Vertices"), positionDeltas, vertexCount, targetCount, targetIndex);

            if (normalDeltas is not null)
                ReadShapeDeltas(indices, FbxSceneMapper.GetNodeArray<double>(shape, "Normals"), normalDeltas, vertexCount, targetCount, targetIndex);
        }

        Morph morph = new(targetNames, new DataBuffer<float>(positionDeltas, targetCount, 3), normalDeltas is null ? null : new DataBuffer<float>(normalDeltas, targetCount, 3), null);

        for (int targetIndex = 0; targetIndex < targetCount; targetIndex++)
            morph.Weights[targetIndex] = (float)(GetDeformPercent(targets[targetIndex].Channel) / 100.0);

        return morph;
    }

    private static void ReadShapeDeltas(int[] indices, double[] deltas, float[] destination, int vertexCount, int targetCount, int targetIndex)
    {
        int count = Math.Min(indices.Length, deltas.Length / 3);

        for (int i = 0; i < count; i++)
        {
            int vertexIndex = indices[i];

            if ((uint)vertexIndex >= (uint)vertexCount)
                continue;

            int offset = ((vertexIndex * targetCount) + targetIndex) * 3;
            destination[offset] = (float)deltas[i * 3];
            destination[offset + 1] = (float)deltas[(i * 3) + 1];
            destination[offset + 2] = (float)deltas[(i * 3) + 2];
        }
    }

    private static double GetDeformPercent(FbxNode channel)
    {
        if (channel.FirstChild("Properties70") is { } properties)
        {
            double animatedPercent = FbxSceneMapper.GetPropertyDouble(properties, "DeformPercent", double.NaN);

            if (!double.IsNaN(animatedPercent))
                return animatedPercent;
        }

        return channel.FirstChild("DeformPercent") is { Properties.Count: > 0 } node ? node.Properties[0].AsDouble() : 0.0;
    }

    private static FbxNode CreateShape(long id, string name, Morph morph, int targetIndex)
    {
        List<int> indices = [];
        List<double> vertices = [];
        List<double> normals = [];

        for (int vertexIndex = 0; vertexIndex < morph.VertexCount; vertexIndex++)
        {
            Vector3 position = morph.DeltaPositions?.GetVector3(vertexIndex, targetIndex) ?? Vector3.Zero;
            Vector3 normal = morph.DeltaNormals?.GetVector3(vertexIndex, targetIndex) ?? Vector3.Zero;

            if (position == Vector3.Zero && normal == Vector3.Zero)
                continue;

            indices.Add(vertexIndex);
            vertices.AddRange([position.X, position.Y, position.Z]);
            normals.AddRange([normal.X, normal.Y, normal.Z]);
        }

        FbxNode shape = CreateObject("Geometry", id, name, "Geometry", ShapeType);
        shape.Children.Add(new FbxNode("Indexes") { Properties = { new FbxProperty('i', indices.ToArray()) } });
        shape.Children.Add(new FbxNode("Vertices") { Properties = { new FbxProperty('d', vertices.ToArray()) } });

        if (morph.DeltaNormals is not null)
            shape.Children.Add(new FbxNode("Normals") { Properties = { new FbxProperty('d', normals.ToArray()) } });

        return shape;
    }

    private static FbxNode CreateObject(string nodeName, long id, string name, string className, string type)
    {
        FbxNode node = new(nodeName);
        node.Properties.Add(new FbxProperty('L', id));
        node.Properties.Add(new FbxProperty('S', name + "\0\u0001" + className));
        node.Properties.Add(new FbxProperty('S', type));
        node.Children.Add(new FbxNode("Version") { Properties = { new FbxProperty('I', 100) } });
        return node;
    }

    private static bool IsObjectOfType(FbxNode node, string nodeName, string type) => string.Equals(node.Name, nodeName, StringComparison.Ordinal) && node.Properties.Count > 2 && string.Equals(node.Properties[2].AsString(), type, StringComparison.OrdinalIgnoreCase);

    private static List<long> GetOrAdd(Dictionary<long, List<long>> map, long key)
    {
        if (!map.TryGetValue(key, out List<long>? values))
        {
            values = [];
            map[key] = values;
        }

        return values;
    }
}
