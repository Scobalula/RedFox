namespace RedFox.Graphics3D;

/// <summary>
/// Represents a visible scene-graph constraint node imported from an external format such as FBX.
/// </summary>
public abstract class ConstraintNode(string name, SceneNode constrainedNode, SceneNode sourceNode) : SceneNode(name)
{
    /// <summary>
    /// Gets or sets the node whose transform is constrained.
    /// </summary>
    public SceneNode ConstrainedNode { get; set; } = constrainedNode;

    /// <summary>
    /// Gets or sets the source node that drives the constraint.
    /// </summary>
    public SceneNode SourceNode { get; set; } = sourceNode;

    /// <summary>
    /// Gets or sets the normalized constraint weight.
    /// </summary>
    public float Weight { get; set; } = 1.0f;

    /// <inheritdoc/>
    public override void Swap(SceneNode oldNode, SceneNode newNode)
    {
        base.Swap(oldNode, newNode);

        if (ReferenceEquals(ConstrainedNode, oldNode))
            ConstrainedNode = newNode;
        if (ReferenceEquals(SourceNode, oldNode))
            SourceNode = newNode;
    }

    /// <summary>
    /// Creates the runtime solver equivalent for this imported constraint node.
    /// </summary>
    /// <returns>The runtime solver representation.</returns>
    public abstract AnimationSamplerSolver CreateSolver();
}