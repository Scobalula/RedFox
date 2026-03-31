using System.Diagnostics.CodeAnalysis;
using RedFox.Graphics2D.BC;
using RedFox.Graphics2D.Codecs;

namespace RedFox.Graphics2D
{
    /// <summary>
    /// Resolves the built-in pixel codec for a known <see cref="ImageFormat"/>.
    /// </summary>
    public static class PixelCodec
    {
        private static readonly IPixelCodec R8G8B8A8Unorm = new R8G8B8A8Codec(ImageFormat.R8G8B8A8Unorm);
        private static readonly IPixelCodec R8G8B8A8UnormSrgb = new R8G8B8A8Codec(ImageFormat.R8G8B8A8UnormSrgb);
        private static readonly IPixelCodec B8G8R8A8Unorm = new B8G8R8A8Codec(ImageFormat.B8G8R8A8Unorm);
        private static readonly IPixelCodec B8G8R8A8UnormSrgb = new B8G8R8A8Codec(ImageFormat.B8G8R8A8UnormSrgb);
        private static readonly IPixelCodec R32G32B32A32Float = new R32G32B32A32FloatCodec();
        private static readonly IPixelCodec R16G16B16A16Float = new R16G16B16A16FloatCodec();

        private static readonly IPixelCodec Bc1Typeless = new BC1Codec(ImageFormat.BC1Typeless);
        private static readonly IPixelCodec Bc1Unorm = new BC1Codec(ImageFormat.BC1Unorm);
        private static readonly IPixelCodec Bc1UnormSrgb = new BC1Codec(ImageFormat.BC1UnormSrgb);
        private static readonly IPixelCodec Bc2Typeless = new BC2Codec(ImageFormat.BC2Typeless);
        private static readonly IPixelCodec Bc2Unorm = new BC2Codec(ImageFormat.BC2Unorm);
        private static readonly IPixelCodec Bc2UnormSrgb = new BC2Codec(ImageFormat.BC2UnormSrgb);
        private static readonly IPixelCodec Bc3Typeless = new BC3Codec(ImageFormat.BC3Typeless);
        private static readonly IPixelCodec Bc3Unorm = new BC3Codec(ImageFormat.BC3Unorm);
        private static readonly IPixelCodec Bc3UnormSrgb = new BC3Codec(ImageFormat.BC3UnormSrgb);
        private static readonly IPixelCodec Bc4Typeless = new BC4Codec(ImageFormat.BC4Typeless);
        private static readonly IPixelCodec Bc4Unorm = new BC4Codec(ImageFormat.BC4Unorm);
        private static readonly IPixelCodec Bc4Snorm = new BC4Codec(ImageFormat.BC4Snorm);
        private static readonly IPixelCodec Bc5Typeless = new BC5Codec(ImageFormat.BC5Typeless);
        private static readonly IPixelCodec Bc5Unorm = new BC5Codec(ImageFormat.BC5Unorm);
        private static readonly IPixelCodec Bc5Snorm = new BC5Codec(ImageFormat.BC5Snorm);
        private static readonly IPixelCodec Bc6HTypeless = new BC6HCodec(ImageFormat.BC6HTypeless);
        private static readonly IPixelCodec Bc6HUf16 = new BC6HCodec(ImageFormat.BC6HUF16);
        private static readonly IPixelCodec Bc6HSf16 = new BC6HCodec(ImageFormat.BC6HSF16);
        private static readonly IPixelCodec Bc7Typeless = new BC7Codec(ImageFormat.BC7Typeless);
        private static readonly IPixelCodec Bc7Unorm = new BC7Codec(ImageFormat.BC7Unorm);
        private static readonly IPixelCodec Bc7UnormSrgb = new BC7Codec(ImageFormat.BC7UnormSrgb);

        /// <summary>
        /// Gets the codec for the requested image format.
        /// </summary>
        /// <param name="format">The format whose codec should be returned.</param>
        /// <returns>The codec for <paramref name="format"/>.</returns>
        /// <exception cref="NotSupportedException">Thrown when <paramref name="format"/> has no built-in codec.</exception>
        public static IPixelCodec GetCodec(ImageFormat format)
        {
            if (TryGetCodec(format, out IPixelCodec? codec))
                return codec;

            throw new NotSupportedException($"No pixel codec is registered for format {format}.");
        }

        /// <summary>
        /// Attempts to get the codec for the requested image format.
        /// </summary>
        /// <param name="format">The format whose codec should be returned.</param>
        /// <param name="codec">The resolved codec when found; otherwise <see langword="null"/>.</param>
        /// <returns><see langword="true"/> when a codec exists for <paramref name="format"/>; otherwise <see langword="false"/>.</returns>
        public static bool TryGetCodec(ImageFormat format, [NotNullWhen(true)] out IPixelCodec? codec)
        {
            codec = format switch
            {
                ImageFormat.R8G8B8A8Unorm => R8G8B8A8Unorm,
                ImageFormat.R8G8B8A8UnormSrgb => R8G8B8A8UnormSrgb,
                ImageFormat.B8G8R8A8Unorm => B8G8R8A8Unorm,
                ImageFormat.B8G8R8A8UnormSrgb => B8G8R8A8UnormSrgb,
                ImageFormat.R32G32B32A32Float => R32G32B32A32Float,
                ImageFormat.R16G16B16A16Float => R16G16B16A16Float,
                ImageFormat.BC1Typeless => Bc1Typeless,
                ImageFormat.BC1Unorm => Bc1Unorm,
                ImageFormat.BC1UnormSrgb => Bc1UnormSrgb,
                ImageFormat.BC2Typeless => Bc2Typeless,
                ImageFormat.BC2Unorm => Bc2Unorm,
                ImageFormat.BC2UnormSrgb => Bc2UnormSrgb,
                ImageFormat.BC3Typeless => Bc3Typeless,
                ImageFormat.BC3Unorm => Bc3Unorm,
                ImageFormat.BC3UnormSrgb => Bc3UnormSrgb,
                ImageFormat.BC4Typeless => Bc4Typeless,
                ImageFormat.BC4Unorm => Bc4Unorm,
                ImageFormat.BC4Snorm => Bc4Snorm,
                ImageFormat.BC5Typeless => Bc5Typeless,
                ImageFormat.BC5Unorm => Bc5Unorm,
                ImageFormat.BC5Snorm => Bc5Snorm,
                ImageFormat.BC6HTypeless => Bc6HTypeless,
                ImageFormat.BC6HUF16 => Bc6HUf16,
                ImageFormat.BC6HSF16 => Bc6HSf16,
                ImageFormat.BC7Typeless => Bc7Typeless,
                ImageFormat.BC7Unorm => Bc7Unorm,
                ImageFormat.BC7UnormSrgb => Bc7UnormSrgb,
                _ => null,
            };

            return codec is not null;
        }
    }
}
