using System.Buffers.Binary;
using System.Numerics;
using RedFox.Graphics2D.Codecs;

namespace RedFox.Graphics2D.IO
{
    /// <summary>
    /// An <see cref="ImageTranslator"/> for TGA (Truevision TARGA) files.
    /// Supports reading uncompressed true-color and RLE-compressed true-color TGA files.
    /// Writes uncompressed 32-bit BGRA or 24-bit BGR TGA files.
    /// </summary>
    public sealed class TgaImageTranslator : ImageTranslator
    {
        /// <inheritdoc/>
        public override string Name => "TGA";

        /// <inheritdoc/>
        public override bool CanRead => true;

        /// <inheritdoc/>
        public override bool CanWrite => true;

        /// <inheritdoc/>
        public override IReadOnlyList<string> Extensions { get; } = [".tga"];

        /// <inheritdoc/>
        public override Image Read(Stream stream)
        {
            Span<byte> header = stackalloc byte[18];
            stream.ReadExactly(header);

            int idLength = header[0];
            int colorMapType = header[1];
            int imageType = header[2];
            int xOrigin = BinaryPrimitives.ReadUInt16LittleEndian(header[8..]);
            int yOrigin = BinaryPrimitives.ReadUInt16LittleEndian(header[10..]);
            int width = BinaryPrimitives.ReadUInt16LittleEndian(header[12..]);
            int height = BinaryPrimitives.ReadUInt16LittleEndian(header[14..]);
            int bitsPerPixel = header[16];
            int descriptor = header[17];

            if (imageType is not (2 or 10))
                throw new NotSupportedException($"Unsupported TGA image type: {imageType}. Only uncompressed (2) and RLE (10) true-color are supported.");

            if (bitsPerPixel is not (24 or 32))
                throw new NotSupportedException($"Unsupported TGA bits per pixel: {bitsPerPixel}. Only 24 and 32 are supported.");

            if (colorMapType != 0)
                throw new NotSupportedException("Color-mapped TGA files are not supported.");

            // Skip image ID
            if (idLength > 0)
                stream.Seek(idLength, SeekOrigin.Current);

            int bytesPerPixel = bitsPerPixel / 8;
            int pixelCount = width * height;
            bool hasAlpha = bitsPerPixel == 32;
            bool topToBottom = (descriptor & 0x20) != 0;

            // Read raw pixel data (BGR or BGRA)
            var rawPixels = new byte[pixelCount * bytesPerPixel];

            // Run-length packet
            Span<byte> pixel = stackalloc byte[bytesPerPixel];

            if (imageType == 2)
            {
                stream.ReadExactly(rawPixels);
            }
            else
            {
                // RLE decode
                int pixelsRead = 0;
                while (pixelsRead < pixelCount)
                {
                    int packetHeader = stream.ReadByte();
                    if (packetHeader < 0)
                        throw new InvalidDataException("Unexpected end of TGA RLE data.");

                    int count = (packetHeader & 0x7F) + 1;

                    if ((packetHeader & 0x80) != 0)
                    {
                        stream.ReadExactly(pixel);

                        for (int i = 0; i < count && pixelsRead < pixelCount; i++)
                        {
                            pixel.CopyTo(rawPixels.AsSpan(pixelsRead * bytesPerPixel));
                            pixelsRead++;
                        }
                    }
                    else
                    {
                        // Raw packet
                        int bytesToRead = count * bytesPerPixel;
                        stream.ReadExactly(rawPixels.AsSpan(pixelsRead * bytesPerPixel, bytesToRead));
                        pixelsRead += count;
                    }
                }
            }

            // Convert to R8G8B8A8: TGA stores pixels as BGR/BGRA
            var output = new byte[pixelCount * 4];

            for (int i = 0; i < pixelCount; i++)
            {
                int srcOffset = i * bytesPerPixel;
                int dstOffset = i * 4;
                output[dstOffset + 0] = rawPixels[srcOffset + 2]; // R
                output[dstOffset + 1] = rawPixels[srcOffset + 1]; // G
                output[dstOffset + 2] = rawPixels[srcOffset + 0]; // B
                output[dstOffset + 3] = hasAlpha ? rawPixels[srcOffset + 3] : (byte)255;
            }

            // Flip vertically if stored bottom-to-top (default TGA orientation)
            if (!topToBottom)
            {
                int rowBytes = width * 4;
                var temp = new byte[rowBytes];

                for (int y = 0; y < height / 2; y++)
                {
                    int topOffset = y * rowBytes;
                    int bottomOffset = (height - 1 - y) * rowBytes;

                    output.AsSpan(topOffset, rowBytes).CopyTo(temp);
                    output.AsSpan(bottomOffset, rowBytes).CopyTo(output.AsSpan(topOffset, rowBytes));
                    temp.CopyTo(output.AsSpan(bottomOffset, rowBytes));
                }
            }

            return new Image(width, height, ImageFormat.R8G8B8A8Unorm, output);
        }

        /// <inheritdoc/>
        public override void Write(Stream stream, Image image)
        {
            ref readonly var slice = ref image.GetSlice(0, 0, 0);

            int width = slice.Width;
            int height = slice.Height;
            var format = image.Format;
            var pixelSpan = slice.PixelSpan;

            // Fast path: source is already BGRA — zero copy write
            if (format is ImageFormat.B8G8R8A8Unorm or ImageFormat.B8G8R8A8UnormSrgb)
            {
                bool hasAlpha = HasNonOpaqueAlpha(pixelSpan, stride: 4, alphaOffset: 3);
                WriteHeader(stream, width, height, hasAlpha);

                if (hasAlpha)
                {
                    stream.Write(pixelSpan);
                }
                else
                {
                    WriteRowsBgr(stream, pixelSpan, width, height, srcBpp: 4);
                }
                return;
            }

            // Fast path: source is RGBA — inline swizzle, no bulk allocation
            if (format is ImageFormat.R8G8B8A8Unorm or ImageFormat.R8G8B8A8UnormSrgb)
            {
                bool hasAlpha = HasNonOpaqueAlpha(pixelSpan, stride: 4, alphaOffset: 3);
                WriteHeader(stream, width, height, hasAlpha);
                WriteRowsSwizzled(stream, pixelSpan, width, height, hasAlpha);
                return;
            }

            // General path: per-pixel decode via codec — works for all formats including BC
            IPixelCodec codec = PixelCodecRegistry.GetCodec(format);
            WriteHeader(stream, width, height, hasAlpha: true);
            WriteRowsDecoded(stream, slice, width, height, codec);
        }

        private static void WriteHeader(Stream stream, int width, int height, bool hasAlpha)
        {
            int bpp = hasAlpha ? 32 : 24;

            Span<byte> header = stackalloc byte[18];
            header.Clear();
            header[2] = 2; // Uncompressed true-color
            BinaryPrimitives.WriteUInt16LittleEndian(header[12..], (ushort)width);
            BinaryPrimitives.WriteUInt16LittleEndian(header[14..], (ushort)height);
            header[16] = (byte)bpp;
            header[17] = (byte)((hasAlpha ? 8 : 0) | 0x20); // Alpha bits + top-left origin

            stream.Write(header);
        }

        /// <summary>
        /// Scans byte data for any alpha value &lt; 255.
        /// </summary>
        private static bool HasNonOpaqueAlpha(ReadOnlySpan<byte> data, int stride, int alphaOffset)
        {
            for (int i = alphaOffset; i < data.Length; i += stride)
            {
                if (data[i] < 255)
                    return true;
            }
            return false;
        }

        /// <summary>
        /// Writes BGR rows from BGRA source, stripping the alpha byte. No allocations beyond a stack row buffer.
        /// </summary>
        private static void WriteRowsBgr(Stream stream, ReadOnlySpan<byte> bgra, int width, int height, int srcBpp)
        {
            var rowBuffer = new byte[width * 3];

            for (int y = 0; y < height; y++)
            {
                int rowStart = y * width * srcBpp;

                for (int x = 0; x < width; x++)
                {
                    int s = rowStart + x * srcBpp;
                    int d = x * 3;
                    rowBuffer[d + 0] = bgra[s + 0];
                    rowBuffer[d + 1] = bgra[s + 1];
                    rowBuffer[d + 2] = bgra[s + 2];
                }

                stream.Write(rowBuffer);
            }
        }

        /// <summary>
        /// Writes from RGBA source with R↔B swizzle to produce BGRA/BGR output.
        /// </summary>
        private static void WriteRowsSwizzled(Stream stream, ReadOnlySpan<byte> rgba, int width, int height, bool hasAlpha)
        {
            int outBpp = hasAlpha ? 4 : 3;
            var rowBuffer = new byte[width * outBpp];

            for (int y = 0; y < height; y++)
            {
                int rowStart = y * width * 4;

                for (int x = 0; x < width; x++)
                {
                    int s = rowStart + x * 4;
                    int d = x * outBpp;
                    rowBuffer[d + 0] = rgba[s + 2]; // B
                    rowBuffer[d + 1] = rgba[s + 1]; // G
                    rowBuffer[d + 2] = rgba[s + 0]; // R
                    if (hasAlpha)
                        rowBuffer[d + 3] = rgba[s + 3]; // A
                }

                stream.Write(rowBuffer);
            }
        }

        /// <summary>
        /// Strip-based decode path: decodes 4 rows at a time (matching BC block height), then writes BGRA.
        /// Each block is decoded exactly once — no redundant per-pixel block decoding.
        /// </summary>
        private static void WriteRowsDecoded(Stream stream, in ImageSlice slice, int width, int height, IPixelCodec codec)
        {
            const int StripHeight = 4;
            var pixelBuf = new Vector4[width * StripHeight];
            var rowBuffer = new byte[width * 4];
            var pixelSpan = slice.PixelSpan;

            for (int stripY = 0; stripY < height; stripY += StripHeight)
            {
                int rows = Math.Min(StripHeight, height - stripY);
                codec.DecodeRows(pixelSpan, pixelBuf, stripY, rows, width, height);

                for (int row = 0; row < rows; row++)
                {
                    int pixelBase = row * width;

                    for (int x = 0; x < width; x++)
                    {
                        var pixel = pixelBuf[pixelBase + x];
                        int d = x * 4;
                        rowBuffer[d + 0] = (byte)(Math.Clamp(pixel.Z, 0f, 1f) * 255f + 0.5f); // B
                        rowBuffer[d + 1] = (byte)(Math.Clamp(pixel.Y, 0f, 1f) * 255f + 0.5f); // G
                        rowBuffer[d + 2] = (byte)(Math.Clamp(pixel.X, 0f, 1f) * 255f + 0.5f); // R
                        rowBuffer[d + 3] = (byte)(Math.Clamp(pixel.W, 0f, 1f) * 255f + 0.5f); // A
                    }

                    stream.Write(rowBuffer);
                }
            }
        }

        /// <inheritdoc/>
        public override bool IsValid(ReadOnlySpan<byte> header, string filePath, string extension)
        {
            // TGA has no magic number, so rely on extension and basic header sanity
            if (!IsValid(filePath, extension))
                return false;

            if (header.Length < 18)
                return false;

            int imageType = header[2];
            int bitsPerPixel = header[16];

            // Only support true-color types we handle
            return imageType is 2 or 10
                && bitsPerPixel is 24 or 32;
        }
    }
}
