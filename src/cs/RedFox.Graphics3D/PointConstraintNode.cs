using System.Numerics;
using RedFox.Graphics3D.Solvers;

namespace RedFox.Graphics3D;

/// <summary>
/// Represents a point constraint node that moves the constrained node to the source node's position.
/// </summary>
public sealed class PointConstraintNode(string name, SceneNode constrainedNode, SceneNode sourceNode) : ConstraintNode(name, constrainedNode, sourceNode)
{
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

    /// <summary>
    /// Creates the runtime solver equivalent for this point constraint node.
    /// </summary>
    /// <returns>The runtime point-constraint solver.</returns>
    public override AnimationSamplerSolver CreateSolver()
        => new PointConstraint(Name, ConstrainedNode, SourceNode)
        {
            CurrentWeight = Weight,
            TranslationOffset = TranslationOffset,
            SkipX = SkipX,
            SkipY = SkipY,
            SkipZ = SkipZ,
        };
}
