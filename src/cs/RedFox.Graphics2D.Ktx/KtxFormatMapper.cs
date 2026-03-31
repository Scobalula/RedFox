namespace RedFox.Graphics2D.Ktx
{
    internal static class KtxFormatMapper
    {
        public static KtxFormatDescriptor GetDescriptor(ImageFormat format)
        {
            return format switch
            {
                ImageFormat.R8G8B8A8Unorm => new(format, KtxConstants.GlUnsignedByte, 1, KtxConstants.GlRgba, KtxConstants.GlRgba8, KtxConstants.GlRgba, false),
                ImageFormat.R8G8B8A8UnormSrgb => new(format, KtxConstants.GlUnsignedByte, 1, KtxConstants.GlRgba, KtxConstants.GlSrgb8Alpha8, KtxConstants.GlRgba, false),
                ImageFormat.B8G8R8A8Unorm => new(format, KtxConstants.GlUnsignedByte, 1, KtxConstants.GlBgra, KtxConstants.GlRgba8, KtxConstants.GlRgba, false),
                ImageFormat.B8G8R8A8UnormSrgb => new(format, KtxConstants.GlUnsignedByte, 1, KtxConstants.GlBgra, KtxConstants.GlSrgb8Alpha8, KtxConstants.GlRgba, false),
                ImageFormat.R16G16B16A16Float => new(format, KtxConstants.GlHalfFloat, 2, KtxConstants.GlRgba, KtxConstants.GlRgba16F, KtxConstants.GlRgba, false),
                ImageFormat.R32G32B32A32Float => new(format, KtxConstants.GlFloat, 4, KtxConstants.GlRgba, KtxConstants.GlRgba32F, KtxConstants.GlRgba, false),
                ImageFormat.BC1Unorm => new(format, 0, 1, 0, KtxConstants.GlCompressedRgbaS3TcDxt1Ext, KtxConstants.GlRgba, true),
                ImageFormat.BC1UnormSrgb => new(format, 0, 1, 0, KtxConstants.GlCompressedSrgbAlphaS3TcDxt1Ext, KtxConstants.GlRgba, true),
                ImageFormat.BC2Unorm => new(format, 0, 1, 0, KtxConstants.GlCompressedRgbaS3TcDxt3Ext, KtxConstants.GlRgba, true),
                ImageFormat.BC2UnormSrgb => new(format, 0, 1, 0, KtxConstants.GlCompressedSrgbAlphaS3TcDxt3Ext, KtxConstants.GlRgba, true),
                ImageFormat.BC3Unorm => new(format, 0, 1, 0, KtxConstants.GlCompressedRgbaS3TcDxt5Ext, KtxConstants.GlRgba, true),
                ImageFormat.BC3UnormSrgb => new(format, 0, 1, 0, KtxConstants.GlCompressedSrgbAlphaS3TcDxt5Ext, KtxConstants.GlRgba, true),
                ImageFormat.BC4Unorm => new(format, 0, 1, 0, KtxConstants.GlCompressedRedRgtc1, KtxConstants.GlRed, true),
                ImageFormat.BC4Snorm => new(format, 0, 1, 0, KtxConstants.GlCompressedSignedRedRgtc1, KtxConstants.GlRed, true),
                ImageFormat.BC5Unorm => new(format, 0, 1, 0, KtxConstants.GlCompressedRgRgtc2, KtxConstants.GlRg, true),
                ImageFormat.BC5Snorm => new(format, 0, 1, 0, KtxConstants.GlCompressedSignedRgRgtc2, KtxConstants.GlRg, true),
                ImageFormat.BC6HUF16 => new(format, 0, 1, 0, KtxConstants.GlCompressedRgbBptcUnsignedFloat, KtxConstants.GlRgb, true),
                ImageFormat.BC6HSF16 => new(format, 0, 1, 0, KtxConstants.GlCompressedRgbBptcSignedFloat, KtxConstants.GlRgb, true),
                ImageFormat.BC7Unorm => new(format, 0, 1, 0, KtxConstants.GlCompressedRgbaBptcUnorm, KtxConstants.GlRgba, true),
                ImageFormat.BC7UnormSrgb => new(format, 0, 1, 0, KtxConstants.GlCompressedSrgbAlphaBptcUnorm, KtxConstants.GlRgba, true),
                _ => throw new NotSupportedException($"KTX writing does not support the {format} image format."),
            };
        }

        public static ImageFormat GetImageFormat(KtxHeader header)
        {
            if (header.GlType == KtxConstants.GlUnsignedByte && header.GlFormat == KtxConstants.GlRgba && header.GlInternalFormat == KtxConstants.GlRgba8)
                return ImageFormat.R8G8B8A8Unorm;

            if (header.GlType == KtxConstants.GlUnsignedByte && header.GlFormat == KtxConstants.GlBgra && header.GlInternalFormat == KtxConstants.GlRgba8)
                return ImageFormat.B8G8R8A8Unorm;

            if (header.GlType == KtxConstants.GlUnsignedByte && header.GlFormat == KtxConstants.GlRgba && header.GlInternalFormat == KtxConstants.GlSrgb8Alpha8)
                return ImageFormat.R8G8B8A8UnormSrgb;

            if (header.GlType == KtxConstants.GlUnsignedByte && header.GlFormat == KtxConstants.GlBgra && header.GlInternalFormat == KtxConstants.GlSrgb8Alpha8)
                return ImageFormat.B8G8R8A8UnormSrgb;

            if (header.GlType == KtxConstants.GlHalfFloat && header.GlFormat == KtxConstants.GlRgba && header.GlInternalFormat == KtxConstants.GlRgba16F)
                return ImageFormat.R16G16B16A16Float;

            if (header.GlType == KtxConstants.GlFloat && header.GlFormat == KtxConstants.GlRgba && header.GlInternalFormat == KtxConstants.GlRgba32F)
                return ImageFormat.R32G32B32A32Float;

            return header.GlInternalFormat switch
            {
                KtxConstants.GlCompressedRgbS3TcDxt1Ext or KtxConstants.GlCompressedRgbaS3TcDxt1Ext => ImageFormat.BC1Unorm,
                KtxConstants.GlCompressedSrgbS3TcDxt1Ext or KtxConstants.GlCompressedSrgbAlphaS3TcDxt1Ext => ImageFormat.BC1UnormSrgb,
                KtxConstants.GlCompressedRgbaS3TcDxt3Ext => ImageFormat.BC2Unorm,
                KtxConstants.GlCompressedSrgbAlphaS3TcDxt3Ext => ImageFormat.BC2UnormSrgb,
                KtxConstants.GlCompressedRgbaS3TcDxt5Ext => ImageFormat.BC3Unorm,
                KtxConstants.GlCompressedSrgbAlphaS3TcDxt5Ext => ImageFormat.BC3UnormSrgb,
                KtxConstants.GlCompressedRedRgtc1 => ImageFormat.BC4Unorm,
                KtxConstants.GlCompressedSignedRedRgtc1 => ImageFormat.BC4Snorm,
                KtxConstants.GlCompressedRgRgtc2 => ImageFormat.BC5Unorm,
                KtxConstants.GlCompressedSignedRgRgtc2 => ImageFormat.BC5Snorm,
                KtxConstants.GlCompressedRgbBptcUnsignedFloat => ImageFormat.BC6HUF16,
                KtxConstants.GlCompressedRgbBptcSignedFloat => ImageFormat.BC6HSF16,
                KtxConstants.GlCompressedRgbaBptcUnorm => ImageFormat.BC7Unorm,
                KtxConstants.GlCompressedSrgbAlphaBptcUnorm => ImageFormat.BC7UnormSrgb,
                _ => throw new NotSupportedException(
                    $"KTX does not support the OpenGL format combination glType=0x{header.GlType:X8}, glFormat=0x{header.GlFormat:X8}, glInternalFormat=0x{header.GlInternalFormat:X8}."),
            };
        }
    }
}
