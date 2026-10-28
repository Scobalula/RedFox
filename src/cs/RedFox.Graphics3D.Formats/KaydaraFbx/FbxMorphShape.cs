namespace RedFox.Graphics3D.Formats.KaydaraFbx;

/// <summary>
/// Represents a blend shape read from an FBX file before it is expanded to mesh vertices.
/// </summary>
/// <param name="Name">The morph target name.</param>
/// <param name="Node">The node holding the shape's <c>Indexes</c>, <c>Vertices</c> and <c>Normals</c>.</param>
/// <param name="Weight">The initial target weight from zero to one.</param>
public readonly record struct FbxMorphShape(string Name, FbxNode Node, float Weight);
