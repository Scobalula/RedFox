namespace RedFox.Graphics2D.IO
{
    internal static class DdsLegacyFormatMapper
    {
        internal static bool IsDx10(in DdsPixelFormat pixelFormat)
        {
            return (pixelFormat.Flags & DdsPixelFormatFlags.FourCc) != 0 && pixelFormat.FourCC == DdsFourCc.Dx10;
        }

        internal static ImageFormat FromLegacyPixelFormat(in DdsPixelFormat pixelFormat)
        {
            if ((pixelFormat.Flags & DdsPixelFormatFlags.FourCc) != 0)
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
                    36 => ImageFormat.R16G16B16A16Unorm,
                    110 => ImageFormat.R16G16B16A16Snorm,
                    111 => ImageFormat.R16Float,
                    112 => ImageFormat.R16G16Float,
                    113 => ImageFormat.R16G16B16A16Float,
                    114 => ImageFormat.R32Float,
                    115 => ImageFormat.R32G32Float,
                    116 => ImageFormat.R32G32B32A32Float,
                    _ => throw new NotSupportedException($"Unsupported DDS FourCC: 0x{pixelFormat.FourCC:X8}."),
                };
            }

            if ((pixelFormat.Flags & DdsPixelFormatFlags.Rgb) != 0)
            {
                return pixelFormat.RgbBitCount switch
                {
                    32 when pixelFormat.RBitMask == 0x000000FF && pixelFormat.GBitMask == 0x0000FF00 && pixelFormat.BBitMask == 0x00FF0000 && pixelFormat.ABitMask == 0xFF000000 => ImageFormat.R8G8B8A8Unorm,
                    32 when pixelFormat.RBitMask == 0x00FF0000 && pixelFormat.GBitMask == 0x0000FF00 && pixelFormat.BBitMask == 0x000000FF && pixelFormat.ABitMask == 0xFF000000 => ImageFormat.B8G8R8A8Unorm,
                    32 when pixelFormat.RBitMask == 0x00FF0000 && pixelFormat.GBitMask == 0x0000FF00 && pixelFormat.BBitMask == 0x000000FF && pixelFormat.ABitMask == 0x00000000 => ImageFormat.B8G8R8X8Unorm,
                    24 when pixelFormat.RBitMask == 0xFF0000 && pixelFormat.GBitMask == 0x00FF00 && pixelFormat.BBitMask == 0x0000FF => ImageFormat.B8G8R8X8Unorm,
                    16 when pixelFormat.RBitMask == 0xF800 && pixelFormat.GBitMask == 0x07E0 && pixelFormat.BBitMask == 0x001F => ImageFormat.B5G6R5Unorm,
                    16 when pixelFormat.RBitMask == 0x7C00 && pixelFormat.GBitMask == 0x03E0 && pixelFormat.BBitMask == 0x001F && pixelFormat.ABitMask == 0x8000 => ImageFormat.B5G5R5A1Unorm,
                    16 when pixelFormat.RBitMask == 0x0F00 && pixelFormat.GBitMask == 0x00F0 && pixelFormat.BBitMask == 0x000F && pixelFormat.ABitMask == 0xF000 => ImageFormat.B4G4R4A4Unorm,
                    _ => throw new NotSupportedException($"Unsupported DDS RGB format: {pixelFormat.RgbBitCount}bpp R=0x{pixelFormat.RBitMask:X} G=0x{pixelFormat.GBitMask:X} B=0x{pixelFormat.BBitMask:X} A=0x{pixelFormat.ABitMask:X}."),
                };
            }

            if ((pixelFormat.Flags & DdsPixelFormatFlags.Luminance) != 0)
            {
                return pixelFormat.RgbBitCount switch
                {
                    8 => ImageFormat.R8Unorm,
                    16 when pixelFormat.ABitMask == 0xFF00 => ImageFormat.R8G8Unorm,
                    16 => ImageFormat.R16Unorm,
                    _ => throw new NotSupportedException($"Unsupported DDS luminance format: {pixelFormat.RgbBitCount}bpp."),
                };
            }

            if ((pixelFormat.Flags & DdsPixelFormatFlags.Alpha) != 0 && pixelFormat.RgbBitCount == 8)
            {
                return ImageFormat.A8Unorm;
            }

            throw new NotSupportedException("Unsupported DDS pixel format.");
        }
    }
}
