using Silk.NET.Windowing;

namespace RedFox.Graphics3D.Silk;

/// <summary>
/// Creates API-specific renderer presenters for a shared Silk renderer host.
/// </summary>
public interface ISilkGraphicsPresenterFactory
{
    /// <summary>
    /// Configures Silk window options before the window is created.
    /// </summary>
    /// <param name="options">The window options to configure.</param>
    void ConfigureWindowOptions(ref WindowOptions options);

    /// <summary>
    /// Creates a renderer presenter for the loaded Silk window.
    /// </summary>
    /// <param name="window">The loaded Silk window.</param>
    /// <returns>The created renderer presenter.</returns>
    ISilkGraphicsPresenter CreatePresenter(IWindow window);
}
