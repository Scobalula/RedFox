using System.Numerics;

namespace RedFox.Graphics3D.Avalonia;

/// <summary>
/// Describes one face of a view cube.
/// </summary>
/// <param name="Normal">The direction the face points.</param>
/// <param name="Right">The direction of the face's label text.</param>
/// <param name="Up">The direction of the top of the face's label.</param>
/// <param name="Label">The face's label.</param>
public readonly record struct ViewCubeFace(Vector3 Normal, Vector3 Right, Vector3 Up, string Label);
