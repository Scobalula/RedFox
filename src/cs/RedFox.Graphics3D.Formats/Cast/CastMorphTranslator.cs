using Cast.NET;
using Cast.NET.Nodes;
using RedFox.Graphics3D.Buffers;
using System.Numerics;

namespace RedFox.Graphics3D.Formats.Cast;

internal static class CastMorphTranslator
{
    public static void Read(ModelNode modelNode, Dictionary<MeshNode, Mesh> meshes)
    {
        foreach (var group in modelNode.BlendShapes.GroupBy(blendShape => blendShape.GetFirstValue<ulong>("b", 0)))
        {
            if (!modelNode.TryGetChild<MeshNode>(group.Key, out var baseNode) || !meshes.TryGetValue(baseNode, out var mesh) || mesh.Positions is null)
                continue;

            var targets = group.ToArray();
            int vertexCount = mesh.Positions.ElementCount;
            var deltas = new float[vertexCount * targets.Length * 3];

            for (int t = 0; t < targets.Length; t++)
            {
                var indices = ReadIndices(targets[t].GetProperty("vi"));
                var positions = targets[t].GetProperty<CastArrayProperty<Vector3>>("vp").Values;

                for (int i = 0; i < indices.Length && i < positions.Count; i++)
                {
                    int vertexIndex = indices[i];

                    if ((uint)vertexIndex >= (uint)vertexCount)
                        continue;

                    var delta = positions[i] - mesh.Positions.GetVector3(vertexIndex, 0);
                    delta.CopyTo(deltas, ((vertexIndex * targets.Length) + t) * 3);
                }
            }

            mesh.Morph = new Morph([.. targets.Select(target => target.GetStringValue("n", string.Empty))], new DataBuffer<float>(deltas, targets.Length, 3));
        }
    }

    public static void Write(ModelNode modelNode, MeshNode meshNode, Mesh mesh)
    {
        if (mesh.Morph is not { TargetCount: > 0, DeltaPositions: { } deltaPositions } morph || mesh.Positions is null)
            return;

        meshNode.Hash = CastHasher.Compute($"{mesh.Name}_{modelNode.Children.Count}");

        for (int t = 0; t < morph.TargetCount; t++)
        {
            var indices = new List<uint>();
            var positions = new List<Vector3>();

            for (int v = 0; v < deltaPositions.ElementCount; v++)
            {
                var delta = deltaPositions.GetVector3(v, t);

                if (delta == Vector3.Zero)
                    continue;

                indices.Add((uint)v);
                positions.Add(mesh.Positions.GetVector3(v, 0) + delta);
            }

            var blendShapeNode = modelNode.AddNode<BlendShapeNode>();
            blendShapeNode.AddString("n", morph.TargetNames[t]);
            blendShapeNode.AddValue("b", meshNode.Hash);
            blendShapeNode.AddArray("vi", indices);
            blendShapeNode.AddArray("vp", positions);
        }
    }

    private static int[] ReadIndices(CastProperty property) => property switch
    {
        CastArrayProperty<byte> bytes => [.. bytes.Values.Select(value => (int)value)],
        CastArrayProperty<ushort> shorts => [.. shorts.Values.Select(value => (int)value)],
        CastArrayProperty<uint> ints => [.. ints.Values.Select(value => (int)value)],
        CastArrayProperty<int> ints => [.. ints.Values],
        _ => [],
    };
}
