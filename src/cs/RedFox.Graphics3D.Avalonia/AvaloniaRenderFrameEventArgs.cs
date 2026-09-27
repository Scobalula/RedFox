using RedFox.Graphics3D.Rendering;

namespace RedFox.Graphics3D.Avalonia;

/// <summary>
/// Provides data for an Avalonia OpenGL renderer frame.
/// </summary>
/// <param name="renderer">The active scene renderer.</param>
/// <param name="scene">The scene rendered this frame.</param>
/// <param name="camera">The camera rendered this frame.</param>
/// <param name="elapsedTime">The elapsed frame time.</param>
/// <param name="renderDuration">The time spent updating and rendering the scene.</param>
public sealed class AvaloniaRenderFrameEventArgs(SceneRenderer renderer, Scene scene, Camera camera, TimeSpan elapsedTime, TimeSpan renderDuration) : EventArgs
{
    /// <summary>
    /// Gets the active scene renderer.
    /// </summary>
    public SceneRenderer Renderer { get; } = renderer ?? throw new ArgumentNullException(nameof(renderer));

    /// <summary>
    /// Gets the scene rendered this frame.
    /// </summary>
    public Scene Scene { get; } = scene ?? throw new ArgumentNullException(nameof(scene));

    /// <summary>
    /// Gets the camera rendered this frame.
    /// </summary>
    public Camera Camera { get; } = camera ?? throw new ArgumentNullException(nameof(camera));

    /// <summary>
    /// Gets the elapsed frame time.
    /// </summary>
    public TimeSpan ElapsedTime { get; } = elapsedTime;

    /// <summary>
    /// Gets the time spent updating and rendering the scene.
    /// </summary>
    public TimeSpan RenderDuration { get; } = renderDuration;
}
