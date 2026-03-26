using System.Buffers.Binary;

namespace RedFox.Graphics2D.Tga
{
    /// <summary>
    /// Represents the 18-byte header of a TGA (Truevision TARGA) file.
    /// Contains all fields needed to interpret the pixel data that follows.
    /// </summary>
    /// <param name="IdLength">
    /// Number of bytes in the image identification field that follows this header.
    /// </param>
    /// <param name="ColorMapType">
    /// Indicates whether a color map is present (0 = none, 1 = present).
    /// </param>
    /// <param name="ImageType">
    /// The type of image stored in the file.
    /// </param>
    /// <param name="XOrigin">
    /// Horizontal coordinate of the lower-left corner of the image.
    /// </param>
    /// <param name="YOrigin">
    /// Vertical coordinate of the lower-left corner of the image.
    /// </param>
    /// <param name="Width">
    /// Image width in pixels.
    /// </param>
    /// <param name="Height">
    /// Image height in pixels.
    /// </param>
    /// <param name="BitsPerPixel">
    /// Number of bits per pixel (typically 24 or 32).
    /// </param>
    /// <param name="Descriptor">
    /// Image descriptor byte containing alpha channel depth (bits 0-3)
    /// and origin corner flags (bits 4-5).
    /// </param>
    public readonly record struct TgaHeader(
        byte IdLength,
        byte ColorMapType,
        TgaImageType ImageType,
        ushort XOrigin,
        ushort YOrigin,
        ushort Width,
        ushort Height,
        byte BitsPerPixel,
        byte Descriptor)
    {
        /// <summary>
        /// The fixed size of a TGA header in bytes.
        /// </summary>
        public const int SizeInBytes = 18;

        /// <summary>
        /// Gets whether the image is stored top-to-bottom.
        /// When <see langword="false"/>, rows are stored bottom-to-top (the TGA default)
        /// and must be flipped vertically after decoding.
        /// </summary>
        public bool IsTopToBottom => (Descriptor & 0x20) != 0;

        /// <summary>
        /// Gets the number of bytes per pixel derived from <see cref="BitsPerPixel"/>.
        /// </summary>
        public int BytesPerPixel => BitsPerPixel / 8;

        /// <summary>
        /// Gets whether the pixel data includes an alpha channel (32 bits per pixel).
        /// </summary>
        public bool HasAlpha => BitsPerPixel == 32;

        /// <summary>
        /// Parses a TGA header from 18 bytes of data.
        /// </summary>
        /// <param name="data">A span containing at least 18 bytes of header data.</param>
        /// <returns>The parsed <see cref="TgaHeader"/>.</returns>
        public static TgaHeader Parse(ReadOnlySpan<byte> data)
        {
            return new TgaHeader(
                data[0],
                data[1],
                (TgaImageType)data[2],
                BinaryPrimitives.ReadUInt16LittleEndian(data[8..]),
                BinaryPrimitives.ReadUInt16LittleEndian(data[10..]),
                BinaryPrimitives.ReadUInt16LittleEndian(data[12..]),
                BinaryPrimitives.ReadUInt16LittleEndian(data[14..]),
                data[16],
                data[17]);
        }

        /// <summary>
        /// Writes a TGA header for an uncompressed true-color image to the provided span.
        /// </summary>
        /// <param name="destination">A span of at least <see cref="SizeInBytes"/> bytes to write into.</param>
        /// <param name="width">Image width in pixels.</param>
        /// <param name="height">Image height in pixels.</param>
        /// <param name="hasAlpha">
        /// <see langword="true"/> to write a 32-bit BGRA header;
        /// <see langword="false"/> for 24-bit BGR.
        /// </param>
        public static void WriteTrueColor(Span<byte> destination, int width, int height, bool hasAlpha)
        {
            int bpp = hasAlpha ? 32 : 24;

            destination.Clear();
            destination[2] = (byte)TgaImageType.TrueColor;
            BinaryPrimitives.WriteUInt16LittleEndian(destination[12..], (ushort)width);
            BinaryPrimitives.WriteUInt16LittleEndian(destination[14..], (ushort)height);
            destination[16] = (byte)bpp;
            destination[17] = (byte)((hasAlpha ? 8 : 0) | 0x20); // alpha bits + top-left origin
        }
    }
}
