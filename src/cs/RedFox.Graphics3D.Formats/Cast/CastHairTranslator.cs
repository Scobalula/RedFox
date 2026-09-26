using CastNet;
using CastNet.Nodes;
using RedFox.Graphics3D.Buffers;
using RedFox.Graphics3D.Groups;

namespace RedFox.Graphics3D.Formats.Cast;

internal static class CastHairTranslator
{
    public static void Read(MeshGroup model, HairNode hairNode, Dictionary<MaterialNode, Material> materials)
    {
        var hair = model.AddNode<Hair>();

        hair.StrandSegments = hairNode.Segments?.ToArray<int>() ?? [];
        hair.Particles = hairNode.Particles is { Type: CastPropertyType.Vector3 } particles ? new DataBuffer<float>(particles.AsBytes().ToArray(), 1, 3) : null;
        hair.Material = hairNode.Material is MaterialNode materialNode ? materials.GetValueOrDefault(materialNode) : null;
    }

    public static void Write(ModelNode modelNode, Hair hair, Dictionary<Material, MaterialNode> materials)
    {
        modelNode.AddNode(new HairNode
        {
            Name = hair.Name,
            Segments = CastArrayProperty.CreateIndices<int>(hair.StrandSegments),
            Particles = hair.Particles is null ? null : CastMeshTranslator.CreateVector3Property(hair.Particles),
            Material = hair.Material is null ? null : materials[hair.Material],
        });
    }
}
