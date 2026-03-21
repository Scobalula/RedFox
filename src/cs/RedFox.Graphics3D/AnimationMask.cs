using System.Diagnostics;

namespace RedFox.Graphics3D;

/// <summary>
/// Defines a set of node names that an animation layer is allowed to affect.
/// When attached to an <see cref="AnimationSampler"/>, only nodes whose names
/// appear in the mask will receive blended values from that layer.
/// <para>
/// Pass <see langword="null"/> as the mask to affect all nodes (no filtering).
/// </para>
/// </summary>
[DebuggerDisplay("AnimationMask: {Name}, Entries = {_entries.Count}")]
public class AnimationMask
{
    private readonly HashSet<string> _entries;

    /// <summary>
    /// Gets the display name of this mask (e.g. "UpperBody", "LeftArm").
    /// </summary>
    public string Name { get; }

    /// <summary>
    /// Gets the number of node names in this mask.
    /// </summary>
    public int Count => _entries.Count;

    /// <summary>
    /// Initializes a new <see cref="AnimationMask"/> with the given name and
    /// case-insensitive node name matching.
    /// </summary>
    /// <param name="name">Descriptive name for this mask.</param>
    public AnimationMask(string name)
    {
        Name = name;
        _entries = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
    }

    /// <summary>
    /// Initializes a new <see cref="AnimationMask"/> pre-populated with node names.
    /// </summary>
    /// <param name="name">Descriptive name for this mask.</param>
    /// <param name="nodeNames">Initial set of node names to include.</param>
    public AnimationMask(string name, IEnumerable<string> nodeNames)
    {
        Name = name;
        _entries = new HashSet<string>(nodeNames, StringComparer.OrdinalIgnoreCase);
    }

    /// <summary>
    /// Adds a node name to the mask. Duplicate names are silently ignored.
    /// </summary>
    /// <param name="nodeName">The node name to include.</param>
    /// <returns>This mask for fluent chaining.</returns>
    public AnimationMask Include(string nodeName)
    {
        _entries.Add(nodeName);
        return this;
    }

    /// <summary>
    /// Adds multiple node names to the mask.
    /// </summary>
    /// <param name="nodeNames">The node names to include.</param>
    /// <returns>This mask for fluent chaining.</returns>
    public AnimationMask Include(IEnumerable<string> nodeNames)
    {
        foreach (var name in nodeNames)
            _entries.Add(name);
        return this;
    }

    /// <summary>
    /// Adds all descendant node names from the given root node to the mask.
    /// Useful for building masks from a skeleton hierarchy subtree.
    /// </summary>
    /// <param name="rootNode">The root node whose hierarchy should be included.</param>
    /// <returns>This mask for fluent chaining.</returns>
    public AnimationMask IncludeHierarchy(SceneNode rootNode)
    {
        foreach (var node in rootNode.EnumerateHierarchy())
            _entries.Add(node.Name);
        return this;
    }

    /// <summary>
    /// Removes a node name from the mask.
    /// </summary>
    /// <param name="nodeName">The node name to exclude.</param>
    /// <returns>This mask for fluent chaining.</returns>
    public AnimationMask Exclude(string nodeName)
    {
        _entries.Remove(nodeName);
        return this;
    }

    /// <summary>
    /// Tests whether the specified node name is included in this mask.
    /// </summary>
    /// <param name="nodeName">The node name to test.</param>
    /// <returns><see langword="true"/> if the name is in the mask; otherwise <see langword="false"/>.</returns>
    public bool Contains(string nodeName) => _entries.Contains(nodeName);

    /// <summary>
    /// Creates an inverted copy of this mask relative to a full hierarchy.
    /// The result contains all node names from <paramref name="allNodes"/> that
    /// are NOT in this mask.
    /// </summary>
    /// <param name="allNodes">All available node names in the hierarchy.</param>
    /// <returns>A new <see cref="AnimationMask"/> with the complement set.</returns>
    public AnimationMask CreateInverse(IEnumerable<string> allNodes)
    {
        var inverse = new AnimationMask($"{Name}_Inverse");
        foreach (var name in allNodes)
        {
            if (!_entries.Contains(name))
                inverse._entries.Add(name);
        }
        return inverse;
    }
}
