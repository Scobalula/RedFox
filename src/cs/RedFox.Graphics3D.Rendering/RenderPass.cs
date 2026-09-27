using System;

namespace RedFox.Graphics3D.Rendering;

/// <summary>
/// Optional convenience base class for <see cref="IRenderPass"/> implementations that do not need
/// resize handling or initialization. Implementations override <see cref="ExecutePass"/>.
/// </summary>
public abstract class RenderPass : IRenderPass
{
    private bool _disposed;

    /// <inheritdoc/>
    public abstract RenderPassPhase Phase { get; }

    /// <summary>
    /// Gets or sets a value indicating whether the pass should execute.
    /// </summary>
    public bool Enabled { get; set; } = true;

    /// <inheritdoc/>
    public virtual void Initialize()
    {
    }

    /// <inheritdoc/>
    public virtual void Resize(int width, int height)
    {
    }

    /// <inheritdoc/>
    public void Execute(RenderFrameContext context)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        ArgumentNullException.ThrowIfNull(context);
        ExecutePass(context);
    }

    /// <inheritdoc/>
    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        DisposeResources();
        _disposed = true;
        GC.SuppressFinalize(this);
    }

    /// <summary>
    /// Performs the pass's work. Called only when <see cref="Enabled"/> is <see langword="true"/>.
    /// </summary>
    /// <param name="context">The frame context.</param>
    protected abstract void ExecutePass(RenderFrameContext context);

    /// <summary>
    /// Releases pass-owned resources. Default implementation is a no-op.
    /// </summary>
    protected virtual void DisposeResources()
    {
    }

    /// <summary>
    /// Throws when the pass has been disposed.
    /// </summary>
    protected void ThrowIfDisposed()
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
    }
}
