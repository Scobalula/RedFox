
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
    None,

    /// <summary>
    /// Indicates that the node should not be drawn or rendered in the scene.
    /// </summary>
    NoDraw,

    /// <summary>
    /// Indicates that no update operation should be performed.
    /// </summary>
    NoUpdate,

    /// <summary>
    /// Gets or sets a value indicating whether the item is selected.
    /// </summary>
    Selected,
}
