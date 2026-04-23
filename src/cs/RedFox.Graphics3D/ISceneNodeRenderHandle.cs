namespace RedFox.Graphics3D;

/// <summary>
/// Represents a renderer-specific handle attached to a scene node.
/// Implementations are backend-defined and may carry GPU resources or cached render data.
/// </summary>
public interface ISceneNodeRenderHandle : IDisposable
{
    /// <summary>
    /// Updates render state based on the associated scene node.
    /// Called once per frame before rendering. Implementations should refresh
    /// cached geometry, transform data, or other frame-dependent state.
    /// </summary>
    void Update();

    /// <summary>
    /// Releases GPU resources and performs cleanup.
    /// After this is called, the handle should not be used further.
    /// </summary>
    void Release();
}
