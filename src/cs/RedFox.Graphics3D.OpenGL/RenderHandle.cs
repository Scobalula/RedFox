using Silk.NET.OpenGL;

namespace RedFox.Graphics3D.OpenGL;

public abstract class RenderHandle : IDisposable
{
    private bool _disposed;
    private GL? _gl;

    public void Initialize(GL gl)
    {
        _gl = gl;
        OnInitialize(gl);
    }

    public void Update(GL gl, float deltaTime)
    {
        OnUpdate(gl, deltaTime);
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;

        if (_gl is not null)
            OnDispose(_gl);

        _gl = null;
        GC.SuppressFinalize(this);
    }

    protected virtual void OnInitialize(GL gl) { }
    protected virtual void OnUpdate(GL gl, float deltaTime) { }
    protected virtual void OnDispose(GL gl) { }
}
