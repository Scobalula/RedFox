using RedFox.Graphics3D.Rendering.Backend;

namespace RedFox.Graphics3D.D3D11;

/// <summary>
/// Represents the placeholder D3D11 GPU texture resource for the backend skeleton.
/// </summary>
public sealed class D3D11Texture : IGpuTexture
{
    internal D3D11Texture()
    {
    }

    /// <inheritdoc/>
    public bool IsDisposed => throw D3D11BackendSkeleton.NotImplemented();

    /// <inheritdoc/>
    public void Dispose()
    {
        throw D3D11BackendSkeleton.NotImplemented();
    }
}