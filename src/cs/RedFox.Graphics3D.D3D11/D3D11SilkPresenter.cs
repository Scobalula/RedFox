using RedFox.Graphics3D.Rendering;
using RedFox.Graphics3D.Silk;

namespace RedFox.Graphics3D.D3D11;

internal sealed class D3D11SilkPresenter : ISilkGraphicsPresenter
{
    private readonly D3D11Context _context;
    private readonly D3D11GraphicsDevice _graphicsDevice;
    private bool _disposed;

    public D3D11SilkPresenter(D3D11Context context, D3D11GraphicsDevice graphicsDevice)
    {
        _context = context ?? throw new ArgumentNullException(nameof(context));
        _graphicsDevice = graphicsDevice ?? throw new ArgumentNullException(nameof(graphicsDevice));
    }

    public IGraphicsDevice GraphicsDevice => _graphicsDevice;

    public void Resize(int width, int height)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        _context.Resize(width, height);
    }

    public void Present()
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        _context.Present();
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _graphicsDevice.Dispose();
        _disposed = true;
        GC.SuppressFinalize(this);
    }
}
