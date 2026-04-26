namespace RedFox.Graphics3D;

/// <summary>
/// Identifies the kind of scene graph change that occurred.
/// </summary>
public enum SceneChangeKind
{
    /// <summary>
    /// A node was added to the scene graph.
    /// </summary>
    NodeAdded,

    /// <summary>
    /// A node was removed from the scene graph.
    /// </summary>
    NodeRemoved,

    /// <summary>
    /// The scene graph was cleared.
    /// </summary>
    Cleared,
}
