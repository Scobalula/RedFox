using System;
using System.Numerics;
using RedFox.Graphics3D.Rendering.Backend;

namespace RedFox.Graphics3D.Rendering.Handles;

/// <summary>
/// Provides a common base for render handles that own backend resources.
/// </summary>
internal abstract class RenderHandle : IRenderHandle
{
    private bool _disposed;

    /// <inheritdoc/>
    public abstract void Update(ICommandList commandList);

    /// <inheritdoc/>
    public abstract void Render(
        ICommandList commandList,
        RenderPhase phase,
        in Matrix4x4 view,
        in Matrix4x4 projection,
        in Matrix4x4 sceneAxis,
        Vector3 cameraPosition,
        Vector2 viewportSize);

    /// <inheritdoc/>
    public void Release()
    {
        if (_disposed)
        {
            return;
        }

        ReleaseCore();
        _disposed = true;
    }

    /// <inheritdoc/>
    public void Dispose()
    {
        Release();
        GC.SuppressFinalize(this);
    }

    /// <summary>
    /// Throws when the handle has already been released.
    /// </summary>
    protected void ThrowIfDisposed()
    {
        ObjectDisposedException.ThrowIf(_disposed, GetType().Name);
    }

    /// <summary>
    /// Releases handle-specific backend resources.
    /// </summary>
    protected virtual void ReleaseCore()
    {
    }
}