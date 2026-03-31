using System.Buffers.Binary;

namespace RedFox.Graphics2D.Ktx
{
    internal static class KtxLoader
    {
        public static Image Load(Stream stream)
        {
            ArgumentNullException.ThrowIfNull(stream);
            if (!stream.CanRead)
                throw new IOException("The supplied stream is not readable.");

            KtxHeader header = ReadHeader(stream, out bool swapEndianness);

            if (header.NumberOfFaces is not 1 and not 6)
                throw new InvalidDataException($"KTX numberOfFaces must be 1 or 6. The file declared {header.NumberOfFaces}.");

            ImageFormat format = KtxFormatMapper.GetImageFormat(header);
            int width = checked((int)header.PixelWidth);
            int height = header.PixelHeight == 0 ? 1 : checked((int)header.PixelHeight);
            int depth = header.PixelDepth == 0 ? 1 : checked((int)header.PixelDepth);
            int mipLevels = header.NumberOfMipmapLevels == 0 ? 1 : checked((int)header.NumberOfMipmapLevels);
            int arrayElements = header.NumberOfArrayElements == 0 ? 1 : checked((int)header.NumberOfArrayElements);
            bool isCubemap = header.NumberOfFaces == 6;
            int arraySize = isCubemap
                ? checked(arrayElements * 6)
                : arrayElements;

            Skip(stream, checked((int)header.BytesOfKeyValueData));

            Image image = new(width, height, depth, arraySize, mipLevels, format, isCubemap);

            for (int mipLevel = 0; mipLevel < mipLevels; mipLevel++)
            {
                uint rawImageSize = ReadUInt32(stream, swapEndianness);
                int mipWidth = Math.Max(1, width >> mipLevel);
                int mipHeight = Math.Max(1, height >> mipLevel);
                int mipDepth = Math.Max(1, depth >> mipLevel);
                int slicePitch = ImageFormatInfo.CalculatePitch(format, mipWidth, mipHeight).SlicePitch;
                int faceCount = checked((int)header.NumberOfFaces);
                int faceImageSize = checked((int)rawImageSize);
                int cubePadding = isCubemap && header.NumberOfArrayElements == 0
                    ? GetPadding(faceImageSize)
                    : 0;

                if (isCubemap && header.NumberOfArrayElements == 0)
                {
                    if (faceImageSize != slicePitch)
                        throw new InvalidDataException($"Unexpected KTX cubemap face size for mip {mipLevel}. Expected {slicePitch} bytes but found {faceImageSize}.");
                }
                else
                {
                    int expectedMipSize = checked(slicePitch * arrayElements * faceCount * mipDepth);
                    if (faceImageSize != expectedMipSize)
                        throw new InvalidDataException($"Unexpected KTX mip size for mip {mipLevel}. Expected {expectedMipSize} bytes but found {faceImageSize}.");
                }

                for (int arrayElement = 0; arrayElement < arrayElements; arrayElement++)
                {
                    for (int face = 0; face < faceCount; face++)
                    {
                        int arrayIndex = isCubemap
                            ? checked((arrayElement * 6) + face)
                            : arrayElement;

                        for (int depthSlice = 0; depthSlice < mipDepth; depthSlice++)
                        {
                            ref readonly ImageSlice slice = ref image.GetSlice(mipLevel, arrayIndex, depthSlice);
                            stream.ReadExactly(slice.PixelSpan);

                            if (swapEndianness && !ImageFormatInfo.IsBlockCompressed(format) && header.GlTypeSize > 1)
                                SwapElementEndianness(slice.PixelSpan, checked((int)header.GlTypeSize));
                        }

                        if (cubePadding > 0)
                            Skip(stream, cubePadding);
                    }
                }

                int mipDataSize = isCubemap && header.NumberOfArrayElements == 0
                    ? checked((faceImageSize + cubePadding) * faceCount)
                    : faceImageSize;
                int mipPadding = GetPadding(mipDataSize);
                if (mipPadding > 0)
                    Skip(stream, mipPadding);
            }

            return image;
        }

        private static KtxHeader ReadHeader(Stream stream, out bool swapEndianness)
        {
            Span<byte> identifier = stackalloc byte[12];
            stream.ReadExactly(identifier);

            if (!identifier.SequenceEqual(KtxConstants.Identifier))
                throw new InvalidDataException("The supplied file is not a valid KTX texture.");

            Span<byte> headerBytes = stackalloc byte[13 * sizeof(uint)];
            stream.ReadExactly(headerBytes);

            uint endianness = BinaryPrimitives.ReadUInt32LittleEndian(headerBytes);
            swapEndianness = endianness == KtxConstants.ReversedEndianness;

            if (!swapEndianness && endianness != KtxConstants.NativeEndianness)
                throw new InvalidDataException($"Unsupported KTX endianness marker 0x{endianness:X8}.");

            return new KtxHeader(
                endianness,
                ReadHeaderUInt32(headerBytes, 1, swapEndianness),
                ReadHeaderUInt32(headerBytes, 2, swapEndianness),
                ReadHeaderUInt32(headerBytes, 3, swapEndianness),
                ReadHeaderUInt32(headerBytes, 4, swapEndianness),
                ReadHeaderUInt32(headerBytes, 5, swapEndianness),
                ReadHeaderUInt32(headerBytes, 6, swapEndianness),
                ReadHeaderUInt32(headerBytes, 7, swapEndianness),
                ReadHeaderUInt32(headerBytes, 8, swapEndianness),
                ReadHeaderUInt32(headerBytes, 9, swapEndianness),
                ReadHeaderUInt32(headerBytes, 10, swapEndianness),
                ReadHeaderUInt32(headerBytes, 11, swapEndianness),
                ReadHeaderUInt32(headerBytes, 12, swapEndianness));
        }

        private static uint ReadHeaderUInt32(ReadOnlySpan<byte> headerBytes, int fieldIndex, bool swapEndianness)
        {
            uint value = BinaryPrimitives.ReadUInt32LittleEndian(headerBytes[(fieldIndex * sizeof(uint))..]);
            return swapEndianness ? BinaryPrimitives.ReverseEndianness(value) : value;
        }

        private static uint ReadUInt32(Stream stream, bool swapEndianness)
        {
            Span<byte> buffer = stackalloc byte[sizeof(uint)];
            stream.ReadExactly(buffer);
            uint value = BinaryPrimitives.ReadUInt32LittleEndian(buffer);
            return swapEndianness ? BinaryPrimitives.ReverseEndianness(value) : value;
        }

        private static void Skip(Stream stream, int byteCount)
        {
            if (byteCount <= 0)
                return;

            if (stream.CanSeek)
            {
                stream.Seek(byteCount, SeekOrigin.Current);
                return;
            }

            Span<byte> scratch = stackalloc byte[256];
            int remaining = byteCount;

            while (remaining > 0)
            {
                int chunk = Math.Min(remaining, scratch.Length);
                stream.ReadExactly(scratch[..chunk]);
                remaining -= chunk;
            }
        }

        private static int GetPadding(int byteCount)
        {
            return (4 - (byteCount & 3)) & 3;
        }

        private static void SwapElementEndianness(Span<byte> data, int elementSize)
        {
            switch (elementSize)
            {
                case 2:
                    for (int offset = 0; offset < data.Length; offset += 2)
                        (data[offset], data[offset + 1]) = (data[offset + 1], data[offset]);
                    break;

                case 4:
                    for (int offset = 0; offset < data.Length; offset += 4)
                    {
                        (data[offset], data[offset + 3]) = (data[offset + 3], data[offset]);
                        (data[offset + 1], data[offset + 2]) = (data[offset + 2], data[offset + 1]);
                    }
                    break;

                default:
                    throw new NotSupportedException($"KTX endianness conversion for {elementSize}-byte elements is not supported.");
            }
        }
    }
}
