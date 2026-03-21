using System.Collections.Concurrent;

namespace RedFox.Graphics2D.Codecs
{
    /// <summary>
    /// A thread-safe registry that maps <see cref="ImageFormat"/> values to <see cref="IPixelCodec"/> instances.
    /// Built-in codecs are registered automatically. Custom codecs can be registered at runtime.
    /// </summary>
    public static class PixelCodecRegistry
    {
        private static readonly ConcurrentDictionary<ImageFormat, IPixelCodec> _codecs = new();

        static PixelCodecRegistry()
        {
            Register(new R8G8B8A8Codec(ImageFormat.R8G8B8A8Unorm));
            Register(new R8G8B8A8Codec(ImageFormat.R8G8B8A8UnormSrgb));
            Register(new B8G8R8A8Codec(ImageFormat.B8G8R8A8Unorm));
            Register(new B8G8R8A8Codec(ImageFormat.B8G8R8A8UnormSrgb));
            Register(new R32G32B32A32FloatCodec());
            Register(new R16G16B16A16FloatCodec());
            Register(new BC1Codec(ImageFormat.BC1Unorm));
            Register(new BC1Codec(ImageFormat.BC1UnormSrgb));
            Register(new BC1Codec(ImageFormat.BC1Typeless));
            Register(new BC2Codec(ImageFormat.BC2Unorm));
            Register(new BC2Codec(ImageFormat.BC2UnormSrgb));
            Register(new BC2Codec(ImageFormat.BC2Typeless));
            Register(new BC3Codec(ImageFormat.BC3Unorm));
            Register(new BC3Codec(ImageFormat.BC3UnormSrgb));
            Register(new BC3Codec(ImageFormat.BC3Typeless));
            Register(new BC4Codec(ImageFormat.BC4Unorm));
            Register(new BC4Codec(ImageFormat.BC4Snorm));
            Register(new BC4Codec(ImageFormat.BC4Typeless));
            Register(new BC5Codec(ImageFormat.BC5Unorm));
            Register(new BC5Codec(ImageFormat.BC5Snorm));
            Register(new BC5Codec(ImageFormat.BC5Typeless));
            Register(new BC6HCodec(ImageFormat.BC6HUF16));
            Register(new BC6HCodec(ImageFormat.BC6HSF16));
            Register(new BC6HCodec(ImageFormat.BC6HTypeless));
            Register(new BC7Codec(ImageFormat.BC7Unorm));
            Register(new BC7Codec(ImageFormat.BC7UnormSrgb));
            Register(new BC7Codec(ImageFormat.BC7Typeless));
        }

        /// <summary>
        /// Registers a codec. Replaces any existing codec for the same format.
        /// </summary>
        public static void Register(IPixelCodec codec)
        {
            _codecs[codec.Format] = codec;
        }

        /// <summary>
        /// Retrieves the codec for the specified format.
        /// </summary>
        /// <exception cref="NotSupportedException">Thrown if no codec is registered for the format.</exception>
        public static IPixelCodec GetCodec(ImageFormat format)
        {
            if (_codecs.TryGetValue(format, out var codec))
                return codec;

            throw new NotSupportedException($"No pixel codec registered for format {format}.");
        }

        /// <summary>
        /// Attempts to retrieve the codec for the specified format.
        /// </summary>
        public static bool TryGetCodec(ImageFormat format, out IPixelCodec? codec)
        {
            return _codecs.TryGetValue(format, out codec);
        }
    }
}
