namespace RedFox.Graphics2D.IO
{
    /// <summary>
    /// Maps legacy DDS pixel format descriptors to modern <see cref="ImageFormat"/> values.
    /// </summary>
    public static class DdsLegacyFormatMapper
    {
        // D3DFMT numeric FourCC codes used in legacy DDS files.
        private const uint D3dFmtA16B16G16R16 = 36;
        private const uint D3dFmtQ16W16V16U16 = 110;
        private const uint D3dFmtR16F = 111;
        private const uint D3dFmtG16R16F = 112;
        private const uint D3dFmtA16B16G16R16F = 113;
        private const uint D3dFmtR32F = 114;
        private const uint D3dFmtG32R32F = 115;
        private const uint D3dFmtA32B32G32R32F = 116;

        /// <summary>
        /// Determines whether the pixel format indicates a DX10 extended header is present.
        /// </summary>
        /// <param name="pixelFormat">The pixel format descriptor from the DDS header.</param>
        /// <returns><see langword="true"/> if the FourCC is <c>DX10</c>; otherwise <see langword="false"/>.</returns>
        public static bool IsDx10(in DdsPixelFormat pixelFormat)
        {
            return (pixelFormat.Flags & DdsPixelFormatFlags.FourCc) != 0
                && pixelFormat.FourCC == DdsFourCc.Dx10;
        }

        /// <summary>
        /// Converts a legacy DDS pixel format descriptor to a modern <see cref="ImageFormat"/> value.
        /// </summary>
        /// <param name="pixelFormat">The pixel format descriptor from the DDS header.</param>
        /// <returns>The corresponding <see cref="ImageFormat"/>.</returns>
        /// <exception cref="NotSupportedException">Thrown when the pixel format is not recognized.</exception>
        public static ImageFormat FromLegacyPixelFormat(in DdsPixelFormat pixelFormat)
        {
            if ((pixelFormat.Flags & DdsPixelFormatFlags.FourCc) != 0)
            {
                return FromFourCC(pixelFormat);
            }

            if ((pixelFormat.Flags & DdsPixelFormatFlags.Rgb) != 0)
            {
                return FromRgb(pixelFormat);
            }

            if ((pixelFormat.Flags & DdsPixelFormatFlags.Luminance) != 0)
            {
                return FromLuminance(pixelFormat);
            }

            if ((pixelFormat.Flags & DdsPixelFormatFlags.Alpha) != 0 && pixelFormat.RgbBitCount == 8)
            {
                return ImageFormat.A8Unorm;
            }

            throw new NotSupportedException("Unsupported DDS pixel format.");
        }

        private static ImageFormat FromFourCC(in DdsPixelFormat pixelFormat)
        {
            if (pixelFormat.FourCC == DdsFourCc.Dxt1) { return ImageFormat.BC1Unorm; }
            if (pixelFormat.FourCC == DdsFourCc.Dxt2) { return ImageFormat.BC2Unorm; }
            if (pixelFormat.FourCC == DdsFourCc.Dxt3) { return ImageFormat.BC2Unorm; }
            if (pixelFormat.FourCC == DdsFourCc.Dxt4) { return ImageFormat.BC3Unorm; }
            if (pixelFormat.FourCC == DdsFourCc.Dxt5) { return ImageFormat.BC3Unorm; }
            if (pixelFormat.FourCC == DdsFourCc.Ati1 || pixelFormat.FourCC == DdsFourCc.Bc4U) { return ImageFormat.BC4Unorm; }
            if (pixelFormat.FourCC == DdsFourCc.Bc4S) { return ImageFormat.BC4Snorm; }
            if (pixelFormat.FourCC == DdsFourCc.Ati2 || pixelFormat.FourCC == DdsFourCc.Bc5U) { return ImageFormat.BC5Unorm; }
            if (pixelFormat.FourCC == DdsFourCc.Bc5S) { return ImageFormat.BC5Snorm; }

            return pixelFormat.FourCC switch
            {
                D3dFmtA16B16G16R16 => ImageFormat.R16G16B16A16Unorm,
                D3dFmtQ16W16V16U16 => ImageFormat.R16G16B16A16Snorm,
                D3dFmtR16F => ImageFormat.R16Float,
                D3dFmtG16R16F => ImageFormat.R16G16Float,
                D3dFmtA16B16G16R16F => ImageFormat.R16G16B16A16Float,
                D3dFmtR32F => ImageFormat.R32Float,
                D3dFmtG32R32F => ImageFormat.R32G32Float,
                D3dFmtA32B32G32R32F => ImageFormat.R32G32B32A32Float,
                _ => throw new NotSupportedException($"Unsupported DDS FourCC: 0x{pixelFormat.FourCC:X8}."),
            };
        }

        private static ImageFormat FromRgb(in DdsPixelFormat pf)
        {
            return pf.RgbBitCount switch
            {
                32 when pf.RBitMask == 0x000000FF && pf.GBitMask == 0x0000FF00
                     && pf.BBitMask == 0x00FF0000 && pf.ABitMask == 0xFF000000
                    => ImageFormat.R8G8B8A8Unorm,

                32 when pf.RBitMask == 0x00FF0000 && pf.GBitMask == 0x0000FF00
                     && pf.BBitMask == 0x000000FF && pf.ABitMask == 0xFF000000
                    => ImageFormat.B8G8R8A8Unorm,

                32 when pf.RBitMask == 0x00FF0000 && pf.GBitMask == 0x0000FF00
                     && pf.BBitMask == 0x000000FF && pf.ABitMask == 0x00000000
                    => ImageFormat.B8G8R8X8Unorm,

                24 when pf.RBitMask == 0xFF0000 && pf.GBitMask == 0x00FF00
                     && pf.BBitMask == 0x0000FF
                    => ImageFormat.B8G8R8X8Unorm,

                16 when pf.RBitMask == 0xF800 && pf.GBitMask == 0x07E0
                     && pf.BBitMask == 0x001F
                    => ImageFormat.B5G6R5Unorm,

                16 when pf.RBitMask == 0x7C00 && pf.GBitMask == 0x03E0
                     && pf.BBitMask == 0x001F && pf.ABitMask == 0x8000
                    => ImageFormat.B5G5R5A1Unorm,

                16 when pf.RBitMask == 0x0F00 && pf.GBitMask == 0x00F0
                     && pf.BBitMask == 0x000F && pf.ABitMask == 0xF000
                    => ImageFormat.B4G4R4A4Unorm,

                _ => throw new NotSupportedException(
                    $"Unsupported DDS RGB format: {pf.RgbBitCount}bpp " +
                    $"R=0x{pf.RBitMask:X} G=0x{pf.GBitMask:X} " +
                    $"B=0x{pf.BBitMask:X} A=0x{pf.ABitMask:X}."),
            };
        }

        private static ImageFormat FromLuminance(in DdsPixelFormat pf)
        {
            return pf.RgbBitCount switch
            {
                8 => ImageFormat.R8Unorm,
                16 when pf.ABitMask == 0xFF00 => ImageFormat.R8G8Unorm,
                16 => ImageFormat.R16Unorm,
                _ => throw new NotSupportedException(
                    $"Unsupported DDS luminance format: {pf.RgbBitCount}bpp."),
            };
        }
    }
}
