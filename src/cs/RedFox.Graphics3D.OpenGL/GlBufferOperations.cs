using System.Numerics;
using Silk.NET.OpenGL;

namespace RedFox.Graphics3D.OpenGL;

/// <summary>
/// Provides low-level OpenGL buffer and texture allocation/upload operations
/// shared across the renderer, mesh uploader, and render passes.
/// </summary>
internal static class GlBufferOperations
{
    /// <summary>
    /// Uploads a float array to a vertex attribute buffer and returns the generated buffer ID.
    /// </summary>
    public static unsafe uint UploadFloatAttributeBuffer(GL gl, float[] data, BufferUsageARB usage)
    {
        uint buffer = gl.GenBuffer();
        gl.BindBuffer(BufferTargetARB.ArrayBuffer, buffer);
        fixed (float* ptr = data)
        {
            gl.BufferData(BufferTargetARB.ArrayBuffer, (nuint)(data.Length * sizeof(float)), ptr, usage);
        }
        return buffer;
    }

    /// <summary>
    /// Replaces the contents of an existing float buffer with new data using <see cref="BufferUsageARB.DynamicDraw"/>.
    /// </summary>
    public static unsafe void UploadFloatBuffer(GL gl, uint bufferId, float[] data)
    {
        if (bufferId == 0)
            return;

        gl.BindBuffer(BufferTargetARB.ArrayBuffer, bufferId);
        fixed (float* ptr = data)
        {
            gl.BufferData(BufferTargetARB.ArrayBuffer, (nuint)(data.Length * sizeof(float)), ptr, BufferUsageARB.DynamicDraw);
        }
    }

    /// <summary>
    /// Creates a 2D floating-point RGBA32f texture with nearest filtering and clamp-to-edge wrapping.
    /// </summary>
    public static unsafe uint CreateFloatTexture(GL gl, int width, int height, float[] data)
    {
        uint texture = gl.GenTexture();
        gl.BindTexture(TextureTarget.Texture2D, texture);
        gl.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureMinFilter, (int)GLEnum.Nearest);
        gl.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureMagFilter, (int)GLEnum.Nearest);
        gl.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureWrapS, (int)GLEnum.ClampToEdge);
        gl.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureWrapT, (int)GLEnum.ClampToEdge);

        fixed (float* ptr = data)
        {
            gl.TexImage2D(TextureTarget.Texture2D, 0, InternalFormat.Rgba32f,
                (uint)width, (uint)height, 0, PixelFormat.Rgba, PixelType.Float, ptr);
        }

        gl.BindTexture(TextureTarget.Texture2D, 0);
        return texture;
    }

    /// <summary>
    /// Updates a sub-region of an existing floating-point texture with new RGBA data.
    /// </summary>
    public static unsafe void UploadFloatTexture(GL gl, uint textureId, int width, int height, float[] data)
    {
        if (textureId == 0)
            return;

        gl.BindTexture(TextureTarget.Texture2D, textureId);
        fixed (float* ptr = data)
        {
            gl.TexSubImage2D(TextureTarget.Texture2D, 0, 0, 0,
                (uint)width, (uint)height, PixelFormat.Rgba, PixelType.Float, ptr);
        }
        gl.BindTexture(TextureTarget.Texture2D, 0);
    }

    /// <summary>
    /// Computes 2D texture dimensions that can hold <paramref name="texelCount"/> RGBA texels,
    /// clamped to the GPU's maximum texture size.
    /// </summary>
    public static (int Width, int Height) ComputeTextureDimensions(int maxTextureSize, int texelCount)
    {
        int safeTexelCount = Math.Max(texelCount, 1);
        int width = Math.Min(maxTextureSize, Math.Max(1, (int)MathF.Ceiling(MathF.Sqrt(safeTexelCount))));
        int height = (safeTexelCount + width - 1) / width;

        if (height > maxTextureSize)
            throw new InvalidOperationException(
                $"Texture data requires {height} rows, exceeding the maximum texture size {maxTextureSize}.");

        return (width, height);
    }

    /// <summary>
    /// Pads a float array to the requested length by appending zeroes if necessary.
    /// </summary>
    public static float[] PadTextureData(float[] source, int targetLength)
    {
        if (source.Length == targetLength)
            return source;

        float[] result = new float[targetLength];
        Array.Copy(source, result, source.Length);
        return result;
    }

    /// <summary>
    /// Writes the 16 floats of a <see cref="Matrix4x4"/> into a destination buffer as four consecutive RGBA texels.
    /// </summary>
    public static void WriteMatrixTexels(Matrix4x4 matrix, float[] destination, int texelBaseIndex)
    {
        WriteTexel(destination, texelBaseIndex + 0, matrix.M11, matrix.M12, matrix.M13, matrix.M14);
        WriteTexel(destination, texelBaseIndex + 1, matrix.M21, matrix.M22, matrix.M23, matrix.M24);
        WriteTexel(destination, texelBaseIndex + 2, matrix.M31, matrix.M32, matrix.M33, matrix.M34);
        WriteTexel(destination, texelBaseIndex + 3, matrix.M41, matrix.M42, matrix.M43, matrix.M44);
    }

    /// <summary>
    /// Writes a single RGBA texel (4 floats) into a destination buffer.
    /// </summary>
    public static void WriteTexel(float[] destination, int texelIndex, float x, float y, float z, float w)
    {
        int baseIndex = texelIndex * 4;
        destination[baseIndex] = x;
        destination[baseIndex + 1] = y;
        destination[baseIndex + 2] = z;
        destination[baseIndex + 3] = w;
    }

    /// <summary>
    /// Creates a 1x1 RGBA8 texture filled with the specified color.
    /// </summary>
    public static unsafe uint CreateDefault1x1Texture(GL gl, byte r, byte g, byte b, byte a)
    {
        uint tex = gl.GenTexture();
        gl.BindTexture(TextureTarget.Texture2D, tex);
        gl.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureMinFilter, (int)GLEnum.Linear);
        gl.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureMagFilter, (int)GLEnum.Linear);
        gl.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureWrapS, (int)GLEnum.Repeat);
        gl.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureWrapT, (int)GLEnum.Repeat);
        byte* pixel = stackalloc byte[4] { r, g, b, a };
        gl.TexImage2D(TextureTarget.Texture2D, 0, InternalFormat.Rgba8, 1, 1, 0,
            PixelFormat.Rgba, PixelType.UnsignedByte, pixel);
        gl.BindTexture(TextureTarget.Texture2D, 0);
        return tex;
    }
}
