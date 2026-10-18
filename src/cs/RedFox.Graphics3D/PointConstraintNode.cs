using System.Numerics;
using RedFox.Graphics3D.Solvers;
using RedFox.Graphics3D.IO.SceneFile;

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

    /// <inheritdoc/>
    public override void Write(BinaryWriter writer, IReadOnlyDictionary<SceneNode, int> nodeIndices)
    {
        base.Write(writer, nodeIndices);
        writer.WriteValue(TranslationOffset);
        writer.Write(SkipX);
        writer.Write(SkipY);
        writer.Write(SkipZ);
    }

    /// <inheritdoc/>
    public override void Read(BinaryReader reader, IReadOnlyList<SceneNode> nodes)
    {
        base.Read(reader, nodes);
        TranslationOffset	= reader.ReadValue<Vector3>();
        SkipX				= reader.ReadBoolean();
        SkipY				= reader.ReadBoolean();
        SkipZ				= reader.ReadBoolean();
    }

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
