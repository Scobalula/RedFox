namespace RedFox.Graphics3D;

/// <summary>
/// Defines how a duplicate node is resolved when committing nodes into a scene.
/// </summary>
public enum SceneMergeStrategy
{
    /// <summary>
    /// Throw a <see cref="SceneNodeDuplicateException"/> when a duplicate is encountered.
    /// </summary>
    Throw,

    /// <summary>
    /// Keep the existing node and discard the incoming node, redirecting references to the existing node.
    /// </summary>
    Skip,

    /// <summary>
    /// Remove the existing node and replace it with the incoming node, redirecting references to the incoming node.
    /// </summary>
    Replace,

    /// <summary>
    /// Keep the existing node, merge the incoming node's children into it, and redirect references to the existing node.
    /// </summary>
    Merge,

    /// <summary>
    /// Rename the incoming node to a unique name and add it alongside the existing node.
    /// </summary>
    Rename,
}
