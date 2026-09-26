using CastNet;
using CastNet.Nodes;
using RedFox.Graphics3D.Buffers;
using System.Numerics;
using System.Runtime.InteropServices;

namespace RedFox.Graphics3D.Formats.Cast;

internal static class CastMorphTranslator
{
    public static void Read(ModelNode modelNode, Dictionary<MeshNode, Mesh> meshes)
    {
        foreach (var group in modelNode.EnumerateBlendShapes().GroupBy(blendShape => blendShape.BaseShape))
        {
            if (group.Key is not MeshNode baseNode || !meshes.TryGetValue(baseNode, out var mesh) || mesh.Positions is null)
                continue;

            var targets = group.ToArray();
            var vertexCount = mesh.Positions.ElementCount;
            var deltas = new float[vertexCount * targets.Length * 3];

            for (var target = 0; target < targets.Length; target++)
            {
                var indices = targets[target].VertexIndices?.ToArray<int>() ?? [];
                var positions = targets[target].VertexPositions is { Type: CastPropertyType.Vector3 } vertexPositions ? vertexPositions.AsSpan<Vector3>() : [];

                for (var i = 0; i < indices.Length && i < positions.Length; i++)
                {
                    if ((uint)indices[i] < (uint)vertexCount)
                        (positions[i] - mesh.Positions.GetVector3(indices[i], 0)).CopyTo(deltas, (indices[i] * targets.Length + target) * 3);
                }
            }

            mesh.Morph = new Morph([.. targets.Select(target => target.Name)], new DataBuffer<float>(deltas, targets.Length, 3));
        }
    }

    public static void Write(ModelNode modelNode, MeshNode meshNode, Mesh mesh)
    {
        if (mesh.Morph is not { TargetCount: > 0, DeltaPositions: { } deltaPositions } morph || mesh.Positions is null)
            return;

        for (var target = 0; target < morph.TargetCount; target++)
        {
            var indices = new List<int>();
            var positions = new List<Vector3>();

            for (var vertex = 0; vertex < deltaPositions.ElementCount; vertex++)
            {
                var delta = deltaPositions.GetVector3(vertex, target);

                if (delta == Vector3.Zero)
                    continue;

                indices.Add(vertex);
                positions.Add(mesh.Positions.GetVector3(vertex, 0) + delta);
            }

            modelNode.AddNode(new BlendShapeNode { Name = morph.TargetNames[target], BaseShape = meshNode, VertexIndices = CastArrayProperty.CreateIndices<int>(CollectionsMarshal.AsSpan(indices)), VertexPositions = CastArrayProperty.Create<Vector3>(CollectionsMarshal.AsSpan(positions)) });
        }
    }
}
