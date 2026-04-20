using RedFox.Graphics3D;

namespace RedFox.Graphics3D.IO;

/// <summary>
/// Provides a filtered view of a <see cref="Scene"/> for a single translation operation.
/// </summary>
public sealed class SceneTranslationSelection
{
    private readonly Dictionary<Type, Array> _descendantsByType = [];

    /// <summary>
    /// Initializes a new <see cref="SceneTranslationSelection"/> for the specified scene and filter.
    /// </summary>
    /// <param name="scene">The scene being translated.</param>
    /// <param name="filter">The flags nodes must contain to be included in this selection.</param>
    public SceneTranslationSelection(Scene scene, SceneNodeFlags filter)
    {
        Scene = scene ?? throw new ArgumentNullException(nameof(scene));
        Filter = filter;
    }

    /// <summary>
    /// Gets the scene associated with this selection.
    /// </summary>
    public Scene Scene { get; }

    /// <summary>
    /// Gets the node flag filter applied to this selection.
    /// </summary>
    public SceneNodeFlags Filter { get; }

    /// <summary>
    /// Determines whether the specified node is included by this selection.
    /// </summary>
    /// <param name="node">The node to test.</param>
    /// <returns><see langword="true"/> if the node is included; otherwise, <see langword="false"/>.</returns>
    public bool Includes(SceneNode node)
    {
        ArgumentNullException.ThrowIfNull(node);
        return Filter == SceneNodeFlags.None || (node.Flags & Filter) == Filter;
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

        T[] nodes = Scene.GetDescendants<T>(Filter);
        _descendantsByType[typeof(T)] = nodes;
        return nodes;
    }

    /// <summary>
    /// Enumerates descendant nodes of the specified type included by this selection.
    /// </summary>
    /// <typeparam name="T">The node type to retrieve.</typeparam>
    /// <returns>An enumerable of matching descendant nodes.</returns>
    public IEnumerable<T> EnumerateDescendants<T>() where T : SceneNode => GetDescendants<T>();

    /// <summary>
    /// Attempts to get the first node of the specified type included by this selection.
    /// </summary>
    /// <typeparam name="T">The node type to retrieve.</typeparam>
    /// <returns>The first matching node, or <see langword="null"/> if none are included.</returns>
    public T? TryGetFirstOfType<T>() where T : SceneNode => Scene.TryGetFirstOfType<T>(Filter);
}
