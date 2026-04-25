using RedFox.Graphics3D.Rendering;
using RedFox.Graphics3D.Rendering.Hosting;

namespace RedFox.Graphics3D.D3D11;

/// <summary>
/// Represents the placeholder D3D11 renderer host for the backend skeleton.
/// </summary>
public sealed class D3D11RendererHost : IRendererHost
{
    /// <summary>
    /// Initializes a new instance of the <see cref="D3D11RendererHost"/> class.
    /// </summary>
    public D3D11RendererHost()
    {
    }

    /// <inheritdoc/>
    public SceneRenderer Renderer => throw D3D11BackendSkeleton.NotImplemented();

    /// <inheritdoc/>
    public void Run()
    {
        throw D3D11BackendSkeleton.NotImplemented();
    }

    /// <inheritdoc/>
    public void Dispose()
    {
        throw D3D11BackendSkeleton.NotImplemented();
    }
}