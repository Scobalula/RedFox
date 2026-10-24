using System.Numerics;

namespace RedFox.Graphics3D.Rendering;

internal readonly record struct EnvironmentLight(Vector3 Direction, Vector3 Power, Vector3 MajorAxis, Vector2 HalfAngles);
