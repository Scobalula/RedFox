using System;
using System.Buffers;
using System.IO;
using System.IO.Compression;

namespace RedFox.Imaging.Formats.Tiff;

/// <summary>
/// Provides decompression methods for TIFF compression schemes:
/// LZW (Lempel-Ziv-Welch, MSB-first) and PackBits (byte-oriented RLE).
/// </summary>
internal static partial class TiffDecompressor
{
    /// <summary>
    /// Decompresses PackBits (byte-oriented run-length encoding) data.
    /// </summary>
    /// <param name="compressedData">The compressed input data.</param>
    /// <param name="expectedSize">The expected uncompressed output size in bytes.</param>
    /// <returns>A byte array containing the decompressed data.</returns>
    public static byte[] DecompressPackBits(ReadOnlySpan<byte> compressedData, int expectedSize)
    {
        var output = new byte[expectedSize];
        int inputPosition = 0;
        int outputPosition = 0;

        while (inputPosition < compressedData.Length && outputPosition < expectedSize)
        {
            sbyte runHeader = (sbyte)compressedData[inputPosition++];

            if (runHeader >= 0)
            {
                int count = runHeader + 1;
                int toCopy = Math.Min(count, expectedSize - outputPosition);
                compressedData.Slice(inputPosition, toCopy).CopyTo(output.AsSpan(outputPosition));
                inputPosition += count;
                outputPosition += toCopy;
            }
            else if (runHeader != -128)
            {
                // n = -128 is a no-op per the PackBits specification.
                int count = 1 - runHeader;
                byte value = compressedData[inputPosition++];
                int toFill = Math.Min(count, expectedSize - outputPosition);
                output.AsSpan(outputPosition, toFill).Fill(value);
                outputPosition += toFill;
            }
        }

        return output;
    }

    /// <summary>
    /// Decompresses TIFF Deflate/ZIP data.
    /// Supports both zlib-wrapped and raw deflate streams.
    /// </summary>
    /// <param name="compressedData">The compressed input data.</param>
    /// <param name="expectedSize">The expected uncompressed output size in bytes.</param>
    /// <returns>A byte array containing the decompressed data.</returns>
    public static byte[] DecompressDeflate(ReadOnlySpan<byte> compressedData, int expectedSize)
    {
        bool usesZlibWrapper = UsesZlibWrapper(compressedData);
        if (TryDecompressDeflate(compressedData, expectedSize, usesZlibWrapper, out byte[]? output) && output is not null)
            return output;

        throw new InvalidDataException("Unable to decompress TIFF Deflate data.");
    }

    /// <summary>
    /// Decompresses TIFF LZW data using MSB-first bit packing and variable code widths (9–12 bits).
    /// This implementation follows the TIFF 6.0 LZW variant which expects a Clear code
    /// at the start of the stream and performs the early code-size increase behavior.
    /// </summary>
    /// <param name="compressedData">The compressed input data span.</param>
    /// <param name="expectedSize">The expected size of the decompressed output in bytes.</param>
    /// <returns>A newly allocated byte array containing the decompressed data.</returns>
    public static byte[] DecompressLZW(ReadOnlySpan<byte> compressedData, int expectedSize)
    {
        if (TryDecompressLzw(compressedData, expectedSize, leastSignificantBitFirst: false, codeSizeThresholdOffset: -1, out byte[]? standardOutput) && standardOutput is not null)
            return standardOutput;

        if (TryDecompressLzw(compressedData, expectedSize, leastSignificantBitFirst: true, codeSizeThresholdOffset: 0, out byte[]? legacyOutput) && legacyOutput is not null)
            return legacyOutput;

        throw new InvalidDataException("Unable to decompress TIFF LZW data.");
    }

    /// <summary>
    /// Attempts TIFF LZW decompression using the specified bit-order and code-size strategy.
    /// </summary>
    /// <param name="compressedData">The compressed input span.</param>
    /// <param name="expectedSize">The expected size of the decompressed output.</param>
    /// <param name="leastSignificantBitFirst"><see langword="true"/> to interpret codes as LSB-first; otherwise MSB-first.</param>
    /// <param name="codeSizeThresholdOffset">The offset applied when advancing to the next code width.</param>
    /// <param name="output">Receives the decompressed bytes when decoding succeeds.</param>
    /// <returns><see langword="true"/> when decoding succeeded; otherwise <see langword="false"/>.</returns>
    public static bool TryDecompressLzw(ReadOnlySpan<byte> compressedData, int expectedSize, bool leastSignificantBitFirst, int codeSizeThresholdOffset, out byte[]? output)
    {
        byte[] decodedOutput = new byte[expectedSize];
        int outputPosition = 0;

        int[] prefixes = ArrayPool<int>.Shared.Rent(TiffConstants.LzwMaxTableSize);
        byte[] suffixes = ArrayPool<byte>.Shared.Rent(TiffConstants.LzwMaxTableSize);
        int[] lengths = ArrayPool<int>.Shared.Rent(TiffConstants.LzwMaxTableSize);
        byte[] decodeBuffer = ArrayPool<byte>.Shared.Rent(TiffConstants.LzwMaxTableSize + 1);

        try
        {
            TiffLzwDecoder decoder = new(compressedData, prefixes, suffixes, lengths, decodeBuffer, TiffConstants.LzwFirstCode, leastSignificantBitFirst);

            decoder.ResetTable();

            int prevCode = -1;

            while (outputPosition < expectedSize)
            {
                if (!decoder.TryReadCode(out int code) || code == TiffConstants.LzwEoiCode)
                    break;

                if (code == TiffConstants.LzwClearCode)
                {
                    decoder.ResetTable();
                    prevCode = -1;
                    continue;
                }

                if ((uint)code >= TiffConstants.LzwMaxTableSize)
                    throw new InvalidDataException($"Invalid TIFF LZW code: {code}.");

                if (prevCode < 0)
                {
                    if (!decoder.TryDecodeString(code, out int len))
                    {
                        output = null;
                        return false;
                    }

                    int toCopy = Math.Min(len, expectedSize - outputPosition);
                    decoder.DecodeBufferSpan[..toCopy].CopyTo(decodedOutput.AsSpan(outputPosition));
                    outputPosition += toCopy;
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

                int bytesToCopy = Math.Min(decodedLength, expectedSize - outputPosition);
                decoder.DecodeBufferSpan[..bytesToCopy].CopyTo(decodedOutput.AsSpan(outputPosition));
                outputPosition += bytesToCopy;

                if (decoder.NextCode < TiffConstants.LzwMaxTableSize)
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

            if (outputPosition != expectedSize)
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
    public static void AdjustCodeSize(ref TiffLzwDecoder decoder, int codeSizeThresholdOffset)
    {
        if (decoder.CodeSize >= TiffConstants.LzwMaxCodeSize)
            return;

        int threshold = (1 << decoder.CodeSize) + codeSizeThresholdOffset;
        if (decoder.NextCode >= threshold)
            decoder.CodeSize++;
    }

    /// <summary>
    /// Determines whether a Deflate-compressed TIFF payload appears to use a zlib wrapper.
    /// </summary>
    /// <param name="compressedData">The compressed TIFF Deflate payload to inspect.</param>
    /// <returns><see langword="true"/> when the payload appears to have a zlib wrapper; otherwise <see langword="false"/>.</returns>
    public static bool UsesZlibWrapper(ReadOnlySpan<byte> compressedData)
    {
        if (compressedData.Length < 2)
            return false;

        int compressionMethodAndFlags = compressedData[0];
        int compressionInfoAndCheckBits = compressedData[1];

        if ((compressionMethodAndFlags & 0x0F) != 8)
            return false;

        if ((compressionMethodAndFlags >> 4) > 7)
            return false;

        return ((compressionMethodAndFlags << 8) + compressionInfoAndCheckBits) % 31 == 0;
    }

    /// <summary>
    /// Attempts to decompress TIFF Deflate data using either zlib-wrapped or raw Deflate input.
    /// </summary>
    /// <param name="compressedData">The compressed TIFF Deflate payload.</param>
    /// <param name="expectedSize">The expected uncompressed size in bytes.</param>
    /// <param name="usesZlibWrapper"><see langword="true"/> to decode using a zlib wrapper; otherwise raw Deflate.</param>
    /// <param name="result">Receives the decompressed bytes when decoding succeeds.</param>
    /// <returns><see langword="true"/> when decompression succeeded; otherwise <see langword="false"/>.</returns>
    public static bool TryDecompressDeflate(ReadOnlySpan<byte> compressedData, int expectedSize, bool usesZlibWrapper, out byte[]? result)
    {
        try
        {
            using MemoryStream input = new(compressedData.ToArray(), writable: false);
            using Stream inflater = usesZlibWrapper ? new ZLibStream(input, CompressionMode.Decompress) : new DeflateStream(input, CompressionMode.Decompress);
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
