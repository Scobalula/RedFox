namespace RedFox.Graphics3D.Gltf;

/// <summary>
/// Represents a glTF scene, which is a set of root nodes that form the visible scene graph.
/// </summary>
public sealed class GltfScene
{
    /// <summary>
    /// Gets or sets the optional name of the scene.
    /// </summary>
    public string? Name { get; set; }

    /// <summary>
    /// Gets or sets the indices of the root nodes in this scene.
    /// </summary>
    public int[] Nodes { get; set; } = [];
}
