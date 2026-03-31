using System.Buffers.Binary;

namespace RedFox.Graphics2D.Ktx
{
    internal static class KtxWriter
    {
        public static void Save(Stream stream, Image image)
        {
            ArgumentNullException.ThrowIfNull(stream);
            ArgumentNullException.ThrowIfNull(image);

            ValidateImage(image);

            KtxFormatDescriptor descriptor = KtxFormatMapper.GetDescriptor(image.Format);
            int arrayElements = GetArrayElementCount(image);
            int faceCount = image.IsCubemap ? 6 : 1;

            WriteHeader(stream, image, descriptor, arrayElements, faceCount);

            for (int mipLevel = 0; mipLevel < image.MipLevels; mipLevel++)
            {
                int mipWidth = Math.Max(1, image.Width >> mipLevel);
                int mipHeight = Math.Max(1, image.Height >> mipLevel);
                int mipDepth = Math.Max(1, image.Depth >> mipLevel);
                int slicePitch = ImageFormatInfo.CalculatePitch(image.Format, mipWidth, mipHeight).SlicePitch;
                int imageSize = image.IsCubemap && arrayElements == 1
                    ? slicePitch
                    : checked(slicePitch * arrayElements * faceCount * mipDepth);

                WriteUInt32(stream, checked((uint)imageSize));

                int cubePadding = image.IsCubemap && arrayElements == 1
                    ? GetPadding(imageSize)
                    : 0;

                for (int arrayElement = 0; arrayElement < arrayElements; arrayElement++)
                {
                    for (int face = 0; face < faceCount; face++)
                    {
                        int arrayIndex = image.IsCubemap
                            ? checked((arrayElement * 6) + face)
                            : arrayElement;

                        for (int depthSlice = 0; depthSlice < mipDepth; depthSlice++)
                        {
                            ref readonly ImageSlice slice = ref image.GetSlice(mipLevel, arrayIndex, depthSlice);
                            stream.Write(slice.PixelSpan);
                        }

                        WritePadding(stream, cubePadding);
                    }
                }

                int mipDataSize = image.IsCubemap && arrayElements == 1
                    ? checked((imageSize + cubePadding) * faceCount)
                    : imageSize;
                WritePadding(stream, GetPadding(mipDataSize));
            }
        }

        private static void ValidateImage(Image image)
        {
            if (image.IsCubemap && image.ArraySize % 6 != 0)
                throw new InvalidDataException("Cubemap KTX images must have an array size that is a multiple of 6.");

            if (image.IsCubemap && image.Depth != 1)
                throw new InvalidDataException("Cubemap KTX images must have a depth of 1.");

            if (image.IsCubemap && image.Width != image.Height)
                throw new InvalidDataException("Cubemap KTX images must be square.");
        }

        private static int GetArrayElementCount(Image image)
        {
            if (image.IsCubemap)
                return image.ArraySize / 6;

            return image.ArraySize;
        }

        private static void WriteHeader(
            Stream stream,
            Image image,
            KtxFormatDescriptor descriptor,
            int arrayElements,
            int faceCount)
        {
            stream.Write(KtxConstants.Identifier);

            Span<byte> header = stackalloc byte[13 * sizeof(uint)];
            WriteUInt32(header, 0, KtxConstants.NativeEndianness);
            WriteUInt32(header, 1, descriptor.GlType);
            WriteUInt32(header, 2, descriptor.GlTypeSize);
            WriteUInt32(header, 3, descriptor.GlFormat);
            WriteUInt32(header, 4, descriptor.GlInternalFormat);
            WriteUInt32(header, 5, descriptor.GlBaseInternalFormat);
            WriteUInt32(header, 6, checked((uint)image.Width));
            WriteUInt32(header, 7, checked((uint)image.Height));
            WriteUInt32(header, 8, image.Depth > 1 ? checked((uint)image.Depth) : 0u);
            WriteUInt32(header, 9, image.IsCubemap
                ? (arrayElements > 1 ? checked((uint)arrayElements) : 0u)
                : (image.ArraySize > 1 ? checked((uint)image.ArraySize) : 0u));
            WriteUInt32(header, 10, checked((uint)faceCount));
            WriteUInt32(header, 11, checked((uint)image.MipLevels));
            WriteUInt32(header, 12, 0u);
            stream.Write(header);
        }

        private static void WriteUInt32(Stream stream, uint value)
        {
            Span<byte> buffer = stackalloc byte[sizeof(uint)];
            BinaryPrimitives.WriteUInt32LittleEndian(buffer, value);
            stream.Write(buffer);
        }

        private static void WriteUInt32(Span<byte> buffer, int fieldIndex, uint value)
        {
            BinaryPrimitives.WriteUInt32LittleEndian(buffer[(fieldIndex * sizeof(uint))..], value);
        }

        private static int GetPadding(int byteCount)
        {
            return (4 - (byteCount & 3)) & 3;
        }

        private static void WritePadding(Stream stream, int byteCount)
        {
            if (byteCount <= 0)
                return;

            Span<byte> padding = stackalloc byte[4];
            stream.Write(padding[..byteCount]);
        }
    }
}
