using System.Buffers;
using static RedFox.Graphics2D.Tiff.TiffConstants;

namespace RedFox.Graphics2D.Tiff
{
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
        internal static byte[] CompressPackBits(ReadOnlySpan<byte> src)
        {
            // Worst case: each input byte can produce up to 2 output bytes (header + literal).
            int worstCase = Math.Max(4, src.Length * 2);
            var outBuf = ArrayPool<byte>.Shared.Rent(worstCase);
            try
            {
                int pos = 0;
                int outPos = 0;

                while (pos < src.Length)
                {
                    // Look for a run of identical bytes (min 2 to justify RLE header).
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
                        // Literal run: count consecutive non-repeating bytes.
                        int litStart = pos;
                        int litLen = 1;
                        while (litLen < 128 && pos + litLen < src.Length)
                        {
                            // Stop if the next two bytes start a run.
                            if (pos + litLen + 1 < src.Length && src[pos + litLen] == src[pos + litLen + 1])
                                break;
                            litLen++;
                        }

                        // Header byte: litLen - 1.
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
        internal static byte[] CompressLZW(ReadOnlySpan<byte> src)
        {
            if (src.Length == 0)
            {
                // Emit ClearCode + EOI with 9-bit codes using BitWriter into a small buffer.
                var emptyBuf = new byte[3];
                var smallWriter = new BitWriter(emptyBuf);
                smallWriter.Write(LzwClearCode, LzwInitialCodeSize);
                smallWriter.Write(LzwEoiCode, LzwInitialCodeSize);
                int b = smallWriter.Flush();
                var res = new byte[b];
                Array.Copy(emptyBuf, res, b);
                return res;
            }

            // Output buffer — rent from pool to reduce allocations. Use a slightly
            // generous initial size similar to the previous heuristic.
            int initialOutput = Math.Max(512, src.Length * 2 + 512);
            var output = ArrayPool<byte>.Shared.Rent(initialOutput);

            // String table: maps (prefix, suffix) → code.
            // Using a simple hash table with open addressing for fast lookups.
            int tableCapacity = 8192; // Power of two, larger than LzwMaxTableSize.
            var hashKeys = ArrayPool<long>.Shared.Rent(tableCapacity);
            var hashValues = ArrayPool<int>.Shared.Rent(tableCapacity);
            int nextCode;
            int codeSize;
            void ResetTable()
            {
                // Initialize only the active portion of the rented array.
                Array.Fill(hashKeys, -1L, 0, tableCapacity);
                nextCode = LzwFirstCode;
                codeSize = LzwInitialCodeSize;
            }

            int FindOrInsert(int prefix, byte suffix)
            {
                long key = ((long)prefix << 8) | suffix;
                int mask = tableCapacity - 1;
                int slot = (int)((uint)(key * 2654435761L) >> 19) & mask;

                while (true)
                {
                    if (hashKeys[slot] == key)
                        return hashValues[slot];   // Found — return existing code.
                    if (hashKeys[slot] == -1L)
                    {
                        // Not found — insert if table has room.
                        if (nextCode < LzwMaxTableSize)
                        {
                            hashKeys[slot] = key;
                            hashValues[slot] = nextCode++;
                        }
                        return -1; // Signal "not found".
                    }
                    slot = (slot + 1) & mask;
                }
            }

            // BitWriter will manage writing bits into the rented output buffer.
            var writer = new BitWriter(output);

            try
            {
                ResetTable();

                // Emit initial ClearCode.
                writer.Write(LzwClearCode, LzwInitialCodeSize);

                int w = src[0]; // Current string represented as its code.
                int srcPos = 1;

                while (srcPos < src.Length)
                {
                    byte k = src[srcPos++];
                    int code = FindOrInsert(w, k);

                    if (code >= 0)
                    {
                        // w + k exists in the table — extend the current string.
                        w = code;
                    }
                    else
                    {
                        // w + k is new — emit code for w.
                        writer.Write(w, codeSize);

                        // Check for code size increase (early change per TIFF 6.0).
                        if (nextCode >= (1 << codeSize) && codeSize < LzwMaxCodeSize)
                            codeSize++;

                        // Reset when table is full.
                        if (nextCode >= LzwMaxTableSize)
                        {
                            writer.Write(LzwClearCode, codeSize);
                            ResetTable();
                        }

                        w = k; // Start new string with k.
                    }
                }

                // Emit code for the final string and EOI marker, then flush writer.
                writer.Write(w, codeSize);
                writer.Write(LzwEoiCode, codeSize);

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
        /// Helper that writes MSB-first variable-width codes into a byte buffer using
        /// an internal bit-accumulator. Designed as a ref struct for stack-only use.
        /// </summary>
        private ref struct BitWriter(byte[] buffer)
        {
            private byte[] _buffer = buffer;
            private int _bytePos = 0;
            private ulong _bitBuffer = 0ul;
            private int _bitsInBuffer = 0;

            /// <summary>
            /// Writes the <paramref name="code"/> using <paramref name="codeSize"/> bits (MSB-first).
            /// </summary>
            public void Write(int code, int codeSize)
            {
                // Append bits to the buffer: shift existing bits left and OR-in the new code.
                _bitBuffer = (_bitBuffer << codeSize) | (uint)code;
                _bitsInBuffer += codeSize;

                // Flush whole bytes from the top of the bit buffer.
                while (_bitsInBuffer >= 8)
                {
                    int shift = _bitsInBuffer - 8;
                    byte b = (byte)((_bitBuffer >> shift) & 0xFFu);
                    if (_bytePos >= _buffer.Length)
                        throw new InvalidOperationException("BitWriter buffer overflow: buffer too small.");
                    _buffer[_bytePos++] = b;
                    _bitsInBuffer -= 8;
                    if (_bitsInBuffer == 0)
                    {
                        _bitBuffer = 0;
                    }
                    else
                    {
                        _bitBuffer &= ((1ul << _bitsInBuffer) - 1ul);
                    }
                }
            }

            /// <summary>
            /// Flushes any remaining partial byte and returns the total number of bytes written.
            /// </summary>
            public int Flush()
            {
                if (_bitsInBuffer > 0)
                {
                    byte b = (byte)(_bitBuffer << (8 - _bitsInBuffer));
                    if (_bytePos >= _buffer.Length)
                        throw new InvalidOperationException("BitWriter buffer overflow: buffer too small.");
                    _buffer[_bytePos++] = b;
                    _bitsInBuffer = 0;
                    _bitBuffer = 0;
                }
                return _bytePos;
            }
        }
    }
}
