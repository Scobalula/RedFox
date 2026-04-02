using System.Numerics;
using System.Runtime.InteropServices;
using RedFox.Graphics2D.BC;
using RedFox.Graphics2D.Codecs;
using RedFox.Graphics2D.Conversion;

namespace RedFox.Graphics2D
{
    /// <summary>
    /// A container for texture/image data modeled after DirectXTex ScratchImage.
    /// Holds all mip levels, array slices, and depth slices in a single contiguous buffer.
    /// </summary>
    public sealed class Image
    {
        private byte[] _pixels;
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
        /// Gets the depth of the image (for 3D/volume textures). Typically 1.
        /// </summary>
        public int Depth { get; }

        /// <summary>
        /// Gets the number of elements in the texture array (e.g. 6 for cube maps). Typically 1.
        /// </summary>
        public int ArraySize { get; }

        /// <summary>
        /// Gets the number of mip map levels.
        /// </summary>
        public int MipLevels { get; }

        /// <summary>
        /// Gets the pixel format of the image.
        /// </summary>
        public ImageFormat Format { get; set; }

        /// <summary>
        /// Gets whether this image represents a cube map texture.
        /// </summary>
        public bool IsCubemap { get; }

        /// <summary>
        /// Gets the total number of sub-image slices.
        /// </summary>
        public int SliceCount => _slices.Length;

        /// <summary>
        /// Gets a read-only span over all sub-image slices.
        /// </summary>
        public ReadOnlySpan<ImageSlice> Slices => _slices;

        /// <summary>
        /// Gets the entire pixel buffer as a span.
        /// </summary>
        public Span<byte> PixelData => _pixels;

        /// <summary>
        /// Gets the entire pixel buffer as a <see cref="Memory{T}"/>.
        /// </summary>
        public Memory<byte> PixelMemory => _pixels;

        /// <summary>
        /// Initializes a new <see cref="Image"/> with the given dimensions, format, and optional data.
        /// </summary>
        /// <param name="width">Width of the top-level image in pixels.</param>
        /// <param name="height">Height of the top-level image in pixels.</param>
        /// <param name="depth">Depth of the image (1 for 2D textures).</param>
        /// <param name="arraySize">Number of array elements (1 for non-array textures, 6 for cube maps).</param>
        /// <param name="mipLevels">Number of mip map levels (1 for no mipmaps).</param>
        /// <param name="format">The pixel format.</param>
        /// <param name="isCubemap">Whether this image is a cube map texture.</param>
        /// <param name="data">Optional initial pixel data. If null, a zeroed buffer is allocated.</param>
        public Image(int width, int height, int depth, int arraySize, int mipLevels, ImageFormat format, bool isCubemap, byte[]? data)
        {
            ArgumentOutOfRangeException.ThrowIfLessThan(width, 1);
            ArgumentOutOfRangeException.ThrowIfLessThan(height, 1);
            ArgumentOutOfRangeException.ThrowIfLessThan(depth, 1);
            ArgumentOutOfRangeException.ThrowIfLessThan(arraySize, 1);
            ArgumentOutOfRangeException.ThrowIfLessThan(mipLevels, 1);

            Width = width;
            Height = height;
            Depth = depth;
            ArraySize = arraySize;
            MipLevels = mipLevels;
            Format = format;
            IsCubemap = isCubemap;

            var totalSize = CalculateTotalSize(width, height, depth, arraySize, mipLevels, format);

            if (data != null)
            {
                if (data.Length < totalSize)
                    throw new ArgumentException($"Provided data ({data.Length} bytes) is smaller than the required size ({totalSize} bytes).", nameof(data));

                _pixels = data;
            }
            else
            {
                _pixels = new byte[totalSize];
            }

            _slices = BuildSlices(width, height, depth, arraySize, mipLevels, format, _pixels);
        }

        /// <summary>
        /// Initializes a new <see cref="Image"/> with the given dimensions and format, without initial data.
        /// </summary>
        /// <param name="width">Width of the top-level image in pixels.</param>
        /// <param name="height">Height of the top-level image in pixels.</param>
        /// <param name="depth">Depth of the image (1 for 2D textures).</param>
        /// <param name="arraySize">Number of array elements (1 for non-array textures, 6 for cube maps).</param>
        /// <param name="mipLevels">Number of mip map levels (1 for no mipmaps).</param>
        /// <param name="format">The pixel format.</param>
        /// <param name="isCubemap">Whether this image is a cube map texture.</param>
        public Image(int width, int height, int depth, int arraySize, int mipLevels, ImageFormat format, bool isCubemap)
            : this(width, height, depth, arraySize, mipLevels, format, isCubemap, null)
        {
        }

        /// <summary>
        /// Initializes a new <see cref="Image"/> with the given dimensions and format.
        /// </summary>
        /// <param name="width">Width of the top-level image in pixels.</param>
        /// <param name="height">Height of the top-level image in pixels.</param>
        /// <param name="depth">Depth of the image (1 for 2D textures).</param>
        /// <param name="arraySize">Number of array elements (1 for non-array textures, 6 for cube maps).</param>
        /// <param name="mipLevels">Number of mip map levels (1 for no mipmaps).</param>
        /// <param name="format">The pixel format.</param>
        public Image(int width, int height, int depth, int arraySize, int mipLevels, ImageFormat format)
            : this(width, height, depth, arraySize, mipLevels, format, false, null)
        {
        }

        /// <summary>
        /// Initializes a new <see cref="Image"/> with the given dimensions and initial data.
        /// </summary>
        /// <param name="width">Width of the top-level image in pixels.</param>
        /// <param name="height">Height of the top-level image in pixels.</param>
        /// <param name="depth">Depth of the image (1 for 2D textures).</param>
        /// <param name="arraySize">Number of array elements (1 for non-array textures, 6 for cube maps).</param>
        /// <param name="mipLevels">Number of mip map levels (1 for no mipmaps).</param>
        /// <param name="format">The pixel format.</param>
        /// <param name="data">Initial pixel data.</param>
        public Image(int width, int height, int depth, int arraySize, int mipLevels, ImageFormat format, byte[] data)
            : this(width, height, depth, arraySize, mipLevels, format, false, data)
        {
        }

        /// <summary>
        /// Creates a simple 2D image with no mip maps and array size 1.
        /// </summary>
        /// <param name="width">Width of the image in pixels.</param>
        /// <param name="height">Height of the image in pixels.</param>
        /// <param name="format">The pixel format.</param>
        /// <param name="data">Initial pixel data.</param>
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
        /// Gets the <see cref="ImageSlice"/> for the specified mip level and array element.
        /// </summary>
        /// <param name="mipLevel">The mip level (0 is the largest).</param>
        /// <param name="arrayIndex">The array element index.</param>
        /// <param name="depthSlice">The depth slice index (for 3D textures).</param>
        /// <returns>The corresponding <see cref="ImageSlice"/>.</returns>
        public ref readonly ImageSlice GetSlice(int mipLevel, int arrayIndex, int depthSlice)
        {
            var mipDepth = Math.Max(1, Depth >> mipLevel);

            ArgumentOutOfRangeException.ThrowIfGreaterThanOrEqual(mipLevel, MipLevels);
            ArgumentOutOfRangeException.ThrowIfGreaterThanOrEqual(arrayIndex, ArraySize);
            ArgumentOutOfRangeException.ThrowIfGreaterThanOrEqual(depthSlice, mipDepth);

            var index = GetSliceIndex(mipLevel, arrayIndex, depthSlice);
            return ref _slices[index];
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
        /// Converts this image in-place to the specified target format using the built-in codec resolver.
        /// Uses direct byte-to-byte conversion when possible, falling back to <see cref="Vector4"/>
        /// intermediates only when needed. Supports conversion to and from block-compressed (BCn) formats.
        /// </summary>
        /// <param name="targetFormat">The target format.</param>
        public void Convert(ImageFormat targetFormat)
        {
            Convert(targetFormat, ImageConvertFlags.None, ConverterEngine.None);
        }

        /// <summary>
        /// Converts this image in-place to the specified target format using the supplied converter engine.
        /// Falls back to built-in codecs when the engine does not handle the conversion.
        /// </summary>
        /// <param name="targetFormat">The target format.</param>
        /// <param name="converterEngine">Optional custom converter engine.</param>
        public void Convert(ImageFormat targetFormat, ConverterEngine? converterEngine)
        {
            Convert(targetFormat, ImageConvertFlags.None, converterEngine);
        }

        /// <summary>
        /// Converts this image in-place to the specified target format using the built-in codec resolver.
        /// Uses direct byte-to-byte conversion when possible, falling back to <see cref="Vector4"/>
        /// intermediates only when needed. Supports conversion to and from block-compressed (BCn) formats.
        /// Optional <paramref name="flags"/> can prefer faster BC6H/BC7 CPU encoders when those are the target.
        /// </summary>
        /// <param name="targetFormat">The target format.</param>
        /// <param name="flags">Optional conversion hints that can alter encoder selection.</param>
        public void Convert(ImageFormat targetFormat, ImageConvertFlags flags)
        {
            Convert(targetFormat, flags, ConverterEngine.None);
        }

        /// <summary>
        /// Converts this image in-place to the specified target format using a custom converter engine first.
        /// Uses built-in CPU codecs when the engine is null, unsupported, or fails conversion.
        /// </summary>
        /// <param name="targetFormat">The target format.</param>
        /// <param name="flags">Optional conversion hints that can alter encoder selection.</param>
        /// <param name="converterEngine">Optional custom converter engine.</param>
        public void Convert(ImageFormat targetFormat, ImageConvertFlags flags, ConverterEngine? converterEngine)
        {
            if (Format == targetFormat)
                return;

            IPixelCodec sourceCodec = PixelCodec.GetCodec(Format);
            IPixelCodec targetCodec = PixelCodec.GetCodec(targetFormat);

            int totalSize = CalculateTotalSize(Width, Height, Depth, ArraySize, MipLevels, targetFormat);
            byte[] newPixels = new byte[totalSize];
            ImageSlice[] newSlices = BuildSlices(Width, Height, Depth, ArraySize, MipLevels, targetFormat, newPixels);

            for (int i = 0; i < _slices.Length; i++)
            {
                ref readonly ImageSlice srcSlice = ref _slices[i];
                ref readonly ImageSlice dstSlice = ref newSlices[i];

                if (!TryConvertWithEngine(converterEngine, srcSlice.PixelSpan, Format, dstSlice.PixelSpan, targetFormat, srcSlice.Width, srcSlice.Height, flags) &&
                    !TryConvertWithFlags(sourceCodec, targetCodec, srcSlice.PixelSpan, dstSlice.PixelSpan, srcSlice.Width, srcSlice.Height, flags))
                {
                    targetCodec.ConvertFrom(srcSlice.PixelSpan, sourceCodec, dstSlice.PixelSpan, srcSlice.Width, srcSlice.Height);
                }
            }

            _pixels = newPixels;
            _slices = newSlices;
            Format = targetFormat;
        }

        private static bool TryConvertWithEngine(
            ConverterEngine? converterEngine,
            ReadOnlySpan<byte> source,
            ImageFormat sourceFormat,
            Span<byte> destination,
            ImageFormat destinationFormat,
            int width,
            int height,
            ImageConvertFlags flags)
        {
            if (converterEngine is null || ReferenceEquals(converterEngine, ConverterEngine.None))
                return false;

            return converterEngine.TryConvert(source, sourceFormat, destination, destinationFormat, width, height, flags);
        }

        private static bool TryConvertWithFlags(
            IPixelCodec sourceCodec,
            IPixelCodec targetCodec,
            ReadOnlySpan<byte> source,
            Span<byte> destination,
            int width,
            int height,
            ImageConvertFlags flags)
        {
            if ((flags & ImageConvertFlags.PreferFastBc7Encoding) != 0 && targetCodec is BC7Codec bc7Codec)
            {
                Vector4[] pixels = new Vector4[width * height];
                sourceCodec.Decode(source, pixels, width, height);
                bc7Codec.EncodeFast(pixels, destination, width, height);
                return true;
            }

            if ((flags & ImageConvertFlags.PreferFastBc6HEncoding) != 0 && targetCodec is BC6HCodec bc6hCodec)
            {
                Vector4[] pixels = new Vector4[width * height];
                sourceCodec.Decode(source, pixels, width, height);
                bc6hCodec.EncodeFast(pixels, destination, width, height);
                return true;
            }

            return false;
        }

        /// <summary>
        /// Gets the entire pixel buffer reinterpreted as a span of <typeparamref name="T"/>.
        /// This is a zero-copy operation — no allocation or data conversion is performed.
        /// The caller is responsible for ensuring the format's byte layout matches <typeparamref name="T"/>.
        /// </summary>
        /// <typeparam name="T">The unmanaged element type to reinterpret the pixel buffer as.</typeparam>
        /// <returns>A span over the underlying pixel buffer reinterpreted as <typeparamref name="T"/> values.</returns>
        public Span<T> GetPixelData<T>() where T : unmanaged
        {
            return MemoryMarshal.Cast<byte, T>((Span<byte>)_pixels);
        }

        /// <summary>
        /// Decodes all pixels of a specific slice to <see cref="Vector4"/> values using the built-in codec resolver.
        /// </summary>
        /// <param name="mipLevel">The mip level (0 is the largest).</param>
        /// <param name="arrayIndex">The array element index.</param>
        /// <param name="depthSlice">The depth slice index (for 3D textures).</param>
        /// <returns>An array containing one decoded <see cref="Vector4"/> per pixel.</returns>
        public Vector4[] DecodeSlice(int mipLevel, int arrayIndex, int depthSlice)
        {
            ref readonly ImageSlice slice = ref GetSlice(mipLevel, arrayIndex, depthSlice);
            IPixelCodec codec = PixelCodec.GetCodec(Format);
            Vector4[] result = new Vector4[slice.Width * slice.Height];
            codec.Decode(slice.PixelSpan, result, slice.Width, slice.Height);
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
        /// Decodes all pixels of a specific slice to values of type <typeparamref name="T"/> using the built-in codec resolver.
        /// Each pixel produces 4 component values (RGBA).
        /// </summary>
        /// <typeparam name="T">The numeric component type to convert each RGBA channel into.</typeparam>
        /// <param name="mipLevel">The mip level (0 is the largest).</param>
        /// <param name="arrayIndex">The array element index.</param>
        /// <param name="depthSlice">The depth slice index (for 3D textures).</param>
        /// <returns>An array containing interleaved RGBA component values for every decoded pixel.</returns>
        public T[] DecodeSlice<T>(int mipLevel, int arrayIndex, int depthSlice) where T : INumber<T>
        {
            Vector4[] vec4Data = DecodeSlice(mipLevel, arrayIndex, depthSlice);
            T[] result = new T[vec4Data.Length * 4];

            for (int i = 0; i < vec4Data.Length; i++)
            {
                ref Vector4 v = ref vec4Data[i];
                int offset = i * 4;
                result[offset + 0] = T.CreateSaturating(v.X);
                result[offset + 1] = T.CreateSaturating(v.Y);
                result[offset + 2] = T.CreateSaturating(v.Z);
                result[offset + 3] = T.CreateSaturating(v.W);
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

        private int GetSliceIndex(int mipLevel, int arrayIndex, int depthSlice)
        {
            int index = 0;

            for (int arr = 0; arr < arrayIndex; arr++)
            {
                for (int mip = 0; mip < MipLevels; mip++)
                {
                    index += Math.Max(1, Depth >> mip);
                }
            }

            for (int mip = 0; mip < mipLevel; mip++)
            {
                index += Math.Max(1, Depth >> mip);
            }

            index += depthSlice;
            return index;
        }

        private static ImageSlice[] BuildSlices(int width, int height, int depth, int arraySize, int mipLevels, ImageFormat format, byte[] pixels)
        {
            int sliceCount = CalculateSliceCount(depth, arraySize, mipLevels);
            var slices = new ImageSlice[sliceCount];
            int index = 0;
            int offset = 0;

            for (int arr = 0; arr < arraySize; arr++)
            {
                int mipWidth = width;
                int mipHeight = height;
                int mipDepth = depth;

                for (int mip = 0; mip < mipLevels; mip++)
                {
                    var (rowPitch, slicePitch) = ImageFormatInfo.CalculatePitch(format, mipWidth, mipHeight);

                    for (int d = 0; d < mipDepth; d++)
                    {
                        slices[index++] = new ImageSlice(
                            mipWidth,
                            mipHeight,
                            format,
                            rowPitch,
                            slicePitch,
                            new Memory<byte>(pixels, offset, slicePitch));

                        offset += slicePitch;
                    }

                    mipWidth = Math.Max(1, mipWidth >> 1);
                    mipHeight = Math.Max(1, mipHeight >> 1);
                    mipDepth = Math.Max(1, mipDepth >> 1);
                }
            }

            return slices;
        }

        private static int CalculateTotalSize(int width, int height, int depth, int arraySize, int mipLevels, ImageFormat format)
        {
            int total = 0;

            for (int arr = 0; arr < arraySize; arr++)
            {
                int mipWidth = width;
                int mipHeight = height;
                int mipDepth = depth;

                for (int mip = 0; mip < mipLevels; mip++)
                {
                    var (_, slicePitch) = ImageFormatInfo.CalculatePitch(format, mipWidth, mipHeight);
                    total += slicePitch * mipDepth;

                    mipWidth = Math.Max(1, mipWidth >> 1);
                    mipHeight = Math.Max(1, mipHeight >> 1);
                    mipDepth = Math.Max(1, mipDepth >> 1);
                }
            }

            return total;
        }

        private static int CalculateSliceCount(int depth, int arraySize, int mipLevels)
        {
            int count = 0;
            for (int arr = 0; arr < arraySize; arr++)
            {
                int mipDepth = depth;
                for (int mip = 0; mip < mipLevels; mip++)
                {
                    count += mipDepth;
                    mipDepth = Math.Max(1, mipDepth >> 1);
                }
            }
            return count;
        }
    }
}
