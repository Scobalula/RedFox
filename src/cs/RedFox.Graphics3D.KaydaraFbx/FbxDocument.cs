namespace RedFox.Graphics3D.KaydaraFbx;

/// <summary>
/// Represents a complete FBX document tree.
/// </summary>
public sealed class FbxDocument
{
    /// <summary>
    /// Gets or sets the detected or selected FBX format.
    /// </summary>
    public FbxFormat Format { get; set; }

    /// <summary>
    /// Gets or sets the FBX file version number.
    /// </summary>
    public int Version { get; set; } = 7400;

    /// <summary>
    /// Gets the root-level FBX nodes in document order.
    /// </summary>
    public List<FbxNode> Nodes { get; } = [];

    /// <summary>
    /// Returns all root nodes with the provided name.
    /// </summary>
    /// <param name="name">The node name to match.</param>
    /// <returns>A sequence of matching root nodes.</returns>
    public IEnumerable<FbxNode> NodesNamed(string name)
    {
        return Nodes.Where(node => string.Equals(node.Name, name, StringComparison.Ordinal));
    }

    /// <summary>
    /// Returns the first root node with the provided name, if any.
    /// </summary>
    /// <param name="name">The node name to match.</param>
    /// <returns>The first matching root node, or <see langword="null"/>.</returns>
    public FbxNode? FirstNode(string name)
    {
        return Nodes.FirstOrDefault(node => string.Equals(node.Name, name, StringComparison.Ordinal));
    }
}
