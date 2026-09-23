using System;
using System.Numerics;

namespace RedFox.Graphics3D.Rendering.Handles;

/// <summary>
/// Provides a common base for render handles that own GPU resources.
/// </summary>
internal abstract class RenderHandle : IRenderHandle
{
    private bool _disposed;

    /// <inheritdoc/>
    public virtual bool RequiresPerFrameUpdate => true;

    /// <inheritdoc/>
    public virtual RenderHandleFlags Flags => RenderHandleFlags.None;

    /// <inheritdoc/>
    public abstract void Update(ICommandList commandList);

    /// <inheritdoc/>
    public abstract void Render(ICommandList commandList,
        RenderFlags phase,
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
    /// Releases handle-specific GPU resources.
    /// </summary>
    protected virtual void ReleaseCore()
    {
    }
}