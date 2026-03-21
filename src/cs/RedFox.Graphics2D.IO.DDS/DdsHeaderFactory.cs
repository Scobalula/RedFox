namespace RedFox.Graphics2D.IO
{
    internal static class DdsHeaderFactory
    {
        internal static void ValidateImage(Image image)
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

        internal static DdsHeader CreateHeader(Image image)
        {
            bool isBlockCompressed = ImageFormatInfo.IsBlockCompressed(image.Format);
            bool hasMipmaps = image.MipLevels > 1;
            bool isVolume = image.Depth > 1;
            bool isCubemap = image.IsCubemap;
            bool isComplex = hasMipmaps || isVolume || image.ArraySize > 1 || isCubemap;
            DdsHeaderFlags flags = DdsHeaderFlags.Caps | DdsHeaderFlags.Height | DdsHeaderFlags.Width | DdsHeaderFlags.PixelFormat;
            if (hasMipmaps) { flags |= DdsHeaderFlags.MipMapCount; }
            if (isVolume) { flags |= DdsHeaderFlags.Depth; }
            flags |= isBlockCompressed ? DdsHeaderFlags.LinearSize : DdsHeaderFlags.Pitch;
            DdsCaps caps = DdsCaps.Texture;
            if (isComplex) { caps |= DdsCaps.Complex; }
            if (hasMipmaps) { caps |= DdsCaps.MipMap; }
            DdsCaps2 caps2 = DdsCaps2.None;
            if (isVolume) { caps2 |= DdsCaps2.Volume; }
            if (isCubemap) { caps2 |= DdsCaps2.Cubemap | DdsCaps2.CubemapAllFaces; }

            return new DdsHeader
            {
                Size = DdsConstants.HeaderSize,
                Flags = flags,
                Height = (uint)image.Height,
                Width = (uint)image.Width,
                PitchOrLinearSize = DdsPitchCalculator.GetTopLevelPitchOrLinearSize(image.Width, image.Format, isBlockCompressed),
                Depth = (uint)image.Depth,
                MipMapCount = (uint)image.MipLevels,
                PixelFormat = new DdsPixelFormat { Size = DdsConstants.PixelFormatSize, Flags = DdsPixelFormatFlags.FourCc, FourCC = DdsFourCc.Dx10 },
                Caps = caps,
                Caps2 = caps2,
            };
        }

        internal static DdsHeaderDx10 CreateDx10Header(Image image)
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
