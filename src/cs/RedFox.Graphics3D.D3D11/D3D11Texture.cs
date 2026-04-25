using RedFox.Graphics2D;
using RedFox.Graphics3D.Rendering.Backend;
using Silk.NET.Core.Native;
using Silk.NET.Direct3D11;
using System;

namespace RedFox.Graphics3D.D3D11;

/// <summary>
/// Represents a Direct3D 11 GPU texture resource.
/// </summary>
public sealed unsafe class D3D11Texture : IGpuTexture
{
    private ComPtr<ID3D11Texture2D> _texture;

    internal D3D11Texture(ComPtr<ID3D11Texture2D> texture, int width, int height, ImageFormat format, TextureUsage usage)
    {
        _texture = texture;
        Width = width;
        Height = height;
        Format = format;
        Usage = usage;
    }

    internal ID3D11Texture2D* Handle => _texture.Handle;

    internal int Width { get; }

    internal int Height { get; }

    internal ImageFormat Format { get; }

    internal TextureUsage Usage { get; }

    /// <inheritdoc/>
    public bool IsDisposed { get; private set; }

    /// <inheritdoc/>
    public void Dispose()
    {
        if (IsDisposed)
        {
            return;
        }

        _texture.Dispose();
        IsDisposed = true;
        GC.SuppressFinalize(this);
    }
}