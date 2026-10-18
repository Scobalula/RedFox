using RedFox.Graphics3D.Buffers;
using RedFox.Graphics3D.IO.SceneFile;

namespace RedFox.Graphics3D;

/// <summary>
/// Represents hair made of strands of connected particles.
/// </summary>
public class Hair : SceneNode
{
    /// <summary>
    /// Gets or sets the number of segments in each strand. A strand has one more particle than segments.
    /// </summary>
    public int[] StrandSegments { get; set; } = [];

    /// <summary>
    /// Gets or sets the world space particles of every strand, in order.
    /// </summary>
    public DataBuffer? Particles { get; set; }

    /// <summary>
    /// Gets or sets the material applied to the hair.
    /// </summary>
    public Material? Material { get; set; }

    /// <inheritdoc/>
    public override void Swap(SceneNode oldNode, SceneNode newNode)
    {
        base.Swap(oldNode, newNode);

        if (ReferenceEquals(Material, oldNode))
            Material = newNode as Material;
    }

    /// <inheritdoc/>
    public override void Write(BinaryWriter writer, IReadOnlyDictionary<SceneNode, int> nodeIndices)
    {
        base.Write(writer, nodeIndices);
        writer.WriteArray<int>(StrandSegments);
        writer.WriteBuffer(Particles);
        writer.WriteNode(Material, nodeIndices);
    }

    /// <inheritdoc/>
    public override void Read(BinaryReader reader, IReadOnlyList<SceneNode> nodes)
    {
        base.Read(reader, nodes);
        StrandSegments	= reader.ReadArray<int>();
        Particles		= reader.ReadBuffer();
        Material		= reader.ReadNode<Material>(nodes);
    }

    /// <inheritdoc/>
    protected override void RemapClonedReferences(IReadOnlyDictionary<SceneNode, SceneNode> clones)
    {
        if (GetType() != typeof(Hair))
        {
            base.RemapClonedReferences(clones);
            return;
        }

        if (Material is not null && clones.TryGetValue(Material, out SceneNode? materialCopy))
            Material = materialCopy as Material;
    }

    /// <inheritdoc/>
    protected override void OnCloned() => StrandSegments = [.. StrandSegments];
}
