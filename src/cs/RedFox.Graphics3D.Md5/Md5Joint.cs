using System.Numerics;

namespace RedFox.Graphics3D.Md5;

/// <summary>
/// Represents one parsed joint entry from the <c>joints</c> section of an MD5 mesh file.
/// </summary>
/// <remarks>
/// Initializes a new <see cref="Md5Joint"/> with the given hierarchy and bind-pose data.
/// </remarks>
/// <param name="name">The name of the joint.</param>
/// <param name="parentIndex">
/// The index of the parent joint, or <c>-1</c> for root joints.
/// </param>
/// <param name="position">The bind-pose position in object space.</param>
/// <param name="orientation">
/// The bind-pose orientation quaternion. The W component is reconstructed
/// from the stored X, Y, and Z values.
/// </param>
public readonly struct Md5Joint(string name, int parentIndex, Vector3 position, Quaternion orientation)
{
    /// <summary>
    /// Gets the name of the joint.
    /// </summary>
    public string Name { get; } = name;

    /// <summary>
    /// Gets the index of the parent joint, or <c>-1</c> for root joints.
    /// </summary>
    public int ParentIndex { get; } = parentIndex;

    /// <summary>
    /// Gets the bind-pose position in object space.
    /// </summary>
    public Vector3 Position { get; } = position;

    /// <summary>
    /// Gets the bind-pose orientation quaternion.
    /// </summary>
    public Quaternion Orientation { get; } = orientation;
}
