using System.Buffers;
using System.IO.Compression;
using static RedFox.Graphics2D.Tiff.TiffConstants;

namespace RedFox.Graphics2D.Tiff
{
    /// <summary>
    /// Provides decompression methods for TIFF compression schemes:
    /// LZW (Lempel-Ziv-Welch, MSB-first) and PackBits (byte-oriented RLE).
    /// </summary>
    public static partial class TiffDecompressor
    {
        /// <summary>
        /// Decompresses PackBits (byte-oriented run-length encoding) data.
        /// </summary>
        /// <param name="src">The compressed input data.</param>
        /// <param name="expectedSize">The expected uncompressed output size in bytes.</param>
        /// <returns>A byte array containing the decompressed data.</returns>
        public static byte[] DecompressPackBits(ReadOnlySpan<byte> src, int expectedSize)
        {
            var output = new byte[expectedSize];
            int srcPos = 0;
            int dstPos = 0;

            while (srcPos < src.Length && dstPos < expectedSize)
            {
                sbyte n = (sbyte)src[srcPos++];

                if (n >= 0)
                {
                    // Literal run: copy the next (n + 1) bytes as-is.
                    int count = n + 1;
                    int toCopy = Math.Min(count, expectedSize - dstPos);
                    src.Slice(srcPos, toCopy).CopyTo(output.AsSpan(dstPos));
                    srcPos += count;
                    dstPos += toCopy;
                }
                else if (n != -128)
                {
                    // Repeat run: replicate the next byte (1 - n) times.
                    // n = -128 is a no-op per the PackBits specification.
                    int count = 1 - n;
                    byte value = src[srcPos++];
                    int toFill = Math.Min(count, expectedSize - dstPos);
                    output.AsSpan(dstPos, toFill).Fill(value);
                    dstPos += toFill;
                }
            }

            return output;
        }

        /// <summary>
        /// Decompresses TIFF Deflate/ZIP data.
        /// Supports both zlib-wrapped and raw deflate streams.
        /// </summary>
        /// <param name="src">The compressed input data.</param>
        /// <param name="expectedSize">The expected uncompressed output size in bytes.</param>
        /// <returns>A byte array containing the decompressed data.</returns>
        public static byte[] DecompressDeflate(ReadOnlySpan<byte> src, int expectedSize)
        {
            bool usesZlibWrapper = UsesZlibWrapper(src);
            if (TryDecompressDeflate(src, expectedSize, usesZlibWrapper, out byte[]? output) && output is not null)
                return output;

            throw new InvalidDataException("Unable to decompress TIFF Deflate data.");
        }

        /// <summary>
        /// Decompresses TIFF LZW data using MSB-first bit packing and variable code widths (9–12 bits).
        /// This implementation follows the TIFF 6.0 LZW variant which expects a Clear code
        /// at the start of the stream and performs the early code-size increase behavior.
        /// </summary>
        /// <param name="src">The compressed input data span.</param>
        /// <param name="expectedSize">The expected size of the decompressed output in bytes.</param>
        /// <returns>A newly allocated byte array containing the decompressed data.</returns>
        public static byte[] DecompressLZW(ReadOnlySpan<byte> src, int expectedSize)
        {
            if (TryDecompressLZWCore(src, expectedSize, leastSignificantBitFirst: false, codeSizeThresholdOffset: -1, out byte[]? standardOutput) && standardOutput is not null)
                return standardOutput;

            if (TryDecompressLZWCore(src, expectedSize, leastSignificantBitFirst: true, codeSizeThresholdOffset: 0, out byte[]? legacyOutput) && legacyOutput is not null)
                return legacyOutput;

            throw new InvalidDataException("Unable to decompress TIFF LZW data.");
        }

        /// <summary>
        /// Attempts TIFF LZW decompression using the specified bit-order and code-size strategy.
        /// </summary>
        /// <param name="src">The compressed input span.</param>
        /// <param name="expectedSize">The expected size of the decompressed output.</param>
        /// <param name="leastSignificantBitFirst"><see langword="true"/> to interpret codes as LSB-first; otherwise MSB-first.</param>
        /// <param name="codeSizeThresholdOffset">The offset applied when advancing to the next code width.</param>
        /// <param name="output">Receives the decompressed bytes when decoding succeeds.</param>
        /// <returns><see langword="true"/> when decoding succeeded; otherwise <see langword="false"/>.</returns>
        public static bool TryDecompressLZWCore(ReadOnlySpan<byte> src, int expectedSize, bool leastSignificantBitFirst, int codeSizeThresholdOffset, out byte[]? output)
        {
            byte[] decodedOutput = new byte[expectedSize];
            int dstPos = 0;

            // String table: each entry stores (prefix index, suffix byte, total length).
            // Rent working arrays from the shared ArrayPool to reduce GC pressure for
            // repeated decompression operations.
            int[] prefixes = ArrayPool<int>.Shared.Rent(LzwMaxTableSize);
            byte[] suffixes = ArrayPool<byte>.Shared.Rent(LzwMaxTableSize);
            int[] lengths = ArrayPool<int>.Shared.Rent(LzwMaxTableSize);
            byte[] decodeBuffer = ArrayPool<byte>.Shared.Rent(LzwMaxTableSize + 1);

            try
            {
                LzwDecoder decoder = new(src, prefixes, suffixes, lengths, decodeBuffer, LzwFirstCode, leastSignificantBitFirst);

                decoder.ResetTable();

                int prevCode = -1;

                while (dstPos < expectedSize)
                {
                    if (!decoder.TryReadCode(out int code) || code == LzwEoiCode)
                        break;

                    if (code == LzwClearCode)
                    {
                        decoder.ResetTable();
                        prevCode = -1;
                        continue;
                    }

                    if ((uint)code >= LzwMaxTableSize)
                        throw new InvalidDataException($"Invalid TIFF LZW code: {code}.");

                    if (prevCode < 0)
                    {
                        if (!decoder.TryDecodeString(code, out int len))
                        {
                            output = null;
                            return false;
                        }

                        int toCopy = Math.Min(len, expectedSize - dstPos);
                        decoder.DecodeBufferSpan.Slice(0, toCopy).CopyTo(decodedOutput.AsSpan(dstPos));
                        dstPos += toCopy;
                        prevCode = code;
                        continue;
                    }

                    byte firstByte;
                    int decodedLength;

                    if (code < decoder.NextCode)
                    {
                        if (!decoder.TryDecodeString(code, out decodedLength))
                        {
                            output = null;
                            return false;
                        }

                        firstByte = decoder.DecodeBufferSpan[0];
                    }
                    else if (code == decoder.NextCode)
                    {
                        if (!decoder.TryDecodeString(prevCode, out decodedLength))
                        {
                            output = null;
                            return false;
                        }

                        if ((uint)decodedLength >= (uint)decoder.DecodeBufferSpan.Length)
                        {
                            output = null;
                            return false;
                        }

                        firstByte = decoder.DecodeBufferSpan[0];
                        decoder.DecodeBufferSpan[decodedLength] = firstByte;
                        decodedLength++;
                    }
                    else
                    {
                        output = null;
                        return false;
                    }

                    int bytesToCopy = Math.Min(decodedLength, expectedSize - dstPos);
                    decoder.DecodeBufferSpan.Slice(0, bytesToCopy).CopyTo(decodedOutput.AsSpan(dstPos));
                    dstPos += bytesToCopy;

                    if (decoder.NextCode < LzwMaxTableSize)
                    {
                        int previousLength = lengths[prevCode];
                        if (previousLength <= 0 || previousLength >= decodeBuffer.Length)
                        {
                            output = null;
                            return false;
                        }

                        prefixes[decoder.NextCode] = prevCode;
                        suffixes[decoder.NextCode] = firstByte;
                        lengths[decoder.NextCode] = previousLength + 1;
                        decoder.NextCode++;

                        AdjustCodeSize(ref decoder, codeSizeThresholdOffset);
                    }

                    prevCode = code;
                }

                if (dstPos != expectedSize)
                {
                    output = null;
                    return false;
                }

                output = decodedOutput;
                return true;
            }
            finally
            {
                ArrayPool<int>.Shared.Return(prefixes);
                ArrayPool<byte>.Shared.Return(suffixes);
                ArrayPool<int>.Shared.Return(lengths);
                ArrayPool<byte>.Shared.Return(decodeBuffer);
            }
        }

        /// <summary>
        /// Adjusts the active LZW code size when the decoder reaches the next threshold.
        /// </summary>
        /// <param name="decoder">The decoder whose active code width may be increased.</param>
        /// <param name="codeSizeThresholdOffset">The offset applied to the next-code threshold.</param>
        public static void AdjustCodeSize(ref LzwDecoder decoder, int codeSizeThresholdOffset)
        {
            if (decoder.CodeSize >= LzwMaxCodeSize)
                return;

            int threshold = (1 << decoder.CodeSize) + codeSizeThresholdOffset;
            if (decoder.NextCode >= threshold)
                decoder.CodeSize++;
        }

        /// <summary>
        /// Determines whether a Deflate-compressed TIFF payload appears to use a zlib wrapper.
        /// </summary>
        /// <param name="src">The compressed TIFF Deflate payload to inspect.</param>
        /// <returns><see langword="true"/> when the payload appears to have a zlib wrapper; otherwise <see langword="false"/>.</returns>
        public static bool UsesZlibWrapper(ReadOnlySpan<byte> src)
        {
            if (src.Length < 2)
                return false;

            int cmf = src[0];
            int flg = src[1];

            if ((cmf & 0x0F) != 8)
                return false;

            if ((cmf >> 4) > 7)
                return false;

            return ((cmf << 8) + flg) % 31 == 0;
        }

        /// <summary>
        /// Attempts to decompress TIFF Deflate data using either zlib-wrapped or raw Deflate input.
        /// </summary>
        /// <param name="src">The compressed TIFF Deflate payload.</param>
        /// <param name="expectedSize">The expected uncompressed size in bytes.</param>
        /// <param name="usesZlibWrapper"><see langword="true"/> to decode using a zlib wrapper; otherwise raw Deflate.</param>
        /// <param name="result">Receives the decompressed bytes when decoding succeeds.</param>
        /// <returns><see langword="true"/> when decompression succeeded; otherwise <see langword="false"/>.</returns>
        public static bool TryDecompressDeflate(
            ReadOnlySpan<byte> src,
            int expectedSize,
            bool usesZlibWrapper,
            out byte[]? result)
        {
            try
            {
                using MemoryStream input = new(src.ToArray(), writable: false);
                using Stream inflater = usesZlibWrapper
                    ? new ZLibStream(input, CompressionMode.Decompress)
                    : new DeflateStream(input, CompressionMode.Decompress);
                byte[] output = new byte[expectedSize];
                int totalRead = 0;

                while (totalRead < expectedSize)
                {
                    int bytesRead = inflater.Read(output, totalRead, expectedSize - totalRead);
                    if (bytesRead == 0)
                        break;

                    totalRead += bytesRead;
                }

                if (totalRead != expectedSize)
                {
                    result = null;
                    return false;
                }

                result = output;
                return true;
            }
            catch (InvalidDataException)
            {
                result = null;
                return false;
            }
        }
    }
}
