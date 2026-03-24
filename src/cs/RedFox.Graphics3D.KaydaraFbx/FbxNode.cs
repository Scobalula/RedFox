namespace RedFox.Graphics3D.KaydaraFbx;

/// <summary>
/// Represents an FBX node containing ordered properties and child nodes.
/// </summary>
public sealed class FbxNode
{
    /// <summary>
    /// Initializes a new instance of the <see cref="FbxNode"/> class.
    /// </summary>
    /// <param name="name">The FBX node name.</param>
    public FbxNode(string name)
    {
        Name = name;
    }

    /// <summary>
    /// Gets the FBX node name.
    /// </summary>
    public string Name { get; }

    /// <summary>
    /// Gets the mutable collection of typed properties associated with this node.
    /// </summary>
    public List<FbxProperty> Properties { get; } = [];

    /// <summary>
    /// Gets the mutable collection of child nodes.
    /// </summary>
    public List<FbxNode> Children { get; } = [];

    /// <summary>
    /// Creates and appends a child node with the specified name.
    /// </summary>
    /// <param name="name">The child node name.</param>
    /// <returns>The created child node.</returns>
    public FbxNode AddChild(string name)
    {
        FbxNode child = new(name);
        Children.Add(child);
        return child;
    }

    /// <summary>
    /// Returns all direct child nodes with the provided name.
    /// </summary>
    /// <param name="name">The child name to match.</param>
    /// <returns>A sequence of matching child nodes.</returns>
    public IEnumerable<FbxNode> ChildrenNamed(string name)
    {
        return Children.Where(static child => child is not null).Where(child => string.Equals(child.Name, name, StringComparison.Ordinal));
    }

    /// <summary>
    /// Returns the first direct child node with the provided name, if any.
    /// </summary>
    /// <param name="name">The child name to match.</param>
    /// <returns>The matching child node or <see langword="null"/> when none exists.</returns>
    public FbxNode? FirstChild(string name)
    {
        return Children.FirstOrDefault(child => string.Equals(child.Name, name, StringComparison.Ordinal));
    }
}
