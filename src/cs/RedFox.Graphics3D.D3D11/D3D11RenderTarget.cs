using RedFox.Graphics3D.Rendering;
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

    internal D3D11RenderTarget(
        ComPtr<ID3D11RenderTargetView> renderTargetView,
        ComPtr<ID3D11DepthStencilView> depthStencilView,
        D3D11Texture colorTexture,
        D3D11Texture? depthTexture)
    {
        _renderTargetView = renderTargetView;
        _depthStencilView = depthStencilView;
        ColorTexture = colorTexture ?? throw new ArgumentNullException(nameof(colorTexture));
        DepthTexture = depthTexture;
        if (DepthTexture is not null && DepthTexture.SampleCount != ColorTexture.SampleCount)
        {
            throw new ArgumentException("Depth attachment sample count must match color attachment sample count.", nameof(depthTexture));
        }
    }

    internal ID3D11RenderTargetView* RenderTargetView => _renderTargetView.Handle;

    internal ID3D11DepthStencilView* DepthStencilView => _depthStencilView.Handle;

    internal D3D11Texture ColorTexture { get; }

    internal D3D11Texture? DepthTexture { get; }

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