using RedFox.Graphics3D.Rendering;
using RedFox.Graphics3D.Silk;

namespace RedFox.Graphics3D.OpenGL;

internal sealed class OpenGlSilkPresenter(OpenGlGraphicsDevice graphicsDevice) : ISilkGraphicsPresenter
{
    private readonly OpenGlGraphicsDevice _graphicsDevice = graphicsDevice;
    private bool _disposed;

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
