using System;
using System.Collections.Generic;

namespace RedFox.Rendering;

/// <summary>
/// Default <see cref="IRenderPipeline"/> implementation. Passes are executed in ascending
/// <see cref="RenderPassPhase"/> order; ties are broken by insertion order.
/// </summary>
public sealed class RenderPipeline : IRenderPipeline
{
    private readonly List<IRenderPass> _passes = new();
    private readonly List<IRenderPass> _executionOrder = new();
    private bool _executionOrderDirty;
    private bool _disposed;

    /// <inheritdoc/>
    public IReadOnlyList<IRenderPass> Passes => _passes;

    /// <inheritdoc/>
    public void Add(IRenderPass pass)
    {
        ThrowIfDisposed();
        ArgumentNullException.ThrowIfNull(pass);
        _passes.Add(pass);
        _executionOrderDirty = true;
    }

    /// <inheritdoc/>
    public bool Remove(IRenderPass pass)
    {
        ThrowIfDisposed();
        ArgumentNullException.ThrowIfNull(pass);
        bool removed = _passes.Remove(pass);
        if (removed)
        {
            _executionOrderDirty = true;
        }

        return removed;
    }

    /// <inheritdoc/>
    public int RemoveAll<TPass>() where TPass : IRenderPass
    {
        ThrowIfDisposed();
        int removed = _passes.RemoveAll(static pass => pass is TPass);
        if (removed > 0)
        {
            _executionOrderDirty = true;
        }

        return removed;
    }

    /// <inheritdoc/>
    public void Initialize()
    {
        ThrowIfDisposed();
        for (int i = 0; i < _passes.Count; i++)
        {
            _passes[i].Initialize();
        }
    }

    /// <inheritdoc/>
    public void Resize(int width, int height)
    {
        ThrowIfDisposed();
        for (int i = 0; i < _passes.Count; i++)
        {
            _passes[i].Resize(width, height);
        }
    }

    /// <inheritdoc/>
    public void Execute(RenderFrameContext context)
    {
        ThrowIfDisposed();
        ArgumentNullException.ThrowIfNull(context);

        EnsureExecutionOrder();

        for (int i = 0; i < _executionOrder.Count; i++)
        {
            IRenderPass pass = _executionOrder[i];
            if (!pass.Enabled)
            {
                continue;
            }

            pass.Execute(context);
        }
    }

    /// <inheritdoc/>
    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        for (int i = 0; i < _passes.Count; i++)
        {
            _passes[i].Dispose();
        }

        _passes.Clear();
        _executionOrder.Clear();
        _disposed = true;
    }

    private void EnsureExecutionOrder()
    {
        if (!_executionOrderDirty)
        {
            return;
        }

        _executionOrder.Clear();
        _executionOrder.AddRange(_passes);
        // Stable sort by phase (List<T>.Sort is not stable; emulate with index-tagged comparison).
        int[] indices = new int[_executionOrder.Count];
        for (int i = 0; i < indices.Length; i++)
        {
            indices[i] = i;
        }

        IRenderPass[] snapshot = _executionOrder.ToArray();
        Array.Sort(indices, (left, right) =>
        {
            int phaseCompare = ((int)snapshot[left].Phase).CompareTo((int)snapshot[right].Phase);
            return phaseCompare != 0 ? phaseCompare : left.CompareTo(right);
        });

        _executionOrder.Clear();
        for (int i = 0; i < indices.Length; i++)
        {
            _executionOrder.Add(snapshot[indices[i]]);
        }

        _executionOrderDirty = false;
    }

    private void ThrowIfDisposed()
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
    }
}
