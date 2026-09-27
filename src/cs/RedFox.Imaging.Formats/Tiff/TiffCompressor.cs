using System;
using System.Buffers;
using System.IO;
using System.IO.Compression;

namespace RedFox.Imaging.Formats.Tiff;

/// <summary>
/// Provides TIFF-compliant LZW and PackBits compression for image strip data.
/// LZW uses MSB-first bit packing with variable code widths (9–12 bits)
/// and early code-size increase per the TIFF 6.0 specification.
/// </summary>
internal static class TiffCompressor
{
    /// <summary>
    /// Compresses data using the PackBits (byte-oriented RLE) scheme.
    /// </summary>
    /// <param name="sourceData">The uncompressed input data.</param>
    /// <returns>A byte array containing the PackBits-compressed data.</returns>
    public static byte[] CompressPackBits(ReadOnlySpan<byte> sourceData)
    {
        int worstCase = Math.Max(4, sourceData.Length * 2);
        var outputBuffer = ArrayPool<byte>.Shared.Rent(worstCase);
        try
        {
            int pos = 0;
            int outPos = 0;

            while (pos < sourceData.Length)
            {
                if (pos + 1 < sourceData.Length && sourceData[pos] == sourceData[pos + 1])
                {
                    byte value = sourceData[pos];
                    int runLen = 2;
                    while (pos + runLen < sourceData.Length && runLen < 128 && sourceData[pos + runLen] == value)
                        runLen++;

                    // Header byte: (byte)(1 - runLen), which is -(runLen - 1) stored as sbyte.
                    outputBuffer[outPos++] = (byte)(1 - runLen);
                    outputBuffer[outPos++] = value;
                    pos += runLen;
                }
                else
                {
                    int litStart = pos;
                    int litLen = 1;
                    while (litLen < 128 && pos + litLen < sourceData.Length)
                    {
                        if (pos + litLen + 1 < sourceData.Length && sourceData[pos + litLen] == sourceData[pos + litLen + 1])
                            break;
                        litLen++;
                    }

                    outputBuffer[outPos++] = (byte)(litLen - 1);
                    sourceData.Slice(litStart, litLen).CopyTo(outputBuffer.AsSpan(outPos));
                    outPos += litLen;
                    pos += litLen;
                }
            }

            var result = new byte[outPos];
            Array.Copy(outputBuffer, result, outPos);
            return result;
        }
        finally
        {
            ArrayPool<byte>.Shared.Return(outputBuffer);
        }
    }

    /// <summary>
    /// Compresses data using TIFF LZW with MSB-first bit packing.
    /// Produces output conforming to the TIFF 6.0 LZW specification.
    /// </summary>
    /// <param name="sourceData">The uncompressed input data.</param>
    /// <returns>A byte array containing the LZW-compressed data.</returns>
    public static byte[] CompressLZW(ReadOnlySpan<byte> sourceData)
    {
        if (sourceData.Length == 0)
        {
            var emptyOutputBuffer = new byte[3];
            var smallWriter = new TiffBitWriter(emptyOutputBuffer);
            smallWriter.Write(TiffConstants.LzwClearCode, TiffConstants.LzwInitialCodeSize);
            smallWriter.Write(TiffConstants.LzwEoiCode, TiffConstants.LzwInitialCodeSize);
            int bytesWritten = smallWriter.Flush();
            var result = new byte[bytesWritten];
            Array.Copy(emptyOutputBuffer, result, bytesWritten);
            return result;
        }

        int initialOutput = Math.Max(512, sourceData.Length * 2 + 512);
        var output = ArrayPool<byte>.Shared.Rent(initialOutput);

        int tableCapacity = 8192;
        var hashKeys = ArrayPool<long>.Shared.Rent(tableCapacity);
        var hashValues = ArrayPool<int>.Shared.Rent(tableCapacity);
        int nextCode = 0;
        int codeSize = 0;

        var writer = new TiffBitWriter(output);

        try
        {
            ResetLzwTable(hashKeys, tableCapacity, ref nextCode, ref codeSize);

            writer.Write(TiffConstants.LzwClearCode, TiffConstants.LzwInitialCodeSize);

            int prefixCode = sourceData[0];
            int sourcePosition = 1;

            while (sourcePosition < sourceData.Length)
            {
                byte nextByte = sourceData[sourcePosition++];
                int code = FindOrInsertLzwCode(hashKeys, hashValues, tableCapacity, ref nextCode, prefixCode, nextByte);

                if (code >= 0)
                {
                    prefixCode = code;
                }
                else
                {
                    writer.Write(prefixCode, codeSize);

                    // TIFF LZW uses the historical Aldus off-by-one code-size transition.
                    if (nextCode >= ((1 << codeSize) - 1) && codeSize < TiffConstants.LzwMaxCodeSize)
                        codeSize++;

                    if (nextCode >= TiffConstants.LzwMaxTableSize)
                    {
                        writer.Write(TiffConstants.LzwClearCode, codeSize);
                        ResetLzwTable(hashKeys, tableCapacity, ref nextCode, ref codeSize);
                    }

                    prefixCode = nextByte;
                }
            }

            writer.Write(prefixCode, codeSize);
            writer.Write(TiffConstants.LzwEoiCode, codeSize);

            int totalBytes = writer.Flush();
            var result = new byte[totalBytes];
            Array.Copy(output, result, totalBytes);
            return result;
        }
        finally
        {
            ArrayPool<byte>.Shared.Return(output);
            ArrayPool<long>.Shared.Return(hashKeys);
            ArrayPool<int>.Shared.Return(hashValues);
        }
    }

    /// <summary>
    /// Compresses data using Deflate/ZIP compression.
    /// </summary>
    /// <param name="sourceData">The uncompressed input data.</param>
    /// <returns>A byte array containing the Deflate-compressed data.</returns>
    public static byte[] CompressDeflate(ReadOnlySpan<byte> sourceData)
    {
        using MemoryStream output = new();
        using (ZLibStream compressor = new(output, CompressionLevel.Optimal, leaveOpen: true))
        {
            compressor.Write(sourceData);
        }

        return output.ToArray();
    }

    private static void ResetLzwTable(long[] hashKeys, int tableCapacity, ref int nextCode, ref int codeSize)
    {
        Array.Fill(hashKeys, -1L, 0, tableCapacity);
        nextCode = TiffConstants.LzwFirstCode;
        codeSize = TiffConstants.LzwInitialCodeSize;
    }

    private static int FindOrInsertLzwCode(long[] hashKeys, int[] hashValues, int tableCapacity, ref int nextCode, int prefix, byte suffix)
    {
        long key = ((long)prefix << 8) | suffix;
        int mask = tableCapacity - 1;
        int slot = (int)((uint)(key * 2654435761L) >> 19) & mask;

        while (true)
        {
            if (hashKeys[slot] == key)
                return hashValues[slot];
            if (hashKeys[slot] == -1L)
            {
                if (nextCode < TiffConstants.LzwMaxTableSize)
                {
                    hashKeys[slot] = key;
                    hashValues[slot] = nextCode++;
                }
                return -1;
            }
            slot = (slot + 1) & mask;
        }
    }
}
