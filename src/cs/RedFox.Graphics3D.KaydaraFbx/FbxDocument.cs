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

    /// <summary>
    /// Enumerates all root nodes and their descendants in depth-first order.
    /// </summary>
    /// <returns>A depth-first sequence over the full document tree.</returns>
    public IEnumerable<FbxNode> EnumerateNodesDepthFirst()
    {
        for (int i = 0; i < Nodes.Count; i++)
        {
            foreach (FbxNode node in Nodes[i].EnumerateSubtree())
            {
                yield return node;
            }
        }
    }

    /// <summary>
    /// Returns the first node anywhere in the document tree with the provided name, if any.
    /// </summary>
    /// <param name="name">The node name to match.</param>
    /// <returns>The first matching node, or <see langword="null"/>.</returns>
    public FbxNode? FirstNodeRecursive(string name)
    {
        return EnumerateNodesDepthFirst().FirstOrDefault(node => string.Equals(node.Name, name, StringComparison.Ordinal));
    }
}
