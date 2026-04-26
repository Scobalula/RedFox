namespace RedFox.Graphics3D;

/// <summary>
/// Provides data for scene graph change notifications.
/// </summary>
public sealed class SceneChangedEventArgs : EventArgs
{
    /// <summary>
    /// Initializes a new instance of the <see cref="SceneChangedEventArgs"/> class.
    /// </summary>
    /// <param name="kind">The kind of scene graph change.</param>
    /// <param name="node">The node affected by the change, when applicable.</param>
    /// <param name="version">The scene version after the change.</param>
    public SceneChangedEventArgs(SceneChangeKind kind, SceneNode? node, long version)
    {
        Kind = kind;
        Node = node;
        Version = version;
    }

    /// <summary>
    /// Gets the kind of scene graph change.
    /// </summary>
    public SceneChangeKind Kind { get; }

    /// <summary>
    /// Gets the node affected by the change, when applicable.
    /// </summary>
    public SceneNode? Node { get; }

    /// <summary>
    /// Gets the scene version after the change.
    /// </summary>
    public long Version { get; }
}
