using System.Numerics;

namespace RedFox.Graphics3D.Solvers;

/// <summary>
/// Represents a Maya-style orient constraint that drives only the rotation of a constrained node from a source node.
/// </summary>
public class OrientConstraint(string name, SceneNode constrainedNode, SceneNode sourceNode) : AnimationSamplerSolver(name)
{
    /// <summary>
    /// Gets or sets the node whose rotation is driven by this constraint.
    /// </summary>
    public SceneNode ConstrainedNode { get; set; } = constrainedNode;

    /// <summary>
    /// Gets or sets the source node whose active rotation is used as the orient-constraint driver.
    /// </summary>
    public SceneNode SourceNode { get; set; } = sourceNode;

    /// <summary>
    /// Gets or sets the local rotation offset preserved between the source and constrained nodes.
    /// </summary>
    public Quaternion RotationOffset { get; set; } = Quaternion.Identity;

    /// <summary>
    /// Solves the orient constraint by matching the constrained node rotation to the source node with the configured offset.
    /// </summary>
    /// <param name="time">The current frame time.</param>
    protected override void OnSolve(float time)
    {
        _ = time;

        Quaternion targetWorldRotation = Quaternion.Normalize(SourceNode.GetActiveWorldRotation() * RotationOffset);
        Quaternion blendedWorldRotation = Quaternion.Slerp(ConstrainedNode.GetActiveWorldRotation(), targetWorldRotation, CurrentWeight);

        if (ConstrainedNode.Parent is not null)
        {
            Quaternion parentWorldRotation = ConstrainedNode.Parent.GetActiveWorldRotation();
            Quaternion localRotation = Quaternion.Normalize(Quaternion.Conjugate(parentWorldRotation) * blendedWorldRotation);
            ConstrainedNode.LiveTransform.LocalRotation = localRotation;
            ConstrainedNode.LiveTransform.WorldRotation = null;
            return;
        }

        ConstrainedNode.LiveTransform.WorldRotation = blendedWorldRotation;
        ConstrainedNode.LiveTransform.LocalRotation = null;
    }
}