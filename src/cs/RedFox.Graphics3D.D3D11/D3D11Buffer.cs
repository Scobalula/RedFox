using System;
using RedFox.Graphics3D.Rendering.Backend;
using Silk.NET.Core.Native;
using Silk.NET.Direct3D11;

namespace RedFox.Graphics3D.D3D11;

/// <summary>
/// Represents a Direct3D 11 GPU buffer resource.
/// </summary>
public sealed unsafe class D3D11Buffer : IGpuBuffer
{
    private ComPtr<ID3D11Buffer> _buffer;
    private ComPtr<ID3D11ShaderResourceView> _shaderResourceView;
    private ComPtr<ID3D11UnorderedAccessView> _unorderedAccessView;

    internal D3D11Buffer(
        ComPtr<ID3D11Buffer> buffer,
        ComPtr<ID3D11ShaderResourceView> shaderResourceView,
        ComPtr<ID3D11UnorderedAccessView> unorderedAccessView,
        int sizeBytes,
        int strideBytes,
        BufferUsage usage)
    {
        _buffer = buffer;
        _shaderResourceView = shaderResourceView;
        _unorderedAccessView = unorderedAccessView;
        SizeBytes = sizeBytes;
        StrideBytes = strideBytes;
        Usage = usage;
    }

    internal ID3D11Buffer* Handle => _buffer.Handle;

    internal ID3D11ShaderResourceView* ShaderResourceView => _shaderResourceView.Handle;

    internal ID3D11UnorderedAccessView* UnorderedAccessView => _unorderedAccessView.Handle;

    internal int SizeBytes { get; }

    internal int StrideBytes { get; }

    internal BufferUsage Usage { get; }

    /// <inheritdoc/>
    public bool IsDisposed { get; private set; }

    /// <inheritdoc/>
    public void Dispose()
    {
        if (IsDisposed)
        {
            return;
        }

        _unorderedAccessView.Dispose();
        _shaderResourceView.Dispose();
        _buffer.Dispose();
        IsDisposed = true;
        GC.SuppressFinalize(this);
    }
}