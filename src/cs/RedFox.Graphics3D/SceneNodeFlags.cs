
namespace RedFox.Graphics3D;

/// <summary>
/// Specifies flags that control the behavior and state of a scene node.
/// </summary>
[Flags]
public enum SceneNodeFlags
{
    /// <summary>
    /// Inidicates that no special flags are set for this node.
    /// </summary>
    None = 0,

    /// <summary>
    /// Indicates that the node should not be drawn or rendered in the scene.
    /// </summary>
    NoDraw = 1,

    /// <summary>
    /// Indicates that no update operation should be performed.
    /// </summary>
    NoUpdate = 2,

    /// <summary>
    /// Gets or sets a value indicating whether the item is selected.
    /// </summary>
    Selected = 4,
}
