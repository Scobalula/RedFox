namespace RedFox.Graphics2D.Tga
{
    /// <summary>
    /// Provides run-length decoding for TGA image data.
    /// TGA RLE encodes packets with a one-byte header: if bit 7 is set the packet is a
    /// run-length repeat; otherwise it is a literal (raw) packet. The lower 7 bits encode
    /// the count minus one.
    /// </summary>
    public static class TgaRleDecoder
    {
        /// <summary>
        /// Decodes RLE-compressed TGA pixel data from a stream into a destination buffer.
        /// </summary>
        /// <param name="stream">The source stream positioned at the start of the RLE data.</param>
        /// <param name="destination">The buffer to write decoded pixel data into.</param>
        /// <param name="pixelCount">The total number of pixels to decode.</param>
        /// <param name="bytesPerPixel">The number of bytes per pixel (3 for BGR, 4 for BGRA).</param>
        public static void Decode(Stream stream, Span<byte> destination, int pixelCount, int bytesPerPixel)
        {
            Span<byte> pixel = stackalloc byte[bytesPerPixel];
            int pixelsRead = 0;

            while (pixelsRead < pixelCount)
            {
                int packetHeader = stream.ReadByte();
                if (packetHeader < 0)
                {
                    throw new InvalidDataException("Unexpected end of TGA RLE data.");
                }

                int count = (packetHeader & 0x7F) + 1;

                if ((packetHeader & 0x80) != 0)
                {
                    // Run-length packet: one pixel value repeated 'count' times.
                    stream.ReadExactly(pixel);

                    for (int i = 0; i < count && pixelsRead < pixelCount; i++)
                    {
                        pixel.CopyTo(destination.Slice(pixelsRead * bytesPerPixel, bytesPerPixel));
                        pixelsRead++;
                    }
                }
                else
                {
                    // Raw packet: 'count' literal pixels follow.
                    int bytesToRead = count * bytesPerPixel;
                    stream.ReadExactly(destination.Slice(pixelsRead * bytesPerPixel, bytesToRead));
                    pixelsRead += count;
                }
            }
        }
    }
}
