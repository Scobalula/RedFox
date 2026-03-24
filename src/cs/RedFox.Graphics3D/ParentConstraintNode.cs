using System.Numerics;
using RedFox.Graphics3D.Solvers;

namespace RedFox.Graphics3D;

/// <summary>
/// Represents a visible Maya-style parent constraint node in the scene graph.
/// </summary>
public sealed class ParentConstraintNode(string name, SceneNode constrainedNode, SceneNode sourceNode) : ConstraintNode(name, constrainedNode, sourceNode)
{
    /// <summary>
    /// Gets or sets the local translation offset preserved between the source and constrained nodes.
    /// </summary>
    public Vector3 TranslationOffset { get; set; } = Vector3.Zero;

    /// <summary>
    /// Gets or sets the local rotation offset preserved between the source and constrained nodes.
    /// </summary>
    public Quaternion RotationOffset { get; set; } = Quaternion.Identity;

    /// <summary>
    /// Creates the runtime solver equivalent for this parent constraint node.
    /// </summary>
    /// <returns>The runtime parent-constraint solver.</returns>
    public override AnimationSamplerSolver CreateSolver()
        => new ParentConstraint(Name, ConstrainedNode, SourceNode)
        {
            CurrentWeight = Weight,
            TranslationOffset = TranslationOffset,
            RotationOffset = RotationOffset,
        };
}