
using System.Diagnostics;

namespace RedFox.Graphics3D
{
    /// <summary>
    /// Represents a single blend shape (morph target) that deforms a <see cref="Mesh"/>.
    /// The deformation deltas are stored in the mesh's <see cref="Mesh.DeltaPositions"/>
    /// buffer; this class tracks the target's name, index, and current weight.
    /// <para>
    /// As a <see cref="SceneNode"/>, blend shapes can live in the scene graph as
    /// children of the mesh they deform, enabling standard traversal and naming.
    /// </para>
    /// </summary>
    [DebuggerDisplay("BlendShape: {Name}, Index = {TargetIndex}, Weight = {Weight}")]
    public class BlendShape : SceneNode
    {
        /// <summary>
        /// Gets or sets the zero-based index of this morph target within the mesh's
        /// <see cref="Mesh.DeltaPositions"/> buffer (maps to the buffer's value index).
        /// </summary>
        public int TargetIndex { get; set; }

        /// <summary>
        /// Gets or sets the current blend weight for this target.
        /// <list type="bullet">
        ///   <item><description>0.0 — no deformation (base mesh)</description></item>
        ///   <item><description>1.0 — fully deformed to this target</description></item>
        /// </list>
        /// Values outside 0–1 are permitted for over-drive effects.
        /// </summary>
        public float Weight { get; set; }

        /// <summary>
        /// Gets or sets a reference to the mesh that owns this blend shape's
        /// delta data. May be <see langword="null"/> if the shape is defined
        /// independently of a specific mesh.
        /// </summary>
        public Mesh? OwnerMesh { get; set; }

        /// <summary>
        /// Initializes a new <see cref="BlendShape"/> with the given name and target index.
        /// </summary>
        /// <param name="name">Descriptive name (e.g. "Smile", "BlinkLeft").</param>
        /// <param name="targetIndex">Index into the mesh's delta positions buffer.</param>
        public BlendShape(string name, int targetIndex) : base(name)
        {
            TargetIndex = targetIndex;
        }

        /// <summary>
        /// Initializes a new <see cref="BlendShape"/> with name, index, and owner mesh.
        /// </summary>
        /// <param name="name">Descriptive name.</param>
        /// <param name="targetIndex">Index into the mesh's delta positions buffer.</param>
        /// <param name="ownerMesh">The mesh whose DeltaPositions buffer contains the deltas.</param>
        public BlendShape(string name, int targetIndex, Mesh ownerMesh) : base(name)
        {
            TargetIndex = targetIndex;
            OwnerMesh = ownerMesh;
        }
    }
}
