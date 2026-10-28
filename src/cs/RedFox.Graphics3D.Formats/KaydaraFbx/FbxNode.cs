namespace RedFox.Graphics3D.Formats.KaydaraFbx;

/// <summary>
/// Represents an FBX node containing ordered properties and child nodes.
/// </summary>
/// <param name="name">The FBX node name.</param>
public sealed class FbxNode(string name)
{
    /// <summary>
    /// Gets the FBX node name.
    /// </summary>
    public string Name { get; } = name;

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
    /// Creates and appends a child node with the specified name and a single property.
    /// </summary>
    /// <param name="name">The child node name.</param>
    /// <param name="property">The property stored on the child.</param>
    /// <returns>The created child node.</returns>
    public FbxNode AddChild(string name, FbxProperty property)
    {
        FbxNode child = AddChild(name);
        child.Properties.Add(property);
        return child;
    }

    /// <summary>
    /// Returns all direct child nodes with the provided name.
    /// </summary>
    /// <param name="name">The child name to match.</param>
    /// <returns>A sequence of matching child nodes.</returns>
    public IEnumerable<FbxNode> ChildrenNamed(string name)
    {
        foreach (FbxNode child in Children)
        {
            if (string.Equals(child.Name, name, StringComparison.Ordinal))
                yield return child;
        }
    }

    /// <summary>
    /// Returns the first direct child node with the provided name, if any.
    /// </summary>
    /// <param name="name">The child name to match.</param>
    /// <returns>The matching child node or <see langword="null"/> when none exists.</returns>
    public FbxNode? FirstChild(string name)
    {
        foreach (FbxNode child in Children)
        {
            if (string.Equals(child.Name, name, StringComparison.Ordinal))
                return child;
        }

        return null;
    }

    /// <summary>
    /// Enumerates this node and all descendant nodes in depth-first order.
    /// </summary>
    /// <returns>A depth-first sequence beginning with this node.</returns>
    public IEnumerable<FbxNode> EnumerateSubtree()
    {
        Stack<FbxNode> pending = new();
        pending.Push(this);

        while (pending.TryPop(out FbxNode? node))
        {
            yield return node;

            for (int i = node.Children.Count - 1; i >= 0; i--)
                pending.Push(node.Children[i]);
        }
    }

    /// <summary>
    /// Returns the first descendant node with the provided name, if any.
    /// </summary>
    /// <param name="name">The node name to match.</param>
    /// <returns>The first matching descendant node, or <see langword="null"/>.</returns>
    public FbxNode? FirstDescendant(string name)
    {
        foreach (FbxNode node in EnumerateSubtree())
        {
            if (!ReferenceEquals(node, this) && string.Equals(node.Name, name, StringComparison.Ordinal))
                return node;
        }

        return null;
    }

    /// <summary>
    /// Returns the first property of the first direct child with the provided name, if any.
    /// </summary>
    /// <param name="name">The child name to match.</param>
    /// <param name="property">The first property of the matching child.</param>
    /// <returns><see langword="true"/> when the child exists and has at least one property.</returns>
    public bool TryGetChildProperty(string name, out FbxProperty property)
    {
        if (FirstChild(name) is { Properties.Count: > 0 } child)
        {
            property = child.Properties[0];
            return true;
        }

        property = default;
        return false;
    }

    /// <summary>
    /// Returns the node's numeric content as 64-bit floats, reading either a single array property or a list of scalar properties.
    /// </summary>
    /// <returns>The numeric values.</returns>
    public double[] GetDoubles()
    {
        if (Properties.Count == 1 && Properties[0].IsArray)
            return Properties[0].ToDoubleArray();

        double[] values = new double[Properties.Count];

        for (int i = 0; i < values.Length; i++)
            values[i] = Properties[i].AsDouble();

        return values;
    }

    /// <summary>
    /// Returns the node's numeric content as 32-bit floats, reading either a single array property or a list of scalar properties.
    /// </summary>
    /// <returns>The numeric values.</returns>
    public float[] GetSingles()
    {
        if (Properties.Count == 1 && Properties[0].IsArray)
            return Properties[0].ToSingleArray();

        float[] values = new float[Properties.Count];

        for (int i = 0; i < values.Length; i++)
            values[i] = Properties[i].AsSingle();

        return values;
    }

    /// <summary>
    /// Returns the node's numeric content as 32-bit integers, reading either a single array property or a list of scalar properties.
    /// </summary>
    /// <returns>The numeric values.</returns>
    public int[] GetInt32s()
    {
        if (Properties.Count == 1 && Properties[0].IsArray)
            return Properties[0].ToInt32Array();

        int[] values = new int[Properties.Count];

        for (int i = 0; i < values.Length; i++)
            values[i] = Properties[i].AsInt32();

        return values;
    }

    /// <summary>
    /// Returns the node's numeric content as 64-bit integers, reading either a single array property or a list of scalar properties.
    /// </summary>
    /// <returns>The numeric values.</returns>
    public long[] GetInt64s()
    {
        if (Properties.Count == 1 && Properties[0].IsArray)
            return Properties[0].ToInt64Array();

        long[] values = new long[Properties.Count];

        for (int i = 0; i < values.Length; i++)
            values[i] = Properties[i].AsInt64();

        return values;
    }

    /// <inheritdoc/>
    public override string ToString()
    {
        return $"{Name} ({Properties.Count} properties, {Children.Count} children)";
    }
}
