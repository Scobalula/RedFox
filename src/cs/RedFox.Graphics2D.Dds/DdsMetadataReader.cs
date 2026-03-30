using System.Buffers.Binary;
using System.Runtime.CompilerServices;

namespace RedFox.Graphics2D.IO
{
    /// <summary>
    /// Reads and validates DDS image metadata from raw byte data.
    /// </summary>
    public static class DdsMetadataReader
    {
        /// <summary>
        /// Parses the DDS headers from the given data and returns the image metadata.
        /// </summary>
        /// <param name="data">The raw DDS file bytes.</param>
        /// <returns>A <see cref="DdsMetadata"/> describing the image dimensions, format, and data offset.</returns>
        /// <exception cref="InvalidDataException">Thrown when the data is not a valid DDS file.</exception>
        /// <exception cref="NotSupportedException">Thrown when the DDS format is not supported.</exception>
        public static DdsMetadata Read(ReadOnlySpan<byte> data)
        {
            int headerStructSize = Unsafe.SizeOf<DdsHeader>();
            if (data.Length < sizeof(uint) + headerStructSize)
            {
                throw new InvalidDataException("File is too small to be a valid DDS file.");
            }

            uint magic = BinaryPrimitives.ReadUInt32LittleEndian(data);
            if (magic != DdsConstants.Magic)
            {
                throw new InvalidDataException($"Invalid DDS magic number: 0x{magic:X8}.");
            }

            DdsHeader header = DdsStructSerializer.Read<DdsHeader>(data, sizeof(uint));
            ValidateHeader(header);

            int dataOffset = sizeof(uint) + headerStructSize;
            DdsBaseMetadata baseMetadata = new(
                ReadPositiveInt(header.Width, "width"),
                ReadPositiveInt(header.Height, "height"),
                (header.Flags & DdsHeaderFlags.MipMapCount) != 0 ? ReadPositiveInt(header.MipMapCount, "mip level count") : 1);

            var isDx10 = DdsLegacyFormatMapper.IsDx10(header.PixelFormat);
            return isDx10 ? ReadDx10Metadata(data, header, baseMetadata, dataOffset) : ReadLegacyMetadata(data, header, baseMetadata, dataOffset);
        }

        private static DdsMetadata ReadDx10Metadata(
            ReadOnlySpan<byte> data,
            in DdsHeader header,
            DdsBaseMetadata baseMetadata,
            int dataOffset)
        {
            int dxt10HeaderSize = Unsafe.SizeOf<DdsHeaderDx10>();
            if (data.Length < dataOffset + dxt10HeaderSize)
            {
                throw new InvalidDataException("DDS file is too small for DX10 extended header.");
            }

            DdsHeaderDx10 dxt10 = DdsStructSerializer.Read<DdsHeaderDx10>(data, dataOffset);
            dataOffset += dxt10HeaderSize;

            ImageFormat format = (ImageFormat)dxt10.DxgiFormat;
            if (!Enum.IsDefined<ImageFormat>(format))
            {
                throw new NotSupportedException($"Unsupported DDS DXGI format: {dxt10.DxgiFormat}.");
            }

            int arraySize = ReadPositiveInt(dxt10.ArraySize, "array size");
            bool isCubemap = (dxt10.MiscFlag & DdsConstants.Dxt10CubemapFlag) != 0;
            if (isCubemap)
            {
                checked { arraySize *= 6; }
            }

            bool isVolume = dxt10.ResourceDimension == DdsResourceDimension.Texture3D
                        || (header.Flags & DdsHeaderFlags.Depth) != 0
                        || (header.Caps2 & DdsCaps2.Volume) != 0;

            int depth = isVolume ? ReadPositiveInt(header.Depth, "depth") : 1;
            if (dxt10.ResourceDimension == DdsResourceDimension.Texture3D && arraySize != 1)
            {
                throw new InvalidDataException("DDS 3D textures with DX10 headers must declare an array size of 1.");
            }

            ValidatePayloadOffset(data, dataOffset);
            return baseMetadata.ToMetadata(depth, arraySize, format, isCubemap, dataOffset);
        }

        private static DdsMetadata ReadLegacyMetadata(ReadOnlySpan<byte> data, in DdsHeader header, DdsBaseMetadata baseMetadata, int dataOffset)
        {
            ImageFormat format = DdsLegacyFormatMapper.FromLegacyPixelFormat(header.PixelFormat);

            bool isCubemap = (header.Caps2 & DdsCaps2.Cubemap) != 0;
            int arraySize = 1;
            if (isCubemap)
            {
                int faceCount = CountLegacyCubemapFaces(header.Caps2);
                arraySize = faceCount == 0 ? 6 : faceCount;
            }

            bool isVolume = (header.Flags & DdsHeaderFlags.Depth) != 0 || (header.Caps2 & DdsCaps2.Volume) != 0;
            int depth = isVolume ? ReadPositiveInt(header.Depth, "depth") : 1;

            ValidatePayloadOffset(data, dataOffset);
            return baseMetadata.ToMetadata(depth, arraySize, format, isCubemap, dataOffset);
        }

        private static void ValidateHeader(in DdsHeader header)
        {
            if (header.Size != DdsConstants.HeaderSize)
            {
                throw new InvalidDataException($"Invalid DDS header size: {header.Size}.");
            }

            if (header.PixelFormat.Size != DdsConstants.PixelFormatSize)
            {
                throw new InvalidDataException($"Invalid DDS pixel format size: {header.PixelFormat.Size}.");
            }
        }

        private static int ReadPositiveInt(uint value, string fieldName)
        {
            if (value == 0 || value > int.MaxValue)
            {
                throw new InvalidDataException($"Invalid DDS {fieldName}: {value}.");
            }

            return (int)value;
        }

        private static void ValidatePayloadOffset(ReadOnlySpan<byte> data, int offset)
        {
            if (offset >= data.Length)
            {
                throw new InvalidDataException("DDS file does not contain pixel data.");
            }
        }

        private static int CountLegacyCubemapFaces(DdsCaps2 caps2)
        {
            int count = 0;
            if ((caps2 & DdsCaps2.CubemapPositiveX) != 0) { count++; }
            if ((caps2 & DdsCaps2.CubemapNegativeX) != 0) { count++; }
            if ((caps2 & DdsCaps2.CubemapPositiveY) != 0) { count++; }
            if ((caps2 & DdsCaps2.CubemapNegativeY) != 0) { count++; }
            if ((caps2 & DdsCaps2.CubemapPositiveZ) != 0) { count++; }
            if ((caps2 & DdsCaps2.CubemapNegativeZ) != 0) { count++; }
            return count;
        }
    }
}
