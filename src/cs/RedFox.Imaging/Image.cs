using System;
using System.Numerics;
using System.Runtime.InteropServices;
using RedFox.Imaging.BlockCompression;
using RedFox.Imaging.Codecs;
using RedFox.Imaging.Primitives;
using RedFox.Imaging.Processing;

namespace RedFox.Imaging;

/// <summary>
/// Represents texture or image data and all of its subresources in a single contiguous managed buffer.
/// </summary>
/// <remarks>
/// <para>The image owns its pixel buffer. When a buffer is supplied to a constructor it is adopted without copying, so the caller must not keep using that array independently.</para>
/// <para>Subresources are laid out array element first, then mip level, then depth slice. <see cref="ImageSlice"/> values are non-owning views of that buffer.</para>
/// <para>The image holds no unmanaged resources and does not need to be disposed.</para>
/// </remarks>
public sealed class Image
{
    private Memory<byte> _pixels;
    private ImageSlice[] _slices;

    /// <summary>
    /// Gets the width of the top-level image in pixels.
    /// </summary>
    public int Width { get; }

    /// <summary>
    /// Gets the height of the top-level image in pixels.
    /// </summary>
    public int Height { get; }

    /// <summary>
    /// Gets the image depth in pixels.
    /// </summary>
    public int Depth { get; }

    /// <summary>
    /// Gets the number of texture-array elements, including every cube face.
    /// </summary>
    public int ArraySize { get; }

    /// <summary>
    /// Gets the number of mip map levels.
    /// </summary>
    public int MipLevels { get; }

    /// <summary>
    /// Gets the pixel format of the image. Use <see cref="Convert(ImageFormat)"/> to change it.
    /// </summary>
    public ImageFormat Format { get; private set; }

    /// <summary>
    /// Gets whether this image represents a cube map texture.
    /// </summary>
    public bool IsCubemap { get; }

    /// <summary>
    /// Gets the layout description of this image.
    /// </summary>
    public ImageInfo Info => new(Width, Height, Depth, ArraySize, MipLevels, Format, IsCubemap);

    /// <summary>
    /// Gets the total number of sub-image slices.
    /// </summary>
    public int SliceCount => _slices.Length;

    /// <summary>
    /// Gets a read-only span over all sub-image slices.
    /// </summary>
    public ReadOnlySpan<ImageSlice> Slices => _slices;

    /// <summary>
    /// Gets the entire pixel buffer as a span. The span covers exactly the bytes described by the layout.
    /// </summary>
    public Span<byte> PixelData => _pixels.Span;

    /// <summary>
    /// Gets the entire pixel buffer as a <see cref="Memory{T}"/>. The memory covers exactly the bytes described by the layout.
    /// </summary>
    public Memory<byte> PixelMemory => _pixels;

    /// <summary>
    /// Initializes a new <see cref="Image"/> with the given dimensions, format, and optional data.
    /// </summary>
    /// <param name="width">Width of the top-level image in pixels.</param>
    /// <param name="height">Height of the top-level image in pixels.</param>
    /// <param name="depth">Depth of the image (1 for 2D textures).</param>
    /// <param name="arraySize">Number of array elements (1 for non-array textures, a multiple of 6 for cube maps).</param>
    /// <param name="mipLevels">Number of mip map levels (1 for no mipmaps).</param>
    /// <param name="format">The pixel format.</param>
    /// <param name="isCubemap">Whether this image is a cube map texture.</param>
    /// <param name="data">Pixel data to adopt without copying, or <see langword="null"/> to allocate zeroed storage. It may be longer than required; trailing bytes are ignored.</param>
    /// <exception cref="ArgumentException">Thrown when the layout is invalid, too large, or <paramref name="data"/> is too small.</exception>
    public Image(int width, int height, int depth, int arraySize, int mipLevels, ImageFormat format, bool isCubemap, byte[]? data)
    {
        ImageInfo info = new(width, height, depth, arraySize, mipLevels, format, isCubemap);
        string? error = info.GetValidationError();

        if (error is not null)
            throw new ArgumentException(error);

        int totalSize = (int)info.CalculateTotalByteCount();

        if (data is not null && data.Length < totalSize)
            throw new ArgumentException($"Provided data ({data.Length} bytes) is smaller than the required size ({totalSize} bytes).", nameof(data));

        Width = width;
        Height = height;
        Depth = depth;
        ArraySize = arraySize;
        MipLevels = mipLevels;
        Format = format;
        IsCubemap = isCubemap;

        _pixels = (data ?? new byte[totalSize]).AsMemory(0, totalSize);
        _slices = BuildSlices(info, _pixels);
    }

    /// <summary>
    /// Initializes a new <see cref="Image"/> with the given dimensions and format, without initial data.
    /// </summary>
    /// <param name="width">Width of the top-level image in pixels.</param>
    /// <param name="height">Height of the top-level image in pixels.</param>
    /// <param name="depth">Depth of the image (1 for 2D textures).</param>
    /// <param name="arraySize">Number of array elements (1 for non-array textures, a multiple of 6 for cube maps).</param>
    /// <param name="mipLevels">Number of mip map levels (1 for no mipmaps).</param>
    /// <param name="format">The pixel format.</param>
    /// <param name="isCubemap">Whether this image is a cube map texture.</param>
    public Image(int width, int height, int depth, int arraySize, int mipLevels, ImageFormat format, bool isCubemap) : this(width, height, depth, arraySize, mipLevels, format, isCubemap, null)
    {
    }

    /// <summary>
    /// Initializes a new <see cref="Image"/> with the given dimensions and format.
    /// </summary>
    /// <param name="width">Width of the top-level image in pixels.</param>
    /// <param name="height">Height of the top-level image in pixels.</param>
    /// <param name="depth">Depth of the image (1 for 2D textures).</param>
    /// <param name="arraySize">Number of array elements (1 for non-array textures).</param>
    /// <param name="mipLevels">Number of mip map levels (1 for no mipmaps).</param>
    /// <param name="format">The pixel format.</param>
    public Image(int width, int height, int depth, int arraySize, int mipLevels, ImageFormat format) : this(width, height, depth, arraySize, mipLevels, format, false, null)
    {
    }

    /// <summary>
    /// Initializes a new <see cref="Image"/> with the given dimensions and initial data.
    /// </summary>
    /// <param name="width">Width of the top-level image in pixels.</param>
    /// <param name="height">Height of the top-level image in pixels.</param>
    /// <param name="depth">Depth of the image (1 for 2D textures).</param>
    /// <param name="arraySize">Number of array elements (1 for non-array textures).</param>
    /// <param name="mipLevels">Number of mip map levels (1 for no mipmaps).</param>
    /// <param name="format">The pixel format.</param>
    /// <param name="data">Pixel data to adopt without copying.</param>
    public Image(int width, int height, int depth, int arraySize, int mipLevels, ImageFormat format, byte[] data) : this(width, height, depth, arraySize, mipLevels, format, false, data)
    {
    }

    /// <summary>
    /// Creates a simple 2D image with no mip maps and array size 1.
    /// </summary>
    /// <param name="width">Width of the image in pixels.</param>
    /// <param name="height">Height of the image in pixels.</param>
    /// <param name="format">The pixel format.</param>
    /// <param name="data">Pixel data to adopt without copying.</param>
    public Image(int width, int height, ImageFormat format, byte[] data) : this(width, height, 1, 1, 1, format, false, data)
    {
    }

    /// <summary>
    /// Creates a simple 2D image with no mip maps and array size 1.
    /// </summary>
    /// <param name="width">Width of the image in pixels.</param>
    /// <param name="height">Height of the image in pixels.</param>
    /// <param name="format">The pixel format.</param>
    public Image(int width, int height, ImageFormat format) : this(width, height, 1, 1, 1, format, false, null)
    {
    }

    /// <summary>
    /// Creates an image with zeroed storage for the given layout.
    /// </summary>
    /// <param name="info">The image layout.</param>
    public Image(ImageInfo info) : this(info.Width, info.Height, info.Depth, info.ArraySize, info.MipLevels, info.Format, info.IsCubemap, null)
    {
    }

    /// <summary>
    /// Creates an image for the given layout that adopts the supplied data without copying.
    /// </summary>
    /// <param name="info">The image layout.</param>
    /// <param name="data">Pixel data to adopt without copying.</param>
    public Image(ImageInfo info, byte[] data) : this(info.Width, info.Height, info.Depth, info.ArraySize, info.MipLevels, info.Format, info.IsCubemap, data)
    {
    }

    /// <summary>
    /// Gets the <see cref="ImageSlice"/> for the specified mip level, array element, and depth slice.
    /// </summary>
    /// <param name="mipLevel">The mip level (0 is the largest).</param>
    /// <param name="arrayIndex">The array element index.</param>
    /// <param name="depthSlice">The depth slice index (for 3D textures).</param>
    /// <returns>The corresponding <see cref="ImageSlice"/>.</returns>
    public ref readonly ImageSlice GetSlice(int mipLevel, int arrayIndex, int depthSlice)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(mipLevel);
        ArgumentOutOfRangeException.ThrowIfNegative(arrayIndex);
        ArgumentOutOfRangeException.ThrowIfNegative(depthSlice);
        ArgumentOutOfRangeException.ThrowIfGreaterThanOrEqual(mipLevel, MipLevels);
        ArgumentOutOfRangeException.ThrowIfGreaterThanOrEqual(arrayIndex, ArraySize);
        ArgumentOutOfRangeException.ThrowIfGreaterThanOrEqual(depthSlice, Math.Max(1, Depth >> mipLevel));

        return ref _slices[GetSliceIndex(mipLevel, arrayIndex, depthSlice)];
    }

    /// <summary>
    /// Gets the first slice of the image.
    /// </summary>
    /// <returns>The first <see cref="ImageSlice"/> in the image.</returns>
    public ref readonly ImageSlice GetSlice()
    {
        return ref GetSlice(0, 0, 0);
    }

    /// <summary>
    /// Gets the first depth slice for the specified mip level and array element.
    /// </summary>
    /// <param name="mipLevel">The mip level (0 is the largest).</param>
    /// <param name="arrayIndex">The array element index.</param>
    /// <returns>The first depth slice for the requested mip and array entry.</returns>
    public ref readonly ImageSlice GetSlice(int mipLevel, int arrayIndex)
    {
        return ref GetSlice(mipLevel, arrayIndex, 0);
    }

    /// <summary>
    /// Gets the first array/depth slice for the specified mip level.
    /// </summary>
    /// <param name="mipLevel">The mip level (0 is the largest).</param>
    /// <returns>The first slice for the requested mip level.</returns>
    public ref readonly ImageSlice GetSlice(int mipLevel)
    {
        return ref GetSlice(mipLevel, 0, 0);
    }

    /// <summary>
    /// Converts this image in place to the specified format.
    /// </summary>
    /// <param name="targetFormat">The target format.</param>
    public void Convert(ImageFormat targetFormat)
    {
        Convert(targetFormat, ImageConvertFlags.None, ConverterEngine.None);
    }

    /// <summary>
    /// Converts this image in place to the specified format using a converter engine when possible.
    /// </summary>
    /// <param name="targetFormat">The target format.</param>
    /// <param name="converterEngine">Optional custom converter engine.</param>
    public void Convert(ImageFormat targetFormat, ConverterEngine? converterEngine)
    {
        Convert(targetFormat, ImageConvertFlags.None, converterEngine);
    }

    /// <summary>
    /// Converts this image in place to the specified format with conversion hints.
    /// </summary>
    /// <param name="targetFormat">The target format.</param>
    /// <param name="flags">Conversion hints.</param>
    public void Convert(ImageFormat targetFormat, ImageConvertFlags flags)
    {
        Convert(targetFormat, flags, ConverterEngine.None);
    }

    /// <summary>
    /// Converts this image in place to the specified format with an optional converter engine.
    /// Slices the engine declines are converted with the built-in CPU codecs.
    /// Previously obtained <see cref="ImageSlice"/> views keep referencing the old buffer.
    /// </summary>
    /// <param name="targetFormat">The target format.</param>
    /// <param name="flags">Conversion hints.</param>
    /// <param name="converterEngine">The converter engine to try first, or <see langword="null"/> to use the CPU codecs only.</param>
    public void Convert(ImageFormat targetFormat, ImageConvertFlags flags, ConverterEngine? converterEngine)
    {
        if (Format == targetFormat)
            return;

        ImageInfo targetInfo = Info with { Format = targetFormat };
        string? error = targetInfo.GetValidationError();

        if (error is not null)
            throw new NotSupportedException(error);

        IPixelCodec sourceCodec = PixelCodecs.GetCodec(Format);
        IPixelCodec targetCodec = PixelCodecs.GetCodec(targetFormat);

        Memory<byte> newPixels = new byte[targetInfo.CalculateTotalByteCount()];
        ImageSlice[] newSlices = BuildSlices(targetInfo, newPixels);

        for (int i = 0; i < _slices.Length; i++)
        {
            ref readonly ImageSlice source = ref _slices[i];
            ref readonly ImageSlice destination = ref newSlices[i];

            if (converterEngine is not null && converterEngine.TryConvert(source.PixelSpan, Format, destination.PixelSpan, targetFormat, source.Width, source.Height, flags))
                continue;

            ConvertSlice(source, sourceCodec, destination, targetCodec, flags);
        }

        _pixels = newPixels;
        _slices = newSlices;
        Format = targetFormat;
    }

    /// <summary>
    /// Reinterprets the entire raw pixel buffer as a span of <typeparamref name="T"/>.
    /// This is an explicit, unchecked reinterpretation of the stored bytes and performs no format conversion.
    /// </summary>
    /// <typeparam name="T">The unmanaged element type to reinterpret the pixel buffer as.</typeparam>
    /// <returns>A span over the underlying pixel buffer reinterpreted as <typeparamref name="T"/> values.</returns>
    public Span<T> GetPixelData<T>() where T : unmanaged
    {
        return MemoryMarshal.Cast<byte, T>(_pixels.Span);
    }

    /// <summary>
    /// Decodes all pixels of a specific slice to <see cref="Vector4"/> values using the built-in codec.
    /// </summary>
    /// <param name="mipLevel">The mip level (0 is the largest).</param>
    /// <param name="arrayIndex">The array element index.</param>
    /// <param name="depthSlice">The depth slice index (for 3D textures).</param>
    /// <returns>An array containing one decoded <see cref="Vector4"/> per pixel.</returns>
    public Vector4[] DecodeSlice(int mipLevel, int arrayIndex, int depthSlice)
    {
        ref readonly ImageSlice slice = ref GetSlice(mipLevel, arrayIndex, depthSlice);
        Vector4[] result = new Vector4[slice.Width * slice.Height];
        PixelCodecs.GetCodec(Format).Decode(slice.PixelSpan, result, slice.Width, slice.Height);
        return result;
    }

    /// <summary>
    /// Decodes all pixels of the first slice to <see cref="Vector4"/> values.
    /// </summary>
    /// <returns>An array containing one decoded <see cref="Vector4"/> per pixel.</returns>
    public Vector4[] DecodeSlice()
    {
        return DecodeSlice(0, 0, 0);
    }

    /// <summary>
    /// Decodes all pixels for the specified mip level and array index.
    /// </summary>
    /// <param name="mipLevel">The mip level (0 is the largest).</param>
    /// <param name="arrayIndex">The array element index.</param>
    /// <returns>An array containing one decoded <see cref="Vector4"/> per pixel.</returns>
    public Vector4[] DecodeSlice(int mipLevel, int arrayIndex)
    {
        return DecodeSlice(mipLevel, arrayIndex, 0);
    }

    /// <summary>
    /// Decodes all pixels for the specified mip level.
    /// </summary>
    /// <param name="mipLevel">The mip level (0 is the largest).</param>
    /// <returns>An array containing one decoded <see cref="Vector4"/> per pixel.</returns>
    public Vector4[] DecodeSlice(int mipLevel)
    {
        return DecodeSlice(mipLevel, 0, 0);
    }

    /// <summary>
    /// Decodes all pixels of a specific slice to values of type <typeparamref name="T"/> using the built-in codec.
    /// Each pixel produces 4 component values (RGBA).
    /// </summary>
    /// <typeparam name="T">The numeric component type to convert each RGBA channel into.</typeparam>
    /// <param name="mipLevel">The mip level (0 is the largest).</param>
    /// <param name="arrayIndex">The array element index.</param>
    /// <param name="depthSlice">The depth slice index (for 3D textures).</param>
    /// <returns>An array containing interleaved RGBA component values for every decoded pixel.</returns>
    public T[] DecodeSlice<T>(int mipLevel, int arrayIndex, int depthSlice) where T : INumber<T>
    {
        Vector4[] decoded = DecodeSlice(mipLevel, arrayIndex, depthSlice);
        T[] result = new T[decoded.Length * 4];

        for (int i = 0; i < decoded.Length; i++)
        {
            ref Vector4 pixel = ref decoded[i];
            int offset = i * 4;

            result[offset + 0] = T.CreateSaturating(pixel.X);
            result[offset + 1] = T.CreateSaturating(pixel.Y);
            result[offset + 2] = T.CreateSaturating(pixel.Z);
            result[offset + 3] = T.CreateSaturating(pixel.W);
        }

        return result;
    }

    /// <summary>
    /// Decodes all pixels of the first slice to values of type <typeparamref name="T"/>.
    /// </summary>
    /// <typeparam name="T">The numeric component type to convert each RGBA channel into.</typeparam>
    /// <returns>An array containing interleaved RGBA component values for every decoded pixel.</returns>
    public T[] DecodeSlice<T>() where T : INumber<T>
    {
        return DecodeSlice<T>(0, 0, 0);
    }

    /// <summary>
    /// Decodes all pixels for the specified mip level and array index to values of type <typeparamref name="T"/>.
    /// </summary>
    /// <typeparam name="T">The numeric component type to convert each RGBA channel into.</typeparam>
    /// <param name="mipLevel">The mip level (0 is the largest).</param>
    /// <param name="arrayIndex">The array element index.</param>
    /// <returns>An array containing interleaved RGBA component values for every decoded pixel.</returns>
    public T[] DecodeSlice<T>(int mipLevel, int arrayIndex) where T : INumber<T>
    {
        return DecodeSlice<T>(mipLevel, arrayIndex, 0);
    }

    /// <summary>
    /// Decodes all pixels for the specified mip level to values of type <typeparamref name="T"/>.
    /// </summary>
    /// <typeparam name="T">The numeric component type to convert each RGBA channel into.</typeparam>
    /// <param name="mipLevel">The mip level (0 is the largest).</param>
    /// <returns>An array containing interleaved RGBA component values for every decoded pixel.</returns>
    public T[] DecodeSlice<T>(int mipLevel) where T : INumber<T>
    {
        return DecodeSlice<T>(mipLevel, 0, 0);
    }

    /// <summary>
    /// Encodes <see cref="Vector4"/> pixels into a specific slice using the built-in codec for <see cref="Format"/>.
    /// Block-compressed formats are re-encoded, which is lossy.
    /// </summary>
    /// <param name="mipLevel">The mip level (0 is the largest).</param>
    /// <param name="arrayIndex">The array element index.</param>
    /// <param name="depthSlice">The depth slice index (for 3D textures).</param>
    /// <param name="pixels">One RGBA pixel per slice pixel in row-major order.</param>
    public void EncodeSlice(int mipLevel, int arrayIndex, int depthSlice, ReadOnlySpan<Vector4> pixels)
    {
        ref readonly ImageSlice slice = ref GetSlice(mipLevel, arrayIndex, depthSlice);

        if (pixels.Length != slice.Width * slice.Height)
            throw new ArgumentException($"Expected {slice.Width * slice.Height} pixels but received {pixels.Length}.", nameof(pixels));

        PixelCodecs.GetCodec(Format).Encode(pixels, slice.PixelSpan, slice.Width, slice.Height);
    }

    private int GetSliceIndex(int mipLevel, int arrayIndex, int depthSlice)
    {
        int slicesPerArrayElement = _slices.Length / ArraySize;
        int index = arrayIndex * slicesPerArrayElement + depthSlice;

        for (int mip = 0; mip < mipLevel; mip++)
            index += Math.Max(1, Depth >> mip);

        return index;
    }

    private static void ConvertSlice(in ImageSlice source, IPixelCodec sourceCodec, in ImageSlice destination, IPixelCodec targetCodec, ImageConvertFlags flags)
    {
        bool fastBc7 = (flags & ImageConvertFlags.PreferFastBc7Encoding) != 0 && targetCodec is BC7Codec;
        bool fastBc6H = (flags & ImageConvertFlags.PreferFastBc6HEncoding) != 0 && targetCodec is BC6HCodec;

        if (!fastBc7 && !fastBc6H)
        {
            targetCodec.ConvertFrom(source.PixelSpan, sourceCodec, destination.PixelSpan, source.Width, source.Height);
            return;
        }

        Vector4[] pixels = new Vector4[source.Width * source.Height];
        sourceCodec.Decode(source.PixelSpan, pixels, source.Width, source.Height);

        if (fastBc7)
            BC7Codec.EncodeFast(pixels, destination.PixelSpan, source.Width, source.Height);
        else
            ((BC6HCodec)targetCodec).EncodeFast(pixels, destination.PixelSpan, source.Width, source.Height);
    }

    private static ImageSlice[] BuildSlices(ImageInfo info, Memory<byte> pixels)
    {
        var slices = new ImageSlice[info.CalculateSliceCount()];
        int index = 0;
        int offset = 0;

        for (int arrayIndex = 0; arrayIndex < info.ArraySize; arrayIndex++)
        {
            for (int mip = 0; mip < info.MipLevels; mip++)
            {
                int mipWidth = Math.Max(1, info.Width >> mip);
                int mipHeight = Math.Max(1, info.Height >> mip);
                int mipDepth = Math.Max(1, info.Depth >> mip);
                var (rowPitch, slicePitch) = ImageFormatInfo.CalculatePitch(info.Format, mipWidth, mipHeight);

                for (int depthIndex = 0; depthIndex < mipDepth; depthIndex++)
                {
                    slices[index++] = new ImageSlice(mipWidth, mipHeight, info.Format, rowPitch, slicePitch, pixels.Slice(offset, slicePitch), mip, arrayIndex, depthIndex);
                    offset += slicePitch;
                }
            }
        }

        return slices;
    }
}
