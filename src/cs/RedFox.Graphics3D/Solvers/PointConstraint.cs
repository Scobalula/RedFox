using System.Numerics;

namespace RedFox.Graphics3D.Solvers;

/// <summary>
/// Moves a constrained node to the world position of a source node plus an offset.
/// </summary>
/// <param name="name">The name of the solver.</param>
/// <param name="constrainedNode">The node being moved.</param>
/// <param name="sourceNode">The node whose position is followed.</param>
public class PointConstraint(string name, SceneNode constrainedNode, SceneNode sourceNode) : AnimationSamplerSolver(name)
{
    /// <summary>
    /// Gets or sets the node being moved.
    /// </summary>
    public SceneNode ConstrainedNode { get; set; } = constrainedNode;

    /// <summary>
    /// Gets or sets the node whose position is followed.
    /// </summary>
    public SceneNode SourceNode { get; set; } = sourceNode;

    /// <summary>
    /// Gets or sets the world space offset added to the source node's position.
    /// </summary>
    public Vector3 TranslationOffset { get; set; } = Vector3.Zero;

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

        Vector3 currentWorldPosition = ConstrainedNode.GetActiveWorldPosition();
        Vector3 targetWorldPosition = SourceNode.GetActiveWorldPosition() + TranslationOffset;
        Vector3 constrainedTarget = new(SkipX ? currentWorldPosition.X : targetWorldPosition.X, SkipY ? currentWorldPosition.Y : targetWorldPosition.Y, SkipZ ? currentWorldPosition.Z : targetWorldPosition.Z);
        Vector3 blendedWorldPosition = Vector3.Lerp(currentWorldPosition, constrainedTarget, CurrentWeight);

        if (ConstrainedNode.Parent is not null)
        {
            Quaternion parentWorldRotation = ConstrainedNode.Parent.GetActiveWorldRotation();
            Vector3 parentWorldPosition = ConstrainedNode.Parent.GetActiveWorldPosition();

            ConstrainedNode.LiveTransform.LocalPosition = Vector3.Transform(blendedWorldPosition - parentWorldPosition, Quaternion.Conjugate(parentWorldRotation));
            ConstrainedNode.LiveTransform.WorldPosition = null;
            return;
        }

        ConstrainedNode.LiveTransform.WorldPosition = blendedWorldPosition;
        ConstrainedNode.LiveTransform.LocalPosition = null;
    }
}
