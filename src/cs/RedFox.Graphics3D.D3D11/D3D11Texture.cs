using RedFox.Graphics2D;
using RedFox.Graphics3D.Rendering;
using Silk.NET.Core.Native;
using Silk.NET.Direct3D11;
using System;

namespace RedFox.Graphics3D.D3D11;

/// <summary>
/// Represents a Direct3D 11 GPU texture resource.
/// </summary>
public sealed unsafe class D3D11Texture : IGpuTexture
{
    private ComPtr<ID3D11SamplerState> _samplerState;
    private ComPtr<ID3D11ShaderResourceView> _shaderResourceView;
    private ComPtr<ID3D11Texture2D> _texture;

    internal D3D11Texture(
        ComPtr<ID3D11Texture2D> texture,
        ComPtr<ID3D11ShaderResourceView> shaderResourceView,
        ComPtr<ID3D11SamplerState> samplerState,
        int width,
        int height,
        int arraySize,
        int mipLevels,
        ImageFormat format,
        TextureUsage usage,
        int sampleCount,
        bool isCubemap)
    {
        _texture = texture;
        _shaderResourceView = shaderResourceView;
        _samplerState = samplerState;
        Width = width;
        Height = height;
        ArraySize = arraySize;
        MipLevels = mipLevels;
        Format = format;
        Usage = usage;
        SampleCount = Math.Max(1, sampleCount);
        IsCubemap = isCubemap;
    }

    internal ID3D11Texture2D* Handle => _texture.Handle;

    internal ID3D11ShaderResourceView* ShaderResourceView => _shaderResourceView.Handle;

    internal ID3D11SamplerState* SamplerState => _samplerState.Handle;

    internal int Width { get; }

    internal int Height { get; }

    internal int ArraySize { get; }

    internal int MipLevels { get; }

    internal ImageFormat Format { get; }

    internal TextureUsage Usage { get; }

    internal int SampleCount { get; }

    internal bool IsCubemap { get; }

    /// <inheritdoc/>
    public bool IsDisposed { get; private set; }

    /// <inheritdoc/>
    public void Dispose()
    {
        if (IsDisposed)
        {
            return;
        }

        _samplerState.Dispose();
        _shaderResourceView.Dispose();
        _texture.Dispose();
        IsDisposed = true;
        GC.SuppressFinalize(this);
    }
}