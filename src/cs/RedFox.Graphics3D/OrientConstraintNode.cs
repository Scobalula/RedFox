using System.Numerics;
using RedFox.Graphics3D.Solvers;

namespace RedFox.Graphics3D;

/// <summary>
/// Represents a visible Maya-style orient constraint node in the scene graph.
/// </summary>
public sealed class OrientConstraintNode(string name, SceneNode constrainedNode, SceneNode sourceNode) : ConstraintNode(name, constrainedNode, sourceNode)
{
    /// <summary>
    /// Gets or sets the local rotation offset preserved between the source and constrained nodes.
    /// </summary>
    public Quaternion RotationOffset { get; set; } = Quaternion.Identity;

    /// <summary>
    /// Creates the runtime solver equivalent for this orient constraint node.
    /// </summary>
    /// <returns>The runtime orient-constraint solver.</returns>
    public override AnimationSamplerSolver CreateSolver()
        => new OrientConstraint(Name, ConstrainedNode, SourceNode)
        {
            CurrentWeight = Weight,
            RotationOffset = RotationOffset,
        };
}