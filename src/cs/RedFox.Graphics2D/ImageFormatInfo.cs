namespace RedFox.Graphics2D
{
    /// <summary>
    /// Provides utility methods for querying properties of <see cref="ImageFormat"/> values,
    /// including block compression detection, bits-per-pixel, and pitch calculations.
    /// </summary>
    public static class ImageFormatInfo
    {
        /// <summary>
        /// Returns whether the given format is block compressed (BC1–BC7).
        /// </summary>
        public static bool IsBlockCompressed(ImageFormat format) => format switch
        {
            >= ImageFormat.BC1Typeless and <= ImageFormat.BC5Snorm => true,
            >= ImageFormat.BC6HTypeless and <= ImageFormat.BC7UnormSrgb => true,
            _ => false,
        };

        /// <summary>
        /// Returns whether the format is explicitly sRGB.
        /// </summary>
        public static bool IsSrgb(ImageFormat format) => format switch
        {
            ImageFormat.R8G8B8A8UnormSrgb or
            ImageFormat.B8G8R8A8UnormSrgb or
            ImageFormat.B8G8R8X8UnormSrgb or
            ImageFormat.BC1UnormSrgb or
            ImageFormat.BC2UnormSrgb or
            ImageFormat.BC3UnormSrgb or
            ImageFormat.BC7UnormSrgb => true,
            _ => false,
        };

        /// <summary>
        /// Gets the block size in bytes for a block-compressed format.
        /// </summary>
        public static int GetBlockSize(ImageFormat format) => format switch
        {
            ImageFormat.BC1Typeless or
            ImageFormat.BC1Unorm or
            ImageFormat.BC1UnormSrgb => 8,

            ImageFormat.BC4Typeless or
            ImageFormat.BC4Unorm or
            ImageFormat.BC4Snorm => 8,

            ImageFormat.BC2Typeless or
            ImageFormat.BC2Unorm or
            ImageFormat.BC2UnormSrgb => 16,

            ImageFormat.BC3Typeless or
            ImageFormat.BC3Unorm or
            ImageFormat.BC3UnormSrgb => 16,

            ImageFormat.BC5Typeless or
            ImageFormat.BC5Unorm or
            ImageFormat.BC5Snorm => 16,

            ImageFormat.BC6HTypeless or
            ImageFormat.BC6HUF16 or
            ImageFormat.BC6HSF16 => 16,

            ImageFormat.BC7Typeless or
            ImageFormat.BC7Unorm or
            ImageFormat.BC7UnormSrgb => 16,

            _ => throw new NotSupportedException($"Format {format} is not block-compressed."),
        };

        /// <summary>
        /// Gets the number of bits per pixel for an uncompressed format.
        /// </summary>
        public static int GetBitsPerPixel(ImageFormat format) => format switch
        {
            ImageFormat.R32G32B32A32Typeless or
            ImageFormat.R32G32B32A32Float or
            ImageFormat.R32G32B32A32Uint or
            ImageFormat.R32G32B32A32Sint => 128,

            ImageFormat.R32G32B32Typeless or
            ImageFormat.R32G32B32Float or
            ImageFormat.R32G32B32Uint or
            ImageFormat.R32G32B32Sint => 96,

            ImageFormat.R16G16B16A16Typeless or
            ImageFormat.R16G16B16A16Float or
            ImageFormat.R16G16B16A16Unorm or
            ImageFormat.R16G16B16A16Uint or
            ImageFormat.R16G16B16A16Snorm or
            ImageFormat.R16G16B16A16Sint or
            ImageFormat.R32G32Typeless or
            ImageFormat.R32G32Float or
            ImageFormat.R32G32Uint or
            ImageFormat.R32G32Sint => 64,

            ImageFormat.R10G10B10A2Typeless or
            ImageFormat.R10G10B10A2Unorm or
            ImageFormat.R10G10B10A2Uint or
            ImageFormat.R11G11B10Float or
            ImageFormat.R8G8B8A8Typeless or
            ImageFormat.R8G8B8A8Unorm or
            ImageFormat.R8G8B8A8UnormSrgb or
            ImageFormat.R8G8B8A8Uint or
            ImageFormat.R8G8B8A8Snorm or
            ImageFormat.R8G8B8A8Sint or
            ImageFormat.R16G16Typeless or
            ImageFormat.R16G16Float or
            ImageFormat.R16G16Unorm or
            ImageFormat.R16G16Uint or
            ImageFormat.R16G16Snorm or
            ImageFormat.R16G16Sint or
            ImageFormat.R32Typeless or
            ImageFormat.D32Float or
            ImageFormat.R32Float or
            ImageFormat.R32Uint or
            ImageFormat.R32Sint or
            ImageFormat.B8G8R8A8Unorm or
            ImageFormat.B8G8R8X8Unorm or
            ImageFormat.B8G8R8A8Typeless or
            ImageFormat.B8G8R8A8UnormSrgb or
            ImageFormat.B8G8R8X8Typeless or
            ImageFormat.B8G8R8X8UnormSrgb or
            ImageFormat.R9G9B9E5SharedExp => 32,

            ImageFormat.R8G8Typeless or
            ImageFormat.R8G8Unorm or
            ImageFormat.R8G8Uint or
            ImageFormat.R8G8Snorm or
            ImageFormat.R8G8Sint or
            ImageFormat.R16Typeless or
            ImageFormat.R16Float or
            ImageFormat.D16Unorm or
            ImageFormat.R16Unorm or
            ImageFormat.R16Uint or
            ImageFormat.R16Snorm or
            ImageFormat.R16Sint or
            ImageFormat.B5G6R5Unorm or
            ImageFormat.B5G5R5A1Unorm or
            ImageFormat.B4G4R4A4Unorm => 16,

            ImageFormat.R8Typeless or
            ImageFormat.R8Unorm or
            ImageFormat.R8Uint or
            ImageFormat.R8Snorm or
            ImageFormat.R8Sint or
            ImageFormat.A8Unorm => 8,

            ImageFormat.R1Unorm => 1,

            _ => throw new NotSupportedException($"Bits per pixel not defined for format {format}."),
        };

        /// <summary>
        /// Calculates the row pitch and slice pitch for the given format and dimensions.
        /// </summary>
        /// <returns>A tuple of (rowPitch, slicePitch) in bytes.</returns>
        public static (int RowPitch, int SlicePitch) CalculatePitch(ImageFormat format, int width, int height)
        {
            if (IsBlockCompressed(format))
            {
                int blockSize = GetBlockSize(format);
                int blockCountW = Math.Max(1, (width + 3) / 4);
                int blockCountH = Math.Max(1, (height + 3) / 4);

                int rowPitch = blockCountW * blockSize;
                int slicePitch = rowPitch * blockCountH;

                return (rowPitch, slicePitch);
            }
            else
            {
                int bpp = GetBitsPerPixel(format);
                int rowPitch = (width * bpp + 7) / 8;
                int slicePitch = rowPitch * height;

                return (rowPitch, slicePitch);
            }
        }
    }
}
