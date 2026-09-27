namespace RedFox.Graphics3D;

/// <summary>
/// Provides data for scene graph change notifications.
/// </summary>
/// <param name="kind">The kind of scene graph change.</param>
/// <param name="node">The node affected by the change, when applicable.</param>
/// <param name="version">The scene version after the change.</param>
public sealed class SceneChangedEventArgs(SceneChangeKind kind, SceneNode? node, long version) : EventArgs
{
    /// <summary>
    /// Gets the kind of scene graph change.
    /// </summary>
    public SceneChangeKind Kind { get; } = kind;

    /// <summary>
    /// Gets the node affected by the change, when applicable.
    /// </summary>
    public SceneNode? Node { get; } = node;

    /// <summary>
    /// Gets the scene version after the change.
    /// </summary>
    public long Version { get; } = version;
}
