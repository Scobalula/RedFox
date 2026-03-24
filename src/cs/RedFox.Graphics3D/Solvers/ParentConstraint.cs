using System.Numerics;

namespace RedFox.Graphics3D.Solvers;

/// <summary>
/// Represents a Maya-style parent constraint that drives a constrained node from a source node while preserving authored offsets.
/// </summary>
public class ParentConstraint(string name, SceneNode constrainedNode, SceneNode sourceNode) : AnimationSamplerSolver(name)
{
    /// <summary>
    /// Gets or sets the node whose transform is driven by this constraint.
    /// </summary>
    public SceneNode ConstrainedNode { get; set; } = constrainedNode;

    /// <summary>
    /// Gets or sets the source node whose active transform is used as the parent-constraint driver.
    /// </summary>
    public SceneNode SourceNode { get; set; } = sourceNode;

    /// <summary>
    /// Gets or sets the local translation offset preserved between the source and constrained nodes.
    /// </summary>
    public Vector3 TranslationOffset { get; set; } = Vector3.Zero;

    /// <summary>
    /// Gets or sets the local rotation offset preserved between the source and constrained nodes.
    /// </summary>
    public Quaternion RotationOffset { get; set; } = Quaternion.Identity;

    /// <summary>
    /// Solves the parent constraint by matching the constrained node to the source node with the configured offsets.
    /// </summary>
    /// <param name="time">The current frame time.</param>
    protected override void OnSolve(float time)
    {
        _ = time;

        Vector3 sourceWorldPosition = SourceNode.GetActiveWorldPosition();
        Quaternion sourceWorldRotation = SourceNode.GetActiveWorldRotation();
        Vector3 targetWorldPosition = sourceWorldPosition + Vector3.Transform(TranslationOffset, sourceWorldRotation);
        Quaternion targetWorldRotation = Quaternion.Normalize(sourceWorldRotation * RotationOffset);
        Vector3 blendedWorldPosition = Vector3.Lerp(ConstrainedNode.GetActiveWorldPosition(), targetWorldPosition, CurrentWeight);
        Quaternion blendedWorldRotation = Quaternion.Slerp(ConstrainedNode.GetActiveWorldRotation(), targetWorldRotation, CurrentWeight);

        if (ConstrainedNode.Parent is not null)
        {
            Quaternion parentWorldRotation = ConstrainedNode.Parent.GetActiveWorldRotation();
            Vector3 parentWorldPosition = ConstrainedNode.Parent.GetActiveWorldPosition();
            Vector3 localPosition = Vector3.Transform(blendedWorldPosition - parentWorldPosition, Quaternion.Conjugate(parentWorldRotation));
            Quaternion localRotation = Quaternion.Normalize(Quaternion.Conjugate(parentWorldRotation) * blendedWorldRotation);
            ConstrainedNode.LiveTransform.LocalPosition = localPosition;
            ConstrainedNode.LiveTransform.LocalRotation = localRotation;
            ConstrainedNode.LiveTransform.WorldPosition = null;
            ConstrainedNode.LiveTransform.WorldRotation = null;
            return;
        }

        ConstrainedNode.LiveTransform.WorldPosition = blendedWorldPosition;
        ConstrainedNode.LiveTransform.WorldRotation = blendedWorldRotation;
        ConstrainedNode.LiveTransform.LocalPosition = null;
        ConstrainedNode.LiveTransform.LocalRotation = null;
    }
}