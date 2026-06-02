using System;

namespace RedFox.Graphics3D;

/// <summary>
/// Defines where duplicate scene nodes are searched for when committing nodes into a scene.
/// </summary>
[Flags]
public enum SceneNodeMatchScope
{
    /// <summary>
    /// No duplicate checking is performed; incoming nodes are always added.
    /// </summary>
    None = 0,

    /// <summary>
    /// Duplicates are matched against the immediate children of the target parent.
    /// </summary>
    Siblings = 1,

    /// <summary>
    /// Duplicates are matched against all descendants of the target parent.
    /// </summary>
    Descendants = 2,

    /// <summary>
    /// Duplicates are matched against every node in the target scene.
    /// </summary>
    WholeScene = 4,
}
