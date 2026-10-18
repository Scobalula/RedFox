using System.Numerics;
using RedFox.Graphics3D.Solvers;
using RedFox.Graphics3D.IO.SceneFile;

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

    /// <inheritdoc/>
    public override void Write(BinaryWriter writer, IReadOnlyDictionary<SceneNode, int> nodeIndices)
    {
        base.Write(writer, nodeIndices);
        writer.WriteValue(TranslationOffset);
        writer.WriteValue(RotationOffset);
    }

    /// <inheritdoc/>
    public override void Read(BinaryReader reader, IReadOnlyList<SceneNode> nodes)
    {
        base.Read(reader, nodes);
        TranslationOffset	= reader.ReadValue<Vector3>();
        RotationOffset		= reader.ReadValue<Quaternion>();
    }

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
