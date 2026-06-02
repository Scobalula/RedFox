using RedFox.Graphics3D;

namespace RedFox.Graphics3D.IO;

/// <summary>
/// Provides a filtered view of a <see cref="Scene"/> for a single translation operation.
/// </summary>
/// <remarks>
/// Initializes a new <see cref="SceneTranslationSelection"/> for the specified scene and filter.
/// </remarks>
/// <param name="scene">The scene being translated.</param>
/// <param name="filter">The flags nodes must contain to be included in this selection.</param>
public sealed class SceneTranslationSelection(Scene scene, SceneNodeFlags filter)
{
    private readonly Dictionary<Type, Array> _descendantsByType = [];

    /// <summary>
    /// Gets the scene associated with this selection.
    /// </summary>
    public Scene Scene { get; } = scene ?? throw new ArgumentNullException(nameof(scene));

    /// <summary>
    /// Gets the node flag filter applied to this selection.
    /// </summary>
    public SceneNodeFlags Filter { get; } = filter;

    /// <summary>
    /// Determines whether the specified node is included by this selection. A node is included
    /// when it matches the filter directly, or when the filter selects by <see cref="SceneNodeFlags.Selected"/>
    /// or <see cref="SceneNodeFlags.SelectedHierarchy"/> and the node or any ancestor is marked
    /// <see cref="SceneNodeFlags.SelectedHierarchy"/>.
    /// </summary>
    /// <param name="node">The node to test.</param>
    /// <returns><see langword="true"/> if the node is included; otherwise, <see langword="false"/>.</returns>
    public bool Includes(SceneNode node)
    {
        ArgumentNullException.ThrowIfNull(node);

        if (node.Flags.HasFlag(Filter))
            return true;

        if (!Filter.HasFlag(SceneNodeFlags.Selected) && !Filter.HasFlag(SceneNodeFlags.SelectedHierarchy))
            return false;

        for (SceneNode? current = node; current is not null; current = current.Parent)
        {
            if (current.Flags.HasFlag(SceneNodeFlags.SelectedHierarchy))
                return true;
        }

        return false;
    }

    /// <summary>
    /// Gets all descendant nodes of the specified type included by this selection.
    /// Results are cached for the lifetime of the selection.
    /// </summary>
    /// <typeparam name="T">The node type to retrieve.</typeparam>
    /// <returns>An array containing matching descendant nodes.</returns>
    public T[] GetDescendants<T>() where T : SceneNode
    {
        if (_descendantsByType.TryGetValue(typeof(T), out Array? cached))
            return (T[])cached;

        T[] nodes = [.. EnumerateDescendants<T>()];
        _descendantsByType[typeof(T)] = nodes;
        return nodes;
    }

    /// <summary>
    /// Enumerates descendant nodes of the specified type included by this selection.
    /// </summary>
    /// <typeparam name="T">The node type to retrieve.</typeparam>
    /// <returns>An enumerable of matching descendant nodes.</returns>
    public IEnumerable<T> EnumerateDescendants<T>() where T : SceneNode =>
        Scene.EnumerateDescendants<T>(SceneNodeFlags.None).Where(Includes);

    /// <summary>
    /// Attempts to get the first node of the specified type included by this selection.
    /// </summary>
    /// <typeparam name="T">The node type to retrieve.</typeparam>
    /// <returns>The first matching node, or <see langword="null"/> if none are included.</returns>
    public T? TryGetFirstOfType<T>() where T : SceneNode => EnumerateDescendants<T>().FirstOrDefault();
}
