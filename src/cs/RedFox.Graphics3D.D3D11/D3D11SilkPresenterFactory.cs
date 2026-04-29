using RedFox.Graphics3D.Silk;
using Silk.NET.Windowing;

namespace RedFox.Graphics3D.D3D11;

/// <summary>
/// Creates Direct3D 11 renderer presenters for the shared Silk renderer host.
/// </summary>
public sealed class D3D11SilkPresenterFactory : ISilkGraphicsPresenterFactory
{
    /// <summary>
    /// Configures the Silk window for a native Direct3D 11 swap chain.
    /// </summary>
    /// <param name="options">The window options to configure.</param>
    public void ConfigureWindowOptions(ref WindowOptions options)
    {
        options.API = new GraphicsAPI(ContextAPI.None, new APIVersion(0, 0));
        options.ShouldSwapAutomatically = false;
    }

    /// <summary>
    /// Creates a Direct3D 11 renderer presenter for the loaded window.
    /// </summary>
    /// <param name="window">The loaded Silk window.</param>
    /// <returns>The created renderer presenter.</returns>
    public ISilkGraphicsPresenter CreatePresenter(IWindow window)
    {
        ArgumentNullException.ThrowIfNull(window);
        D3D11Context context = D3D11Context.Create(window);
        return new D3D11SilkPresenter(context, new D3D11GraphicsDevice(context));
    }
}
