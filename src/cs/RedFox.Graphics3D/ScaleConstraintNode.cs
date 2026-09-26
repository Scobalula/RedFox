using System.Numerics;
using RedFox.Graphics3D.Solvers;

namespace RedFox.Graphics3D;

/// <summary>
/// Represents a scale constraint node that matches the constrained node's world scale to the source node's world scale.
/// </summary>
public sealed class ScaleConstraintNode(string name, SceneNode constrainedNode, SceneNode sourceNode) : ConstraintNode(name, constrainedNode, sourceNode)
{
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

    /// <summary>
    /// Creates the runtime solver equivalent for this scale constraint node.
    /// </summary>
    /// <returns>The runtime scale-constraint solver.</returns>
    public override AnimationSamplerSolver CreateSolver()
        => new ScaleConstraint(Name, ConstrainedNode, SourceNode)
        {
            CurrentWeight = Weight,
            ScaleOffset = ScaleOffset,
            SkipX = SkipX,
            SkipY = SkipY,
            SkipZ = SkipZ,
        };
}
