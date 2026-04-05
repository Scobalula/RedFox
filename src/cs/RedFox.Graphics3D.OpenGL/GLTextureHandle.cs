using RedFox.Graphics2D;
using Silk.NET.OpenGL;

namespace RedFox.Graphics3D.OpenGL;

/// <summary>
/// Represents an OpenGL 2D texture uploaded from image data.
/// Supports both uncompressed RGBA and block-compressed (BCn) formats.
/// </summary>
public sealed class GLTextureHandle : IDisposable
{
    private readonly GL _gl;

    /// <summary>The OpenGL texture ID.</summary>
    public uint TextureId { get; private set; }

    /// <summary>The texture width in pixels.</summary>
    public int Width { get; }

    /// <summary>The texture height in pixels.</summary>
    public int Height { get; }

    /// <summary>
    /// Creates an RGBA8 texture from raw byte pixel data with mipmapped filtering.
    /// </summary>
    public GLTextureHandle(GL gl, byte[] rgbaData, int width, int height)
    {
        _gl = gl;
        Width = width;
        Height = height;

        TextureId = gl.GenTexture();
        gl.BindTexture(TextureTarget.Texture2D, TextureId);

        gl.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureMinFilter, (int)GLEnum.LinearMipmapLinear);
        gl.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureMagFilter, (int)GLEnum.Linear);
        gl.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureWrapS, (int)GLEnum.Repeat);
        gl.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureWrapT, (int)GLEnum.Repeat);

        gl.TexImage2D(TextureTarget.Texture2D, 0, InternalFormat.Rgba8, (uint)width, (uint)height, 0, PixelFormat.Rgba, PixelType.UnsignedByte, rgbaData);
        gl.GenerateMipmap(TextureTarget.Texture2D);
        gl.BindTexture(TextureTarget.Texture2D, 0);
    }

    /// <summary>
    /// Creates a block-compressed texture from an <see cref="Image"/> with pre-existing mip levels.
    /// </summary>
    public GLTextureHandle(GL gl, Image image)
    {
        _gl = gl;
        Width = image.Width;
        Height = image.Height;

        TextureId = gl.GenTexture();
        gl.BindTexture(TextureTarget.Texture2D, TextureId);

        gl.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureMinFilter, (int)GLEnum.LinearMipmapLinear);
        gl.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureMagFilter, (int)GLEnum.Linear);
        gl.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureWrapS, (int)GLEnum.Repeat);
        gl.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureWrapT, (int)GLEnum.Repeat);

        InternalFormat glFormat = MapCompressedFormat(image.Format);

        for (int mip = 0; mip < image.MipLevels; mip++)
        {
            ref readonly ImageSlice slice = ref image.GetSlice(mip, 0, 0);
            ReadOnlySpan<byte> data = slice.PixelSpan;
            int mipWidth = Math.Max(image.Width >> mip, 1);
            int mipHeight = Math.Max(image.Height >> mip, 1);

            gl.CompressedTexImage2D(TextureTarget.Texture2D, mip, glFormat,
                (uint)mipWidth, (uint)mipHeight, 0, (uint)data.Length, data);
        }

        if (image.MipLevels == 1)
            gl.GenerateMipmap(TextureTarget.Texture2D);

        gl.BindTexture(TextureTarget.Texture2D, 0);
    }

    /// <summary>Binds this texture to the specified texture unit.</summary>
    /// <param name="unit">Zero-based texture unit index.</param>
    public void Bind(uint unit)
    {
        _gl.ActiveTexture((TextureUnit)((int)TextureUnit.Texture0 + (int)unit));
        _gl.BindTexture(TextureTarget.Texture2D, TextureId);
    }

    /// <summary>
    /// Checks whether the given image format is supported as a block-compressed GPU texture.
    /// </summary>
    public static bool IsCompressedFormatSupported(ImageFormat format) => MapCompressedFormatOrDefault(format) != 0;

    private static InternalFormat MapCompressedFormat(ImageFormat format) => format switch
    {
        ImageFormat.BC1Typeless or ImageFormat.BC1Unorm => InternalFormat.CompressedRgbaS3TCDxt1Ext,
        ImageFormat.BC1UnormSrgb => InternalFormat.CompressedSrgbAlphaS3TCDxt1Ext,
        ImageFormat.BC2Typeless or ImageFormat.BC2Unorm => InternalFormat.CompressedRgbaS3TCDxt3Ext,
        ImageFormat.BC2UnormSrgb => InternalFormat.CompressedSrgbAlphaS3TCDxt3Ext,
        ImageFormat.BC3Typeless or ImageFormat.BC3Unorm => InternalFormat.CompressedRgbaS3TCDxt5Ext,
        ImageFormat.BC3UnormSrgb => InternalFormat.CompressedSrgbAlphaS3TCDxt5Ext,
        ImageFormat.BC4Typeless or ImageFormat.BC4Unorm => InternalFormat.CompressedRedRgtc1,
        ImageFormat.BC4Snorm => InternalFormat.CompressedSignedRedRgtc1,
        ImageFormat.BC5Typeless or ImageFormat.BC5Unorm => InternalFormat.CompressedRGRgtc2,
        ImageFormat.BC5Snorm => InternalFormat.CompressedSignedRGRgtc2,
        ImageFormat.BC6HTypeless or ImageFormat.BC6HUF16 => InternalFormat.CompressedRgbBptcUnsignedFloat,
        ImageFormat.BC6HSF16 => InternalFormat.CompressedRgbBptcSignedFloat,
        ImageFormat.BC7Typeless or ImageFormat.BC7Unorm => InternalFormat.CompressedRgbaBptcUnorm,
        ImageFormat.BC7UnormSrgb => InternalFormat.CompressedSrgbAlphaBptcUnorm,
        _ => throw new NotSupportedException($"Unsupported compressed format: {format}")
    };

    private static InternalFormat MapCompressedFormatOrDefault(ImageFormat format) => format switch
    {
        ImageFormat.BC1Typeless or ImageFormat.BC1Unorm => InternalFormat.CompressedRgbaS3TCDxt1Ext,
        ImageFormat.BC1UnormSrgb => InternalFormat.CompressedSrgbAlphaS3TCDxt1Ext,
        ImageFormat.BC2Typeless or ImageFormat.BC2Unorm => InternalFormat.CompressedRgbaS3TCDxt3Ext,
        ImageFormat.BC2UnormSrgb => InternalFormat.CompressedSrgbAlphaS3TCDxt3Ext,
        ImageFormat.BC3Typeless or ImageFormat.BC3Unorm => InternalFormat.CompressedRgbaS3TCDxt5Ext,
        ImageFormat.BC3UnormSrgb => InternalFormat.CompressedSrgbAlphaS3TCDxt5Ext,
        ImageFormat.BC4Typeless or ImageFormat.BC4Unorm => InternalFormat.CompressedRedRgtc1,
        ImageFormat.BC4Snorm => InternalFormat.CompressedSignedRedRgtc1,
        ImageFormat.BC5Typeless or ImageFormat.BC5Unorm => InternalFormat.CompressedRGRgtc2,
        ImageFormat.BC5Snorm => InternalFormat.CompressedSignedRGRgtc2,
        ImageFormat.BC6HTypeless or ImageFormat.BC6HUF16 => InternalFormat.CompressedRgbBptcUnsignedFloat,
        ImageFormat.BC6HSF16 => InternalFormat.CompressedRgbBptcSignedFloat,
        ImageFormat.BC7Typeless or ImageFormat.BC7Unorm => InternalFormat.CompressedRgbaBptcUnorm,
        ImageFormat.BC7UnormSrgb => InternalFormat.CompressedSrgbAlphaBptcUnorm,
        _ => 0
    };

    /// <summary>Deletes the OpenGL texture resource.</summary>
    public void Dispose()
    {
        if (TextureId != 0)
        {
            try { _gl.DeleteTexture(TextureId); } catch { }
            TextureId = 0;
        }
    }
}
