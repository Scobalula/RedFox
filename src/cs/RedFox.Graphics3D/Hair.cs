using RedFox.Graphics3D.Buffers;

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
    protected override void OnCloned() => StrandSegments = [.. StrandSegments];
}
