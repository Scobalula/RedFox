using System.Numerics;

namespace RedFox.Graphics3D.Md5;

/// <summary>
/// Represents one parsed weight entry from the <c>mesh</c> section of an MD5 mesh file.
/// </summary>
/// <remarks>
/// A weight binds a vertex to a specific joint with a given bias and a position
/// expressed in that joint's local coordinate space.
/// </remarks>
/// <param name="jointIndex">The index of the joint that owns this weight.</param>
/// <param name="bias">
/// The scalar bias (0–1) that determines how much this weight contributes to
/// the final vertex position.
/// </param>
/// <param name="position">
/// The weight position expressed in the local space of the owning joint.
/// </param>
public readonly struct Md5Weight(int jointIndex, float bias, Vector3 position)
{
    /// <summary>
    /// Gets the index of the joint that owns this weight.
    /// </summary>
    public int JointIndex { get; } = jointIndex;

    /// <summary>
    /// Gets the scalar bias that determines the contribution of this weight
    /// to the final vertex position.
    /// </summary>
    public float Bias { get; } = bias;

    /// <summary>
    /// Gets the weight position in the local space of the owning joint.
    /// </summary>
    public Vector3 Position { get; } = position;
}
