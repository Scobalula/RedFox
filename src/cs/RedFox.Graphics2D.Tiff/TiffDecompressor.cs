using System.Buffers;
using static RedFox.Graphics2D.Tiff.TiffConstants;

namespace RedFox.Graphics2D.Tiff
{
    /// <summary>
    /// Provides decompression methods for TIFF compression schemes:
    /// LZW (Lempel-Ziv-Welch, MSB-first) and PackBits (byte-oriented RLE).
    /// </summary>
    internal static class TiffDecompressor
    {
        /// <summary>
        /// Decompresses PackBits (byte-oriented run-length encoding) data.
        /// </summary>
        /// <param name="src">The compressed input data.</param>
        /// <param name="expectedSize">The expected uncompressed output size in bytes.</param>
        /// <returns>A byte array containing the decompressed data.</returns>
        internal static byte[] DecompressPackBits(ReadOnlySpan<byte> src, int expectedSize)
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
        /// Helper type that encapsulates LZW bitstream reading and string-table operations.
        /// The decoder holds references to the working arrays used by the algorithm and
        /// exposes methods to read codes and reconstruct strings into an internal buffer.
        /// </summary>
        private ref struct LzwDecoder
        {
            private readonly ReadOnlySpan<byte> _src;
            private int _bytePos;
            private uint _bitBuffer;
            private int _bitsInBuffer;
            private readonly int _totalBytes;
            private readonly int[] _prefixes;
            private readonly byte[] _suffixes;
            private readonly int[] _lengths;
            private readonly byte[] _decodeBuffer;

            /// <summary>
            /// Gets or sets the next available string-table code index.
            /// </summary>
            public int NextCode { get; set; }

            /// <summary>
            /// Gets or sets the current code bit width.
            /// </summary>
            public int CodeSize { get; set; }

            /// <summary>
            /// Exposes the internal decode buffer as a span for efficient copying.
            /// </summary>
            public Span<byte> DecodeBufferSpan => _decodeBuffer;

            /// <summary>
            /// Initializes a new decoder instance bound to the provided input span and working arrays.
            /// </summary>
            /// <param name="src">Compressed input span.</param>
            /// <param name="prefixes">String-table prefix array (shared).</param>
            /// <param name="suffixes">String-table suffix array (shared).</param>
            /// <param name="lengths">String-table lengths array (shared).</param>
            /// <param name="decodeBuffer">Temporary buffer used to reconstruct strings.</param>
            public LzwDecoder(ReadOnlySpan<byte> src, int[] prefixes, byte[] suffixes, int[] lengths, byte[] decodeBuffer)
            {
                _src = src;
                _bytePos = 0;
                _bitBuffer = 0;
                _bitsInBuffer = 0;
                _totalBytes = src.Length;
                _prefixes = prefixes;
                _suffixes = suffixes;
                _lengths = lengths;
                _decodeBuffer = decodeBuffer;
                NextCode = LzwFirstCode;
                CodeSize = LzwInitialCodeSize;
            }

            /// <summary>
            /// Resets the string table to the initial state containing single-byte entries 0..255
            /// and sets the next code and code-size to their initial values.
            /// </summary>
            public void ResetTable()
            {
                for (int i = 0; i < 256; i++)
                {
                    _prefixes[i] = -1;
                    _suffixes[i] = (byte)i;
                    _lengths[i] = 1;
                }
                NextCode = LzwFirstCode;
                CodeSize = LzwInitialCodeSize;
            }

            /// <summary>
            /// Reads the next code from the bitstream using MSB-first (big-endian) packing.
            /// Returns <c>LzwEoiCode</c> if there are insufficient bits remaining.
            /// </summary>
            /// <returns>The next code value or <c>LzwEoiCode</c> when the stream ends.</returns>
            public int ReadCode()
            {
                // Calculate remaining bits available in the stream (including bits in buffer).
                int remainingBits = (_totalBytes - _bytePos) * 8 + _bitsInBuffer;
                if (remainingBits < CodeSize)
                    return LzwEoiCode;

                // Ensure we have at least CodeSize bits in the buffer.
                while (_bitsInBuffer < CodeSize)
                {
                    _bitBuffer = (_bitBuffer << 8) | _src[_bytePos++];
                    _bitsInBuffer += 8;
                }

                int shift = _bitsInBuffer - CodeSize;
                uint mask = (uint)((1 << CodeSize) - 1);
                int code = (int)((_bitBuffer >> shift) & mask);

                // Remove the consumed bits from the buffer, keeping the lower bits.
                _bitsInBuffer -= CodeSize;
                if (_bitsInBuffer == 0)
                {
                    _bitBuffer = 0;
                }
                else
                {
                    _bitBuffer &= (uint)((1 << _bitsInBuffer) - 1);
                }

                return code;
            }

            /// <summary>
            /// Reconstructs the byte sequence represented by <paramref name="code"/> into
            /// the internal decode buffer and returns its length.
            /// </summary>
            /// <param name="code">The string-table code to decode.</param>
            /// <returns>The length of the reconstructed string.</returns>
            public int DecodeString(int code)
            {
                int len = _lengths[code];
                int pos = len - 1;
                int c = code;
                while (c >= LzwFirstCode)
                {
                    _decodeBuffer[pos--] = _suffixes[c];
                    c = _prefixes[c];
                }
                _decodeBuffer[pos] = _suffixes[c];
                return len;
            }
        }

        /// <summary>
        /// Decompresses TIFF LZW data using MSB-first bit packing and variable code widths (9–12 bits).
        /// Conforms to the TIFF 6.0 specification for LZW decompression with early code-size increase.
        /// </summary>
        /// <param name="src">The compressed input data.</param>
        /// <param name="expectedSize">The expected uncompressed output size in bytes.</param>
        /// <returns>A byte array containing the decompressed data.</returns>
        /// <summary>
        /// Decompresses TIFF LZW data using MSB-first bit packing and variable code widths (9–12 bits).
        /// This implementation follows the TIFF 6.0 LZW variant which expects a Clear code
        /// at the start of the stream and performs the early code-size increase behavior.
        /// </summary>
        /// <param name="src">The compressed input data span.</param>
        /// <param name="expectedSize">The expected size of the decompressed output in bytes.</param>
        /// <returns>A newly allocated byte array containing the decompressed data.</returns>
        internal static byte[] DecompressLZW(ReadOnlySpan<byte> src, int expectedSize)
        {
            return DecompressLZWCore(src, expectedSize);
        }

        /// <summary>
        /// Core LZW decompression operating on a <see cref="ReadOnlySpan{Byte}"/>.
        /// The method implements the decode loop, maintains the string table and handles Clear/EOI codes.
        /// </summary>
        /// <param name="src">The compressed input span.</param>
        /// <param name="expectedSize">The expected size of the decompressed output.</param>
        /// <returns>A newly allocated byte array containing the decompressed data.</returns>
        private static byte[] DecompressLZWCore(ReadOnlySpan<byte> src, int expectedSize)
        {
            var output = new byte[expectedSize];
            int dstPos = 0;

            // String table: each entry stores (prefix index, suffix byte, total length).
            // Rent working arrays from the shared ArrayPool to reduce GC pressure for
            // repeated decompression operations.
            var prefixes = ArrayPool<int>.Shared.Rent(LzwMaxTableSize);
            var suffixes = ArrayPool<byte>.Shared.Rent(LzwMaxTableSize);
            var lengths = ArrayPool<int>.Shared.Rent(LzwMaxTableSize);
            var decodeBuffer = ArrayPool<byte>.Shared.Rent(LzwMaxTableSize);

            try
            {
                // Use a helper decoder instance to encapsulate bit-reading and
                // string-table operations. This avoids repeated parameter passing
                // and keeps the hot decode loop compact.
                var decoder = new LzwDecoder(src, prefixes, suffixes, lengths, decodeBuffer);

                decoder.ResetTable();

            // First code must be ClearCode per TIFF spec.
            int prevCode = decoder.ReadCode();
            if (prevCode == LzwClearCode)
            {
                decoder.ResetTable();
                prevCode = decoder.ReadCode();
            }

            if (prevCode == LzwEoiCode)
                return output;

            // Output the first code.
            {
                int len = decoder.DecodeString(prevCode);
                decoder.DecodeBufferSpan.Slice(0, len).CopyTo(output.AsSpan(dstPos));
                dstPos += len;
            }

            while (dstPos < expectedSize)
            {
                int code = decoder.ReadCode();

                if (code == LzwEoiCode)
                    break;

                if (code == LzwClearCode)
                {
                    decoder.ResetTable();
                    prevCode = decoder.ReadCode();
                    if (prevCode == LzwEoiCode)
                        break;
                    int len = decoder.DecodeString(prevCode);
                    int toCopy = Math.Min(len, expectedSize - dstPos);
                    decoder.DecodeBufferSpan.Slice(0, toCopy).CopyTo(output.AsSpan(dstPos));
                    dstPos += toCopy;
                    continue;
                }

                byte firstByte;

                if (code < decoder.NextCode)
                {
                    // Code already exists in the string table.
                    int len = decoder.DecodeString(code);
                    firstByte = decoder.DecodeBufferSpan[0];
                    int toCopy = Math.Min(len, expectedSize - dstPos);
                    decoder.DecodeBufferSpan.Slice(0, toCopy).CopyTo(output.AsSpan(dstPos));
                    dstPos += toCopy;
                }
                else
                {
                    // KwKwK special case: code not yet in the table.
                    int len = decoder.DecodeString(prevCode);
                    firstByte = decoder.DecodeBufferSpan[0];
                    decoder.DecodeBufferSpan[len] = firstByte;
                    len++;
                    int toCopy = Math.Min(len, expectedSize - dstPos);
                    decoder.DecodeBufferSpan.Slice(0, toCopy).CopyTo(output.AsSpan(dstPos));
                    dstPos += toCopy;
                }

                // Add new entry to the string table.
                if (decoder.NextCode < LzwMaxTableSize)
                {
                    prefixes[decoder.NextCode] = prevCode;
                    suffixes[decoder.NextCode] = firstByte;
                    lengths[decoder.NextCode] = lengths[prevCode] + 1;
                    decoder.NextCode++;

                    // TIFF 6.0 early code-size increase: bump when the next code
                    // to be assigned would require more bits than the current size.
                    if (decoder.NextCode >= (1 << decoder.CodeSize) && decoder.CodeSize < LzwMaxCodeSize)
                        decoder.CodeSize++;
                }

                prevCode = code;
            }

                return output;
            }
            finally
            {
                ArrayPool<int>.Shared.Return(prefixes);
                ArrayPool<byte>.Shared.Return(suffixes);
                ArrayPool<int>.Shared.Return(lengths);
                ArrayPool<byte>.Shared.Return(decodeBuffer);
            }
        }
    }
}
