using RedFox.Graphics3D.Rendering;

namespace RedFox.Graphics3D.Silk;

/// <summary>
/// Represents API-specific presentation state owned by a shared Silk renderer host.
/// </summary>
public interface ISilkGraphicsPresenter : IDisposable
{
    /// <summary>
    /// Gets the graphics device exposed by the presenter.
    /// </summary>
    IGraphicsDevice GraphicsDevice { get; }

    /// <summary>
    /// Resizes API-specific presentation resources.
    /// </summary>
    /// <param name="width">The framebuffer width in pixels.</param>
    /// <param name="height">The framebuffer height in pixels.</param>
    void Resize(int width, int height);

    /// <summary>
    /// Presents the frame after the renderer callback has completed.
    /// </summary>
    void Present();
}
