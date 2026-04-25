using RedFox.Graphics3D.Rendering.Backend;
using RedFox.Graphics3D.Silk;

namespace RedFox.Graphics3D.OpenGL;

internal sealed class OpenGlSilkBackend : ISilkRendererBackend
{
    private readonly OpenGlGraphicsDevice _graphicsDevice;
    private bool _disposed;

    public OpenGlSilkBackend(OpenGlGraphicsDevice graphicsDevice)
    {
        _graphicsDevice = graphicsDevice ?? throw new ArgumentNullException(nameof(graphicsDevice));
    }

    public IGraphicsDevice GraphicsDevice => _graphicsDevice;

    public void Resize(int width, int height)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
    }

    public void Present()
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        GC.SuppressFinalize(this);
    }
}
