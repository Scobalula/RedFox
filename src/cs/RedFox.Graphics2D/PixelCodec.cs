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
        /// <summary>
        /// Codec for <see cref="ImageFormat.R8G8B8A8Unorm"/>.
        /// </summary>
        public static readonly IPixelCodec R8G8B8A8Unorm = new R8G8B8A8Codec(ImageFormat.R8G8B8A8Unorm);

        /// <summary>
        /// Codec for <see cref="ImageFormat.R8G8B8A8UnormSrgb"/>.
        /// </summary>
        public static readonly IPixelCodec R8G8B8A8UnormSrgb = new R8G8B8A8Codec(ImageFormat.R8G8B8A8UnormSrgb);

        /// <summary>
        /// Codec for <see cref="ImageFormat.B8G8R8A8Unorm"/>.
        /// </summary>
        public static readonly IPixelCodec B8G8R8A8Unorm = new B8G8R8A8Codec(ImageFormat.B8G8R8A8Unorm);

        /// <summary>
        /// Codec for <see cref="ImageFormat.B8G8R8A8UnormSrgb"/>.
        /// </summary>
        public static readonly IPixelCodec B8G8R8A8UnormSrgb = new B8G8R8A8Codec(ImageFormat.B8G8R8A8UnormSrgb);

        /// <summary>
        /// Codec for <see cref="ImageFormat.R32G32B32A32Float"/>.
        /// </summary>
        public static readonly IPixelCodec R32G32B32A32Float = new R32G32B32A32FloatCodec();

        /// <summary>
        /// Codec for <see cref="ImageFormat.R16G16B16A16Float"/>.
        /// </summary>
        public static readonly IPixelCodec R16G16B16A16Float = new R16G16B16A16FloatCodec();

        /// <summary>
        /// Codec for <see cref="ImageFormat.R32G32B32A32Typeless"/>.
        /// </summary>
        public static readonly IPixelCodec R32G32B32A32Typeless = new R32G32B32A32Codec(ImageFormat.R32G32B32A32Typeless);

        /// <summary>
        /// Codec for <see cref="ImageFormat.R32G32B32A32Uint"/>.
        /// </summary>
        public static readonly IPixelCodec R32G32B32A32Uint = new R32G32B32A32Codec(ImageFormat.R32G32B32A32Uint);

        /// <summary>
        /// Codec for <see cref="ImageFormat.R32G32B32A32Sint"/>.
        /// </summary>
        public static readonly IPixelCodec R32G32B32A32Sint = new R32G32B32A32Codec(ImageFormat.R32G32B32A32Sint);

        /// <summary>
        /// Codec for <see cref="ImageFormat.R32G32B32Typeless"/>.
        /// </summary>
        public static readonly IPixelCodec R32G32B32Typeless = new R32G32B32FloatCodec(ImageFormat.R32G32B32Typeless);

        /// <summary>
        /// Codec for <see cref="ImageFormat.R32G32B32Float"/>.
        /// </summary>
        public static readonly IPixelCodec R32G32B32Float = new R32G32B32FloatCodec(ImageFormat.R32G32B32Float);

        /// <summary>
        /// Codec for <see cref="ImageFormat.R32G32B32Uint"/>.
        /// </summary>
        public static readonly IPixelCodec R32G32B32Uint = new R32G32B32FloatCodec(ImageFormat.R32G32B32Uint);

        /// <summary>
        /// Codec for <see cref="ImageFormat.R32G32B32Sint"/>.
        /// </summary>
        public static readonly IPixelCodec R32G32B32Sint = new R32G32B32FloatCodec(ImageFormat.R32G32B32Sint);

        /// <summary>
        /// Codec for <see cref="ImageFormat.R16G16B16A16Typeless"/>.
        /// </summary>
        public static readonly IPixelCodec R16G16B16A16Typeless = new R16G16B16A16Codec(ImageFormat.R16G16B16A16Typeless);

        /// <summary>
        /// Codec for <see cref="ImageFormat.R16G16B16A16Unorm"/>.
        /// </summary>
        public static readonly IPixelCodec R16G16B16A16Unorm = new R16G16B16A16Codec(ImageFormat.R16G16B16A16Unorm);

        /// <summary>
        /// Codec for <see cref="ImageFormat.R16G16B16A16Uint"/>.
        /// </summary>
        public static readonly IPixelCodec R16G16B16A16Uint = new R16G16B16A16Codec(ImageFormat.R16G16B16A16Uint);

        /// <summary>
        /// Codec for <see cref="ImageFormat.R16G16B16A16Snorm"/>.
        /// </summary>
        public static readonly IPixelCodec R16G16B16A16Snorm = new R16G16B16A16Codec(ImageFormat.R16G16B16A16Snorm);

        /// <summary>
        /// Codec for <see cref="ImageFormat.R16G16B16A16Sint"/>.
        /// </summary>
        public static readonly IPixelCodec R16G16B16A16Sint = new R16G16B16A16Codec(ImageFormat.R16G16B16A16Sint);

        /// <summary>
        /// Codec for <see cref="ImageFormat.R32G32Typeless"/>.
        /// </summary>
        public static readonly IPixelCodec R32G32Typeless = new R32G32Codec(ImageFormat.R32G32Typeless);

        /// <summary>
        /// Codec for <see cref="ImageFormat.R32G32Float"/>.
        /// </summary>
        public static readonly IPixelCodec R32G32Float = new R32G32Codec(ImageFormat.R32G32Float);

        /// <summary>
        /// Codec for <see cref="ImageFormat.R32G32Uint"/>.
        /// </summary>
        public static readonly IPixelCodec R32G32Uint = new R32G32Codec(ImageFormat.R32G32Uint);

        /// <summary>
        /// Codec for <see cref="ImageFormat.R32G32Sint"/>.
        /// </summary>
        public static readonly IPixelCodec R32G32Sint = new R32G32Codec(ImageFormat.R32G32Sint);

        /// <summary>
        /// Codec for <see cref="ImageFormat.R16G16Typeless"/>.
        /// </summary>
        public static readonly IPixelCodec R16G16Typeless = new R16G16Codec(ImageFormat.R16G16Typeless);

        /// <summary>
        /// Codec for <see cref="ImageFormat.R16G16Float"/>.
        /// </summary>
        public static readonly IPixelCodec R16G16Float = new R16G16Codec(ImageFormat.R16G16Float);

        /// <summary>
        /// Codec for <see cref="ImageFormat.R16G16Unorm"/>.
        /// </summary>
        public static readonly IPixelCodec R16G16Unorm = new R16G16Codec(ImageFormat.R16G16Unorm);

        /// <summary>
        /// Codec for <see cref="ImageFormat.R16G16Uint"/>.
        /// </summary>
        public static readonly IPixelCodec R16G16Uint = new R16G16Codec(ImageFormat.R16G16Uint);

        /// <summary>
        /// Codec for <see cref="ImageFormat.R16G16Snorm"/>.
        /// </summary>
        public static readonly IPixelCodec R16G16Snorm = new R16G16Codec(ImageFormat.R16G16Snorm);

        /// <summary>
        /// Codec for <see cref="ImageFormat.R16G16Sint"/>.
        /// </summary>
        public static readonly IPixelCodec R16G16Sint = new R16G16Codec(ImageFormat.R16G16Sint);

        /// <summary>
        /// Codec for <see cref="ImageFormat.R32Typeless"/>.
        /// </summary>
        public static readonly IPixelCodec R32Typeless = new R32Codec(ImageFormat.R32Typeless);

        /// <summary>
        /// Codec for <see cref="ImageFormat.D32Float"/>.
        /// </summary>
        public static readonly IPixelCodec D32Float = new R32Codec(ImageFormat.D32Float);

        /// <summary>
        /// Codec for <see cref="ImageFormat.R32Float"/>.
        /// </summary>
        public static readonly IPixelCodec R32Float = new R32Codec(ImageFormat.R32Float);

        /// <summary>
        /// Codec for <see cref="ImageFormat.R32Uint"/>.
        /// </summary>
        public static readonly IPixelCodec R32Uint = new R32Codec(ImageFormat.R32Uint);

        /// <summary>
        /// Codec for <see cref="ImageFormat.R32Sint"/>.
        /// </summary>
        public static readonly IPixelCodec R32Sint = new R32Codec(ImageFormat.R32Sint);

        /// <summary>
        /// Codec for <see cref="ImageFormat.R16Typeless"/>.
        /// </summary>
        public static readonly IPixelCodec R16Typeless = new R16Codec(ImageFormat.R16Typeless);

        /// <summary>
        /// Codec for <see cref="ImageFormat.R16Float"/>.
        /// </summary>
        public static readonly IPixelCodec R16Float = new R16Codec(ImageFormat.R16Float);

        /// <summary>
        /// Codec for <see cref="ImageFormat.D16Unorm"/>.
        /// </summary>
        public static readonly IPixelCodec D16Unorm = new R16Codec(ImageFormat.D16Unorm);

        /// <summary>
        /// Codec for <see cref="ImageFormat.R16Unorm"/>.
        /// </summary>
        public static readonly IPixelCodec R16Unorm = new R16Codec(ImageFormat.R16Unorm);

        /// <summary>
        /// Codec for <see cref="ImageFormat.R16Uint"/>.
        /// </summary>
        public static readonly IPixelCodec R16Uint = new R16Codec(ImageFormat.R16Uint);

        /// <summary>
        /// Codec for <see cref="ImageFormat.R16Snorm"/>.
        /// </summary>
        public static readonly IPixelCodec R16Snorm = new R16Codec(ImageFormat.R16Snorm);

        /// <summary>
        /// Codec for <see cref="ImageFormat.R16Sint"/>.
        /// </summary>
        public static readonly IPixelCodec R16Sint = new R16Codec(ImageFormat.R16Sint);

        /// <summary>
        /// Codec for <see cref="ImageFormat.R8G8Typeless"/>.
        /// </summary>
        public static readonly IPixelCodec R8G8Typeless = new R8G8Codec(ImageFormat.R8G8Typeless);

        /// <summary>
        /// Codec for <see cref="ImageFormat.R8G8Unorm"/>.
        /// </summary>
        public static readonly IPixelCodec R8G8Unorm = new R8G8Codec(ImageFormat.R8G8Unorm);

        /// <summary>
        /// Codec for <see cref="ImageFormat.R8G8Uint"/>.
        /// </summary>
        public static readonly IPixelCodec R8G8Uint = new R8G8Codec(ImageFormat.R8G8Uint);

        /// <summary>
        /// Codec for <see cref="ImageFormat.R8G8Snorm"/>.
        /// </summary>
        public static readonly IPixelCodec R8G8Snorm = new R8G8Codec(ImageFormat.R8G8Snorm);

        /// <summary>
        /// Codec for <see cref="ImageFormat.R8G8Sint"/>.
        /// </summary>
        public static readonly IPixelCodec R8G8Sint = new R8G8Codec(ImageFormat.R8G8Sint);

        /// <summary>
        /// Codec for <see cref="ImageFormat.R8Typeless"/>.
        /// </summary>
        public static readonly IPixelCodec R8Typeless = new R8Codec(ImageFormat.R8Typeless);

        /// <summary>
        /// Codec for <see cref="ImageFormat.R8Unorm"/>.
        /// </summary>
        public static readonly IPixelCodec R8Unorm = new R8Codec(ImageFormat.R8Unorm);

        /// <summary>
        /// Codec for <see cref="ImageFormat.R8Uint"/>.
        /// </summary>
        public static readonly IPixelCodec R8Uint = new R8Codec(ImageFormat.R8Uint);

        /// <summary>
        /// Codec for <see cref="ImageFormat.R8Snorm"/>.
        /// </summary>
        public static readonly IPixelCodec R8Snorm = new R8Codec(ImageFormat.R8Snorm);

        /// <summary>
        /// Codec for <see cref="ImageFormat.R8Sint"/>.
        /// </summary>
        public static readonly IPixelCodec R8Sint = new R8Codec(ImageFormat.R8Sint);

        /// <summary>
        /// Codec for <see cref="ImageFormat.A8Unorm"/>.
        /// </summary>
        public static readonly IPixelCodec A8Unorm = new A8Codec();

        /// <summary>
        /// Codec for <see cref="ImageFormat.R10G10B10A2Typeless"/>.
        /// </summary>
        public static readonly IPixelCodec R10G10B10A2Typeless = new R10G10B10A2Codec(ImageFormat.R10G10B10A2Typeless);

        /// <summary>
        /// Codec for <see cref="ImageFormat.R10G10B10A2Unorm"/>.
        /// </summary>
        public static readonly IPixelCodec R10G10B10A2Unorm = new R10G10B10A2Codec(ImageFormat.R10G10B10A2Unorm);

        /// <summary>
        /// Codec for <see cref="ImageFormat.R10G10B10A2Uint"/>.
        /// </summary>
        public static readonly IPixelCodec R10G10B10A2Uint = new R10G10B10A2Codec(ImageFormat.R10G10B10A2Uint);

        /// <summary>
        /// Codec for <see cref="ImageFormat.R10G10B10XrBiasA2Unorm"/>.
        /// </summary>
        public static readonly IPixelCodec R10G10B10XrBiasA2Unorm = new R10G10B10A2Codec(ImageFormat.R10G10B10XrBiasA2Unorm);

        /// <summary>
        /// Codec for <see cref="ImageFormat.R11G11B10Float"/>.
        /// </summary>
        public static readonly IPixelCodec R11G11B10Float = new R11G11B10FloatCodec();

        /// <summary>
        /// Codec for <see cref="ImageFormat.B5G6R5Unorm"/>.
        /// </summary>
        public static readonly IPixelCodec B5G6R5Unorm = new B5G6R5Codec();

        /// <summary>
        /// Codec for <see cref="ImageFormat.B5G5R5A1Unorm"/>.
        /// </summary>
        public static readonly IPixelCodec B5G5R5A1Unorm = new B5G5R5A1Codec();

        /// <summary>
        /// Codec for <see cref="ImageFormat.B4G4R4A4Unorm"/>.
        /// </summary>
        public static readonly IPixelCodec B4G4R4A4Unorm = new B4G4R4A4Codec();

        /// <summary>
        /// Codec for <see cref="ImageFormat.BC1Typeless"/>.
        /// </summary>
        public static readonly IPixelCodec Bc1Typeless = new BC1Codec(ImageFormat.BC1Typeless);

        /// <summary>
        /// Codec for <see cref="ImageFormat.BC1Unorm"/>.
        /// </summary>
        public static readonly IPixelCodec Bc1Unorm = new BC1Codec(ImageFormat.BC1Unorm);

        /// <summary>
        /// Codec for <see cref="ImageFormat.BC1UnormSrgb"/>.
        /// </summary>
        public static readonly IPixelCodec Bc1UnormSrgb = new BC1Codec(ImageFormat.BC1UnormSrgb);

        /// <summary>
        /// Codec for <see cref="ImageFormat.BC2Typeless"/>.
        /// </summary>
        public static readonly IPixelCodec Bc2Typeless = new BC2Codec(ImageFormat.BC2Typeless);

        /// <summary>
        /// Codec for <see cref="ImageFormat.BC2Unorm"/>.
        /// </summary>
        public static readonly IPixelCodec Bc2Unorm = new BC2Codec(ImageFormat.BC2Unorm);

        /// <summary>
        /// Codec for <see cref="ImageFormat.BC2UnormSrgb"/>.
        /// </summary>
        public static readonly IPixelCodec Bc2UnormSrgb = new BC2Codec(ImageFormat.BC2UnormSrgb);

        /// <summary>
        /// Codec for <see cref="ImageFormat.BC3Typeless"/>.
        /// </summary>
        public static readonly IPixelCodec Bc3Typeless = new BC3Codec(ImageFormat.BC3Typeless);

        /// <summary>
        /// Codec for <see cref="ImageFormat.BC3Unorm"/>.
        /// </summary>
        public static readonly IPixelCodec Bc3Unorm = new BC3Codec(ImageFormat.BC3Unorm);

        /// <summary>
        /// Codec for <see cref="ImageFormat.BC3UnormSrgb"/>.
        /// </summary>
        public static readonly IPixelCodec Bc3UnormSrgb = new BC3Codec(ImageFormat.BC3UnormSrgb);

        /// <summary>
        /// Codec for <see cref="ImageFormat.BC4Typeless"/>.
        /// </summary>
        public static readonly IPixelCodec Bc4Typeless = new BC4Codec(ImageFormat.BC4Typeless);

        /// <summary>
        /// Codec for <see cref="ImageFormat.BC4Unorm"/>.
        /// </summary>
        public static readonly IPixelCodec Bc4Unorm = new BC4Codec(ImageFormat.BC4Unorm);

        /// <summary>
        /// Codec for <see cref="ImageFormat.BC4Snorm"/>.
        /// </summary>
        public static readonly IPixelCodec Bc4Snorm = new BC4Codec(ImageFormat.BC4Snorm);

        /// <summary>
        /// Codec for <see cref="ImageFormat.BC5Typeless"/>.
        /// </summary>
        public static readonly IPixelCodec Bc5Typeless = new BC5Codec(ImageFormat.BC5Typeless);

        /// <summary>
        /// Codec for <see cref="ImageFormat.BC5Unorm"/>.
        /// </summary>
        public static readonly IPixelCodec Bc5Unorm = new BC5Codec(ImageFormat.BC5Unorm);

        /// <summary>
        /// Codec for <see cref="ImageFormat.BC5Snorm"/>.
        /// </summary>
        public static readonly IPixelCodec Bc5Snorm = new BC5Codec(ImageFormat.BC5Snorm);

        /// <summary>
        /// Codec for <see cref="ImageFormat.BC6HTypeless"/>.
        /// </summary>
        public static readonly IPixelCodec Bc6HTypeless = new BC6HCodec(ImageFormat.BC6HTypeless);

        /// <summary>
        /// Codec for <see cref="ImageFormat.BC6HUF16"/>.
        /// </summary>
        public static readonly IPixelCodec Bc6HUf16 = new BC6HCodec(ImageFormat.BC6HUF16);

        /// <summary>
        /// Codec for <see cref="ImageFormat.BC6HSF16"/>.
        /// </summary>
        public static readonly IPixelCodec Bc6HSf16 = new BC6HCodec(ImageFormat.BC6HSF16);

        /// <summary>
        /// Codec for <see cref="ImageFormat.BC7Typeless"/>.
        /// </summary>
        public static readonly IPixelCodec Bc7Typeless = new BC7Codec(ImageFormat.BC7Typeless);

        /// <summary>
        /// Codec for <see cref="ImageFormat.BC7Unorm"/>.
        /// </summary>
        public static readonly IPixelCodec Bc7Unorm = new BC7Codec(ImageFormat.BC7Unorm);

        /// <summary>
        /// Codec for <see cref="ImageFormat.BC7UnormSrgb"/>.
        /// </summary>
        public static readonly IPixelCodec Bc7UnormSrgb = new BC7Codec(ImageFormat.BC7UnormSrgb);

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
                // 8-bit RGBA formats
                ImageFormat.R8G8B8A8Unorm => R8G8B8A8Unorm,
                ImageFormat.R8G8B8A8UnormSrgb => R8G8B8A8UnormSrgb,
                ImageFormat.B8G8R8A8Unorm => B8G8R8A8Unorm,
                ImageFormat.B8G8R8A8UnormSrgb => B8G8R8A8UnormSrgb,

                // 32-bit float RGBA formats
                ImageFormat.R32G32B32A32Typeless => R32G32B32A32Typeless,
                ImageFormat.R32G32B32A32Float => R32G32B32A32Float,
                ImageFormat.R32G32B32A32Uint => R32G32B32A32Uint,
                ImageFormat.R32G32B32A32Sint => R32G32B32A32Sint,

                // 32-bit float RGB formats
                ImageFormat.R32G32B32Typeless => R32G32B32Typeless,
                ImageFormat.R32G32B32Float => R32G32B32Float,
                ImageFormat.R32G32B32Uint => R32G32B32Uint,
                ImageFormat.R32G32B32Sint => R32G32B32Sint,

                // 16-bit RGBA formats
                ImageFormat.R16G16B16A16Typeless => R16G16B16A16Typeless,
                ImageFormat.R16G16B16A16Float => R16G16B16A16Float,
                ImageFormat.R16G16B16A16Unorm => R16G16B16A16Unorm,
                ImageFormat.R16G16B16A16Uint => R16G16B16A16Uint,
                ImageFormat.R16G16B16A16Snorm => R16G16B16A16Snorm,
                ImageFormat.R16G16B16A16Sint => R16G16B16A16Sint,

                // 32-bit RG formats
                ImageFormat.R32G32Typeless => R32G32Typeless,
                ImageFormat.R32G32Float => R32G32Float,
                ImageFormat.R32G32Uint => R32G32Uint,
                ImageFormat.R32G32Sint => R32G32Sint,

                // 16-bit RG formats
                ImageFormat.R16G16Typeless => R16G16Typeless,
                ImageFormat.R16G16Float => R16G16Float,
                ImageFormat.R16G16Unorm => R16G16Unorm,
                ImageFormat.R16G16Uint => R16G16Uint,
                ImageFormat.R16G16Snorm => R16G16Snorm,
                ImageFormat.R16G16Sint => R16G16Sint,

                // 32-bit R formats
                ImageFormat.R32Typeless => R32Typeless,
                ImageFormat.D32Float => D32Float,
                ImageFormat.R32Float => R32Float,
                ImageFormat.R32Uint => R32Uint,
                ImageFormat.R32Sint => R32Sint,

                // 16-bit R formats
                ImageFormat.R16Typeless => R16Typeless,
                ImageFormat.R16Float => R16Float,
                ImageFormat.D16Unorm => D16Unorm,
                ImageFormat.R16Unorm => R16Unorm,
                ImageFormat.R16Uint => R16Uint,
                ImageFormat.R16Snorm => R16Snorm,
                ImageFormat.R16Sint => R16Sint,

                // 16-bit RG formats (8-bit per channel)
                ImageFormat.R8G8Typeless => R8G8Typeless,
                ImageFormat.R8G8Unorm => R8G8Unorm,
                ImageFormat.R8G8Uint => R8G8Uint,
                ImageFormat.R8G8Snorm => R8G8Snorm,
                ImageFormat.R8G8Sint => R8G8Sint,

                // 8-bit R formats
                ImageFormat.R8Typeless => R8Typeless,
                ImageFormat.R8Unorm => R8Unorm,
                ImageFormat.R8Uint => R8Uint,
                ImageFormat.R8Snorm => R8Snorm,
                ImageFormat.R8Sint => R8Sint,

                // Alpha-only format
                ImageFormat.A8Unorm => A8Unorm,

                // 10/11-bit packed formats
                ImageFormat.R10G10B10A2Typeless => R10G10B10A2Typeless,
                ImageFormat.R10G10B10A2Unorm => R10G10B10A2Unorm,
                ImageFormat.R10G10B10A2Uint => R10G10B10A2Uint,
                ImageFormat.R10G10B10XrBiasA2Unorm => R10G10B10XrBiasA2Unorm,
                ImageFormat.R11G11B10Float => R11G11B10Float,

                // 16-bit packed BGR formats
                ImageFormat.B5G6R5Unorm => B5G6R5Unorm,
                ImageFormat.B5G5R5A1Unorm => B5G5R5A1Unorm,
                ImageFormat.B4G4R4A4Unorm => B4G4R4A4Unorm,

                // BC1-BC7 block-compressed formats
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
