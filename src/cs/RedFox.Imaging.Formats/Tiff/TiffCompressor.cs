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
    /// <param name="src">The uncompressed input data.</param>
    /// <returns>A byte array containing the PackBits-compressed data.</returns>
    public static byte[] CompressPackBits(ReadOnlySpan<byte> src)
    {
        int worstCase = Math.Max(4, src.Length * 2);
        var outBuf = ArrayPool<byte>.Shared.Rent(worstCase);
        try
        {
            int pos = 0;
            int outPos = 0;

            while (pos < src.Length)
            {
                if (pos + 1 < src.Length && src[pos] == src[pos + 1])
                {
                    byte value = src[pos];
                    int runLen = 2;
                    while (pos + runLen < src.Length && runLen < 128 && src[pos + runLen] == value)
                        runLen++;

                    // Header byte: (byte)(1 - runLen), which is -(runLen - 1) stored as sbyte.
                    outBuf[outPos++] = (byte)(1 - runLen);
                    outBuf[outPos++] = value;
                    pos += runLen;
                }
                else
                {
                    int litStart = pos;
                    int litLen = 1;
                    while (litLen < 128 && pos + litLen < src.Length)
                    {
                        if (pos + litLen + 1 < src.Length && src[pos + litLen] == src[pos + litLen + 1])
                            break;
                        litLen++;
                    }

                    outBuf[outPos++] = (byte)(litLen - 1);
                    src.Slice(litStart, litLen).CopyTo(outBuf.AsSpan(outPos));
                    outPos += litLen;
                    pos += litLen;
                }
            }

            var result = new byte[outPos];
            Array.Copy(outBuf, result, outPos);
            return result;
        }
        finally
        {
            ArrayPool<byte>.Shared.Return(outBuf);
        }
    }

    /// <summary>
    /// Compresses data using TIFF LZW with MSB-first bit packing.
    /// Produces output conforming to the TIFF 6.0 LZW specification.
    /// </summary>
    /// <param name="src">The uncompressed input data.</param>
    /// <returns>A byte array containing the LZW-compressed data.</returns>
    public static byte[] CompressLZW(ReadOnlySpan<byte> src)
    {
        if (src.Length == 0)
        {
            var emptyBuf = new byte[3];
            var smallWriter = new TiffBitWriter(emptyBuf);
            smallWriter.Write(TiffConstants.LzwClearCode, TiffConstants.LzwInitialCodeSize);
            smallWriter.Write(TiffConstants.LzwEoiCode, TiffConstants.LzwInitialCodeSize);
            int b = smallWriter.Flush();
            var res = new byte[b];
            Array.Copy(emptyBuf, res, b);
            return res;
        }

        int initialOutput = Math.Max(512, src.Length * 2 + 512);
        var output = ArrayPool<byte>.Shared.Rent(initialOutput);

        int tableCapacity = 8192;
        var hashKeys = ArrayPool<long>.Shared.Rent(tableCapacity);
        var hashValues = ArrayPool<int>.Shared.Rent(tableCapacity);
        int nextCode;
        int codeSize;

        void ResetTable()
        {
            Array.Fill(hashKeys, -1L, 0, tableCapacity);
            nextCode = TiffConstants.LzwFirstCode;
            codeSize = TiffConstants.LzwInitialCodeSize;
        }

        int FindOrInsert(int prefix, byte suffix)
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

        var writer = new TiffBitWriter(output);

        try
        {
            ResetTable();

            writer.Write(TiffConstants.LzwClearCode, TiffConstants.LzwInitialCodeSize);

            int w = src[0];
            int srcPos = 1;

            while (srcPos < src.Length)
            {
                byte k = src[srcPos++];
                int code = FindOrInsert(w, k);

                if (code >= 0)
                {
                    w = code;
                }
                else
                {
                    writer.Write(w, codeSize);

                    // TIFF LZW uses the historical Aldus off-by-one code-size transition.
                    if (nextCode >= ((1 << codeSize) - 1) && codeSize < TiffConstants.LzwMaxCodeSize)
                        codeSize++;

                    if (nextCode >= TiffConstants.LzwMaxTableSize)
                    {
                        writer.Write(TiffConstants.LzwClearCode, codeSize);
                        ResetTable();
                    }

                    w = k;
                }
            }

            writer.Write(w, codeSize);
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
    /// <param name="src">The uncompressed input data.</param>
    /// <returns>A byte array containing the Deflate-compressed data.</returns>
    public static byte[] CompressDeflate(ReadOnlySpan<byte> src)
    {
        using MemoryStream output = new();
        using (ZLibStream compressor = new(output, CompressionLevel.Optimal, leaveOpen: true))
        {
            compressor.Write(src);
        }

        return output.ToArray();
    }
}
