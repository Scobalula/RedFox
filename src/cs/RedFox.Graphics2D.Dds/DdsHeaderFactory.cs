namespace RedFox.Graphics2D.IO
{
    /// <summary>
    /// Creates DDS file headers from <see cref="Image"/> instances for writing.
    /// </summary>
    public static class DdsHeaderFactory
    {
        /// <summary>
        /// Validates that the specified image meets DDS layout constraints.
        /// </summary>
        /// <param name="image">The image to validate.</param>
        /// <exception cref="InvalidDataException">Thrown when the image violates DDS layout rules.</exception>
        public static void ValidateImage(Image image)
        {
            ArgumentNullException.ThrowIfNull(image);

            if (image.IsCubemap && image.ArraySize % 6 != 0)
            {
                throw new InvalidDataException("Cubemap DDS images must have an array size that is a multiple of 6 (one entry per face).");
            }

            if (image.IsCubemap && image.Depth != 1)
            {
                throw new InvalidDataException("Cubemap DDS images must use a depth of 1.");
            }

            if (image.Depth > 1 && image.ArraySize > 1)
            {
                throw new InvalidDataException("Volume DDS images cannot also be texture arrays.");
            }
        }

        /// <summary>
        /// Creates a standard DDS header for the given image.
        /// </summary>
        /// <param name="image">The source image.</param>
        /// <returns>A populated <see cref="DdsHeader"/> ready for serialization.</returns>
        public static DdsHeader CreateHeader(Image image)
        {
            bool isBlockCompressed = ImageFormatInfo.IsBlockCompressed(image.Format);
            bool hasMipmaps = image.MipLevels > 1;
            bool isVolume = image.Depth > 1;
            bool isCubemap = image.IsCubemap;

            return new DdsHeader
            {
                Size = DdsConstants.HeaderSize,
                Flags = BuildHeaderFlags(hasMipmaps, isVolume, isBlockCompressed),
                Height = (uint)image.Height,
                Width = (uint)image.Width,
                PitchOrLinearSize = DdsPitchCalculator.GetTopLevelPitchOrLinearSize(
                    image.Width, image.Format, isBlockCompressed),
                Depth = (uint)image.Depth,
                MipMapCount = (uint)image.MipLevels,
                PixelFormat = new DdsPixelFormat
                {
                    Size = DdsConstants.PixelFormatSize,
                    Flags = DdsPixelFormatFlags.FourCc,
                    FourCC = DdsFourCc.Dx10,
                },
                Caps = BuildCaps(hasMipmaps, isVolume, isCubemap, image.ArraySize),
                Caps2 = BuildCaps2(isVolume, isCubemap),
            };
        }

        private static DdsHeaderFlags BuildHeaderFlags(bool hasMipmaps, bool isVolume, bool isBlockCompressed)
        {
            DdsHeaderFlags flags = DdsHeaderFlags.Caps
                | DdsHeaderFlags.Height
                | DdsHeaderFlags.Width
                | DdsHeaderFlags.PixelFormat;

            if (hasMipmaps) { flags |= DdsHeaderFlags.MipMapCount; }
            if (isVolume) { flags |= DdsHeaderFlags.Depth; }
            flags |= isBlockCompressed ? DdsHeaderFlags.LinearSize : DdsHeaderFlags.Pitch;

            return flags;
        }

        private static DdsCaps BuildCaps(bool hasMipmaps, bool isVolume, bool isCubemap, int arraySize)
        {
            DdsCaps caps = DdsCaps.Texture;
            bool isComplex = hasMipmaps || isVolume || arraySize > 1 || isCubemap;

            if (isComplex) { caps |= DdsCaps.Complex; }
            if (hasMipmaps) { caps |= DdsCaps.MipMap; }

            return caps;
        }

        private static DdsCaps2 BuildCaps2(bool isVolume, bool isCubemap)
        {
            DdsCaps2 caps2 = DdsCaps2.None;

            if (isVolume) { caps2 |= DdsCaps2.Volume; }
            if (isCubemap) { caps2 |= DdsCaps2.Cubemap | DdsCaps2.CubemapAllFaces; }

            return caps2;
        }

        /// <summary>
        /// Creates a DX10 extended header for the given image.
        /// </summary>
        /// <param name="image">The source image.</param>
        /// <returns>A populated <see cref="DdsHeaderDx10"/> ready for serialization.</returns>
        public static DdsHeaderDx10 CreateDx10Header(Image image)
        {
            bool isVolume = image.Depth > 1;
            bool isCubemap = image.IsCubemap;
            uint arraySize = isCubemap ? checked((uint)(image.ArraySize / 6)) : (uint)image.ArraySize;

            return new DdsHeaderDx10
            {
                DxgiFormat = (uint)image.Format,
                ResourceDimension = isVolume ? DdsResourceDimension.Texture3D : DdsResourceDimension.Texture2D,
                MiscFlag = isCubemap ? DdsConstants.Dxt10CubemapFlag : 0u,
                ArraySize = arraySize,
                MiscFlags2 = 0,
            };
        }
    }
}
