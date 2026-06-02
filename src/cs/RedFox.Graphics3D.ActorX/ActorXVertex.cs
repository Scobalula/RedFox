using System.Numerics;

namespace RedFox.Graphics3D.ActorX;

/// <summary>
/// An ActorX wedge (texture vertex): a point reference paired with its texture coordinate and material assignment.
/// Multiple wedges may share a single point when the same position needs distinct UVs or materials.
/// </summary>
/// <param name="PointIndex">The index of the referenced position within the points chunk.</param>
/// <param name="Uv">The texture coordinate for this wedge.</param>
/// <param name="MaterialIndex">The index of the material assigned to this wedge.</param>
public readonly record struct ActorXVertex(int PointIndex, Vector2 Uv, byte MaterialIndex);
