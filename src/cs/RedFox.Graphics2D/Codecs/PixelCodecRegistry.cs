using System.Collections.Concurrent;

namespace RedFox.Graphics2D.Codecs
{
    /// <summary>
    /// A thread-safe registry that maps <see cref="ImageFormat"/> values to <see cref="IPixelCodec"/> instances.
    /// Use <see cref="Default"/> for the shared instance with built-in codecs pre-registered,
    /// or create a new instance for isolated codec sets.
    /// </summary>
    public sealed class PixelCodecRegistry
    {
        private readonly ConcurrentDictionary<ImageFormat, IPixelCodec> _codecs = new();

        /// <summary>
        /// Gets the default shared registry instance with all built-in codecs pre-registered.
        /// </summary>
        public static PixelCodecRegistry Default { get; } = CreateDefault();

        /// <summary>
        /// Initializes a new empty <see cref="PixelCodecRegistry"/>.
        /// </summary>
        public PixelCodecRegistry()
        {
        }

        private static PixelCodecRegistry CreateDefault()
        {
            var registry = new PixelCodecRegistry();
            registry.Register(new R8G8B8A8Codec(ImageFormat.R8G8B8A8Unorm));
            registry.Register(new R8G8B8A8Codec(ImageFormat.R8G8B8A8UnormSrgb));
            registry.Register(new B8G8R8A8Codec(ImageFormat.B8G8R8A8Unorm));
            registry.Register(new B8G8R8A8Codec(ImageFormat.B8G8R8A8UnormSrgb));
            registry.Register(new R32G32B32A32FloatCodec());
            registry.Register(new R16G16B16A16FloatCodec());
            registry.Register(new BC1Codec(ImageFormat.BC1Unorm));
            registry.Register(new BC1Codec(ImageFormat.BC1UnormSrgb));
            registry.Register(new BC1Codec(ImageFormat.BC1Typeless));
            registry.Register(new BC2Codec(ImageFormat.BC2Unorm));
            registry.Register(new BC2Codec(ImageFormat.BC2UnormSrgb));
            registry.Register(new BC2Codec(ImageFormat.BC2Typeless));
            registry.Register(new BC3Codec(ImageFormat.BC3Unorm));
            registry.Register(new BC3Codec(ImageFormat.BC3UnormSrgb));
            registry.Register(new BC3Codec(ImageFormat.BC3Typeless));
            registry.Register(new BC4Codec(ImageFormat.BC4Unorm));
            registry.Register(new BC4Codec(ImageFormat.BC4Snorm));
            registry.Register(new BC4Codec(ImageFormat.BC4Typeless));
            registry.Register(new BC5Codec(ImageFormat.BC5Unorm));
            registry.Register(new BC5Codec(ImageFormat.BC5Snorm));
            registry.Register(new BC5Codec(ImageFormat.BC5Typeless));
            registry.Register(new BC6HCodec(ImageFormat.BC6HUF16));
            registry.Register(new BC6HCodec(ImageFormat.BC6HSF16));
            registry.Register(new BC6HCodec(ImageFormat.BC6HTypeless));
            registry.Register(new BC7Codec(ImageFormat.BC7Unorm));
            registry.Register(new BC7Codec(ImageFormat.BC7UnormSrgb));
            registry.Register(new BC7Codec(ImageFormat.BC7Typeless));
            return registry;
        }

        /// <summary>
        /// Registers a codec, replacing any existing codec for the same format.
        /// </summary>
        /// <param name="codec">The codec instance to register.</param>
        public void Register(IPixelCodec codec)
        {
            _codecs[codec.Format] = codec;
        }

        /// <summary>
        /// Retrieves the codec for the specified format.
        /// </summary>
        /// <param name="format">The image format to look up.</param>
        /// <returns>The registered codec for the format.</returns>
        /// <exception cref="NotSupportedException">Thrown if no codec is registered for the format.</exception>
        public IPixelCodec GetCodec(ImageFormat format)
        {
            if (_codecs.TryGetValue(format, out var codec))
                return codec;

            throw new NotSupportedException($"No pixel codec registered for format {format}.");
        }

        /// <summary>
        /// Attempts to retrieve the codec for the specified format.
        /// </summary>
        /// <param name="format">The image format to look up.</param>
        /// <param name="codec">The registered codec, or null if not found.</param>
        /// <returns>True if a codec was found, false otherwise.</returns>
        public bool TryGetCodec(ImageFormat format, out IPixelCodec? codec)
        {
            return _codecs.TryGetValue(format, out codec);
        }
    }
}
