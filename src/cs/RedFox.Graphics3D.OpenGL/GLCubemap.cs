using Silk.NET.OpenGL;
using System;
using System.IO;
using System.Text;

namespace RedFox.Graphics3D.OpenGL;

/// <summary>
/// Represents an OpenGL cubemap texture.
/// </summary>
public sealed class GLCubemap : IDisposable
{
    private readonly GL _gl;

    /// <summary>OpenGL texture ID.</summary>
    public uint TextureId { get; private set; }

    /// <summary>Face resolution (each face is size x size).</summary>
    public int Size { get; private set; }

    /// <summary>Number of mipmap levels (1 if not mipmapped).</summary>
    public int MipLevels { get; private set; }

    public GLCubemap(GL gl)
    {
        _gl = gl ?? throw new ArgumentNullException(nameof(gl));
    }

    /// <summary>
    /// Creates an empty cubemap with the specified parameters.
    /// </summary>
    public void Create(int size, int mipLevels = 1, bool useMipmaps = false)
    {
        Dispose();
        Size = size;
        MipLevels = mipLevels;
        TextureId = _gl.GenTexture();
        _gl.BindTexture(TextureTarget.TextureCubeMap, TextureId);

        for (int level = 0; level < mipLevels; level++)
        {
            int w = Math.Max(size >> level, 1);
            int h = Math.Max(size >> level, 1);
            float[] emptyData = new float[w * h * 4];

            for (int face = 0; face < 6; face++)
            {
                var target = (TextureTarget)((int)TextureTarget.TextureCubeMapPositiveX + face);
                unsafe
                {
                    fixed (float* ptr = emptyData)
                    {
                        _gl.TexImage2D(target, level, InternalFormat.Rgba32f, (uint)w, (uint)h, 0, PixelFormat.Rgba, PixelType.Float, ptr);
                    }
                }
            }
        }

        if (useMipmaps && mipLevels > 1)
        {
            _gl.TexParameter(TextureTarget.TextureCubeMap, TextureParameterName.TextureMinFilter, (int)GLEnum.LinearMipmapLinear);
        }
        else
        {
            _gl.TexParameter(TextureTarget.TextureCubeMap, TextureParameterName.TextureMinFilter, (int)GLEnum.Linear);
        }
        _gl.TexParameter(TextureTarget.TextureCubeMap, TextureParameterName.TextureMagFilter, (int)GLEnum.Linear);

        // Clamp to edge on cubemaps prevents seams
        _gl.TexParameter(TextureTarget.TextureCubeMap, TextureParameterName.TextureWrapS, (int)GLEnum.ClampToEdge);
        _gl.TexParameter(TextureTarget.TextureCubeMap, TextureParameterName.TextureWrapT, (int)GLEnum.ClampToEdge);
        _gl.TexParameter(TextureTarget.TextureCubeMap, TextureParameterName.TextureWrapR, (int)GLEnum.ClampToEdge);

        _gl.BindTexture(TextureTarget.TextureCubeMap, 0);
    }

    /// <summary>
    /// Binds this cubemap to the specified texture unit.
    /// </summary>
    public void Bind(uint unit)
    {
        _gl.ActiveTexture((TextureUnit)((int)TextureUnit.Texture0 + (int)unit));
        _gl.BindTexture(TextureTarget.TextureCubeMap, TextureId);
    }

    /// <summary>
    /// Saves this cubemap to a binary file for caching.
    /// Format: "FOXCMAP " (8 bytes) + size (int) + mipLevels (int) + float data for each face per mip level.
    /// </summary>
    public unsafe void SaveToFile(string path)
    {
        using var fs = new FileStream(path, FileMode.Create, FileAccess.Write, FileShare.None);
        using var bw = new BinaryWriter(fs, Encoding.UTF8);

        // Magic header
        bw.Write(Encoding.ASCII.GetBytes("FOXCMAP "));
        bw.Write(Size);
        bw.Write(MipLevels);

        // Read back each mip level and face from GPU
        byte[] faceBytes;

        for (int level = 0; level < MipLevels; level++)
        {
            int w = Math.Max(Size >> level, 1);
            int h = Math.Max(Size >> level, 1);
            int faceFloatCount = w * h * 4;
            faceBytes = new byte[faceFloatCount * sizeof(float)];

            for (int face = 0; face < 6; face++)
            {
                var target = (TextureTarget)((int)TextureTarget.TextureCubeMapPositiveX + face);
                fixed (byte* ptr = faceBytes)
                {
                    _gl.BindTexture(TextureTarget.TextureCubeMap, TextureId);
                    _gl.GetTexImage(target, level, PixelFormat.Rgba, PixelType.Float, ptr);
                }
                bw.Write(faceBytes);
            }
        }

        _gl.BindTexture(TextureTarget.TextureCubeMap, 0);
    }

    /// <summary>
    /// Loads a cubemap from a previously cached binary file.
    /// Returns true if the file was loaded successfully.
    /// </summary>
    public bool LoadFromFile(string path)
    {
        if (!File.Exists(path))
            return false;

        using var fs = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read);
        using var br = new BinaryReader(fs, Encoding.UTF8);

        var magic = br.ReadBytes(8);
        if (magic.Length != 8 || Encoding.ASCII.GetString(magic) != "FOXCMAP ")
            return false;

        int size = br.ReadInt32();
        int mipLevels = br.ReadInt32();

        Create(size, mipLevels, mipLevels > 1);

        byte[] faceBytes = new byte[size * size * 4 * sizeof(float)];

        for (int level = 0; level < mipLevels; level++)
        {
            int w = Math.Max(size >> level, 1);
            int h = Math.Max(size >> level, 1);
            int faceByteSize = w * h * 4 * sizeof(float);

            if (faceBytes.Length < faceByteSize)
                faceBytes = new byte[faceByteSize];

            for (int face = 0; face < 6; face++)
            {
                int bytesRead = br.Read(faceBytes, 0, faceByteSize);
                if (bytesRead != faceByteSize)
                    return false;

                var target = (TextureTarget)((int)TextureTarget.TextureCubeMapPositiveX + face);
                unsafe
                {
                    fixed (byte* ptr = faceBytes)
                    {
                        _gl.BindTexture(TextureTarget.TextureCubeMap, TextureId);
                        _gl.TexSubImage2D(target, level, 0, 0, (uint)w, (uint)h, PixelFormat.Rgba, PixelType.Float, ptr);
                    }
                }
            }
        }

        _gl.BindTexture(TextureTarget.TextureCubeMap, 0);
        return true;
    }

    /// <summary>
    /// Checks if a cached cubemap file exists and matches the expected parameters.
    /// </summary>
    public static bool CacheExists(string path, int size, int mipLevels)
    {
        if (!File.Exists(path))
            return false;

        using var fs = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read);
        if (fs.Length < 16)
            return false;

        using var br = new BinaryReader(fs, Encoding.UTF8);
        var magic = br.ReadBytes(8);
        if (magic.Length != 8 || Encoding.ASCII.GetString(magic) != "FOXCMAP ")
            return false;

        return br.ReadInt32() == size && br.ReadInt32() == mipLevels;
    }

    public void Dispose()
    {
        if (TextureId != 0)
        {
            try { _gl.DeleteTexture(TextureId); } catch { }
            TextureId = 0;
        }
        Size = 0;
        MipLevels = 0;
    }
}
