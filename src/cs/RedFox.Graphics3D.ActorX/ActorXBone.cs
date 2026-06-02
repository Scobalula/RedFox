using System.Numerics;

namespace RedFox.Graphics3D.ActorX;

/// <summary>
/// An ActorX reference-skeleton bone, combining identity, hierarchy, and reference-pose joint data.
/// The same record layout is shared by the PSK <c>REFSKELT</c> and PSA <c>BONENAMES</c> chunks.
/// </summary>
/// <param name="Name">The bone name.</param>
/// <param name="Flags">Bone flags, unused by most tools.</param>
/// <param name="ChildCount">The number of direct children declared for this bone.</param>
/// <param name="ParentIndex">The index of the parent bone; the root bone references itself.</param>
/// <param name="Orientation">The reference-pose orientation as stored in the file.</param>
/// <param name="Position">The reference-pose position relative to the parent.</param>
/// <param name="Length">The bone length hint.</param>
/// <param name="Size">The per-axis bone size hint.</param>
public readonly record struct ActorXBone(
    string Name,
    uint Flags,
    int ChildCount,
    int ParentIndex,
    Quaternion Orientation,
    Vector3 Position,
    float Length,
    Vector3 Size);
