using RedFox.Graphics3D.Rendering;

namespace RedFox.Graphics3D.Avalonia;

/// <summary>
/// Provides data for an Avalonia OpenGL renderer frame.
/// </summary>
public sealed class AvaloniaRenderFrameEventArgs : EventArgs
{
    /// <summary>
    /// Initializes a new instance of the <see cref="AvaloniaRenderFrameEventArgs"/> class.
    /// </summary>
    /// <param name="renderer">The active scene renderer.</param>
    /// <param name="scene">The scene rendered this frame.</param>
    /// <param name="camera">The camera rendered this frame.</param>
    /// <param name="elapsedTime">The elapsed frame time.</param>
    public AvaloniaRenderFrameEventArgs(SceneRenderer renderer, Scene scene, Camera camera, TimeSpan elapsedTime)
    {
        Renderer = renderer ?? throw new ArgumentNullException(nameof(renderer));
        Scene = scene ?? throw new ArgumentNullException(nameof(scene));
        Camera = camera ?? throw new ArgumentNullException(nameof(camera));
        ElapsedTime = elapsedTime;
    }

    /// <summary>
    /// Gets the active scene renderer.
    /// </summary>
    public SceneRenderer Renderer { get; }

    /// <summary>
    /// Gets the scene rendered this frame.
    /// </summary>
    public Scene Scene { get; }

    /// <summary>
    /// Gets the camera rendered this frame.
    /// </summary>
    public Camera Camera { get; }

    /// <summary>
    /// Gets the elapsed frame time.
    /// </summary>
    public TimeSpan ElapsedTime { get; }
}
