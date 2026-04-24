namespace RedFox.Graphics3D.Rendering.Hosting;

/// <summary>
/// Represents a windowing host that owns a scene renderer and its main loop.
/// </summary>
public interface IRendererHost : IDisposable
{
    /// <summary>
    /// Gets the active scene renderer.
    /// </summary>
    SceneRenderer Renderer { get; }

    /// <summary>
    /// Runs the host render loop.
    /// </summary>
    void Run();
}