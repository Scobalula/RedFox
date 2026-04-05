using Avalonia.OpenGL;
using Silk.NET.Core.Contexts;

namespace RedFox.Graphics3D.OpenGL.Controls;

internal sealed class AvaloniaNativeContext(GlInterface gl) : INativeContext
{
    private readonly GlInterface _gl = gl ?? throw new ArgumentNullException(nameof(gl));

    public IntPtr GetProcAddress(string proc, int? slot = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(proc);
        return _gl.GetProcAddress(proc);
    }

    public bool TryGetProcAddress(string proc, out IntPtr addr, int? slot = null)
    {
        addr = GetProcAddress(proc, slot);
        return addr != IntPtr.Zero;
    }

    public void Dispose()
    {
    }
}
