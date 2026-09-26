using System.Numerics;

namespace RedFox.Graphics3D.Solvers;

/// <summary>
/// Matches the world scale of a constrained node to the world scale of a source node multiplied by an offset.
/// </summary>
/// <param name="name">The name of the solver.</param>
/// <param name="constrainedNode">The node being scaled.</param>
/// <param name="sourceNode">The node whose scale is followed.</param>
public class ScaleConstraint(string name, SceneNode constrainedNode, SceneNode sourceNode) : AnimationSamplerSolver(name)
{
    /// <summary>
    /// Gets or sets the node being scaled.
    /// </summary>
    public SceneNode ConstrainedNode { get; set; } = constrainedNode;

    /// <summary>
    /// Gets or sets the node whose scale is followed.
    /// </summary>
    public SceneNode SourceNode { get; set; } = sourceNode;

    /// <summary>
    /// Gets or sets the offset multiplied with the source node's world scale.
    /// </summary>
    public Vector3 ScaleOffset { get; set; } = Vector3.One;

    /// <summary>
    /// Gets or sets whether the X axis is left unconstrained.
    /// </summary>
    public bool SkipX { get; set; }

    /// <summary>
    /// Gets or sets whether the Y axis is left unconstrained.
    /// </summary>
    public bool SkipY { get; set; }

    /// <summary>
    /// Gets or sets whether the Z axis is left unconstrained.
    /// </summary>
    public bool SkipZ { get; set; }

    /// <inheritdoc/>
    protected override void OnSolve(float time)
    {
        _ = time;

        Vector3 parentWorldScale = ConstrainedNode.Parent is SceneNode parent ? GetWorldScale(parent) : Vector3.One;
        Vector3 currentScale = ConstrainedNode.GetLiveLocalScale();
        Vector3 targetScale = GetWorldScale(SourceNode) * ScaleOffset / parentWorldScale;
        Vector3 constrainedTarget = new(SkipX ? currentScale.X : targetScale.X, SkipY ? currentScale.Y : targetScale.Y, SkipZ ? currentScale.Z : targetScale.Z);

        ConstrainedNode.LiveTransform.Scale = Vector3.Lerp(currentScale, constrainedTarget, CurrentWeight);
    }

    private static Vector3 GetWorldScale(SceneNode node) => Matrix4x4.Decompose(node.GetActiveWorldMatrix(), out Vector3 scale, out _, out _) ? scale : Vector3.One;
}
