using System.Numerics;

namespace RedFox.Graphics3D.ActorX;

/// <summary>
/// A single ActorX animation key, holding the position and orientation of one bone at one frame.
/// Keys are stored frame-major: all bones for frame 0, then all bones for frame 1, and so on.
/// </summary>
/// <param name="Position">The bone's local position at this frame.</param>
/// <param name="Orientation">The bone's orientation at this frame, as stored in the file.</param>
/// <param name="Time">The frame duration hint.</param>
public readonly record struct ActorXAnimKey(Vector3 Position, Quaternion Orientation, float Time);
