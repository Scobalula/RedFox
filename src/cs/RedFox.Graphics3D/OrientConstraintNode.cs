using System.Numerics;
using RedFox.Graphics3D.Solvers;
using RedFox.Graphics3D.IO.SceneFile;

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
    /// Gets or sets whether rotation about the local X axis is left unconstrained.
    /// </summary>
    public bool SkipX { get; set; }

    /// <summary>
    /// Gets or sets whether rotation about the local Y axis is left unconstrained.
    /// </summary>
    public bool SkipY { get; set; }

    /// <summary>
    /// Gets or sets whether rotation about the local Z axis is left unconstrained.
    /// </summary>
    public bool SkipZ { get; set; }

    /// <inheritdoc/>
    public override void Write(BinaryWriter writer, IReadOnlyDictionary<SceneNode, int> nodeIndices)
    {
        base.Write(writer, nodeIndices);
        writer.WriteValue(RotationOffset);
        writer.Write(SkipX);
        writer.Write(SkipY);
        writer.Write(SkipZ);
    }

    /// <inheritdoc/>
    public override void Read(BinaryReader reader, IReadOnlyList<SceneNode> nodes)
    {
        base.Read(reader, nodes);
        RotationOffset	= reader.ReadValue<Quaternion>();
        SkipX			= reader.ReadBoolean();
        SkipY			= reader.ReadBoolean();
        SkipZ			= reader.ReadBoolean();
    }

    /// <summary>
    /// Creates the runtime solver equivalent for this orient constraint node.
    /// </summary>
    /// <returns>The runtime orient-constraint solver.</returns>
    public override AnimationSamplerSolver CreateSolver()
        => new OrientConstraint(Name, ConstrainedNode, SourceNode)
        {
            CurrentWeight = Weight,
            RotationOffset = RotationOffset,
            SkipX = SkipX,
            SkipY = SkipY,
            SkipZ = SkipZ,
        };
}
