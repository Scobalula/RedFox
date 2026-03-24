namespace RedFox.Graphics3D;

/// <summary>
/// Represents a generic scene-graph container that preserves imported hierarchy when a node is neither clearly a model nor a skeleton.
/// </summary>
public class Group : SceneNode
{
    /// <summary>
    /// Initializes a new instance of <see cref="Group"/> with a generated name.
    /// </summary>
    public Group() : base()
    {
    }

    /// <summary>
    /// Initializes a new instance of <see cref="Group"/> with the specified name.
    /// </summary>
    /// <param name="name">The name to assign to the group.</param>
    public Group(string name) : base(name)
    {
    }

    /// <summary>
    /// Initializes a new instance of <see cref="Group"/> with the specified name and flags.
    /// </summary>
    /// <param name="name">The name to assign to the group.</param>
    /// <param name="flags">The flags that control node behavior.</param>
    public Group(string name, SceneNodeFlags flags) : base(name, flags)
    {
    }
}