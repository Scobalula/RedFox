using RedFox.Graphics3D.Rendering.Backend;
using Silk.NET.Core.Native;
using Silk.NET.Direct3D11;
using System;

namespace RedFox.Graphics3D.D3D11;

/// <summary>
/// Represents a Direct3D 11 render-target resource.
/// </summary>
public sealed unsafe class D3D11RenderTarget : IGpuRenderTarget
{
    private ComPtr<ID3D11DepthStencilView> _depthStencilView;
    private ComPtr<ID3D11RenderTargetView> _renderTargetView;

    internal D3D11RenderTarget(ComPtr<ID3D11RenderTargetView> renderTargetView, ComPtr<ID3D11DepthStencilView> depthStencilView)
    {
        _renderTargetView = renderTargetView;
        _depthStencilView = depthStencilView;
    }

    internal ID3D11RenderTargetView* RenderTargetView => _renderTargetView.Handle;

    internal ID3D11DepthStencilView* DepthStencilView => _depthStencilView.Handle;

    /// <inheritdoc/>
    public bool IsDisposed { get; private set; }

    /// <inheritdoc/>
    public void Dispose()
    {
        if (IsDisposed)
        {
            return;
        }

        _depthStencilView.Dispose();
        _renderTargetView.Dispose();
        IsDisposed = true;
        GC.SuppressFinalize(this);
    }
}