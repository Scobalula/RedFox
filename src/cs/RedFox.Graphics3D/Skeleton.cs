namespace RedFox.Graphics3D;

/// <summary>
/// Represents the root node of a bone hierarchy.
/// </summary>
public class Skeleton : SceneNode
{
    /// <summary>
    /// Initializes a new instance of <see cref="Skeleton"/> with a generated name.
    /// </summary>
    public Skeleton() : base()
    {
    }

    /// <summary>
    /// Initializes a new instance of <see cref="Skeleton"/> with the specified name.
    /// </summary>
    /// <param name="name">The name to assign to the skeleton.</param>
    public Skeleton(string name) : base(name)
    {
    }

    /// <summary>
    /// Initializes a new instance of <see cref="Skeleton"/> with the specified name and flags.
    /// </summary>
    /// <param name="name">The name to assign to the skeleton.</param>
    /// <param name="flags">The flags that control node behavior.</param>
    public Skeleton(string name, SceneNodeFlags flags) : base(name, flags)
    {
    }
}
