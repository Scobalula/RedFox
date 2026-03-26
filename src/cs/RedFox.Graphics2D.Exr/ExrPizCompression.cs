using System.Buffers.Binary;

namespace RedFox.Graphics2D.Exr
{
    /// <summary>
    /// Reconstructs PIZ-compressed scanline blocks.
    /// </summary>
    public static class ExrPizCompression
    {
        private const int HufEncSize = (1 << 16) + 1;
        private const int ShortZeroCodeRun = 59;
        private const int LongZeroCodeRun = 63;
        private const int ShortestLongRun = 6;
        private const int LongestCodeLength = 58;
        private const int BitmapSize = 1 << 13;
        private const int UShortRange = 1 << 16;

        /// <summary>
        /// Decodes a PIZ-compressed block into the standard channel-major byte layout.
        /// </summary>
        /// <param name="packedData">The compressed block data.</param>
        /// <param name="channels">The channel list from the EXR header.</param>
        /// <param name="width">The image width in pixels.</param>
        /// <param name="rowsInBlock">The number of scanlines in the block.</param>
        /// <param name="expectedSize">The expected decompressed byte count.</param>
        /// <returns>The decoded channel-major byte array.</returns>
        public static byte[] Decode(ReadOnlySpan<byte> packedData, IReadOnlyList<ExrChannel> channels, int width, int rowsInBlock, int expectedSize)
        {
            if ((expectedSize & 1) != 0)
                throw new InvalidDataException("PIZ EXR blocks must expand to an even byte count.");

            int offset = 0;
            ushort minNonZero = ReadUInt16(packedData, ref offset);
            ushort maxNonZero = ReadUInt16(packedData, ref offset);

            if (maxNonZero >= BitmapSize)
                throw new InvalidDataException("PIZ EXR block range bitmap was invalid.");

            var bitmap = new byte[BitmapSize];
            if (minNonZero <= maxNonZero)
            {
                int bitmapLength = maxNonZero - minNonZero + 1;
                ReadBytes(packedData, ref offset, bitmapLength).CopyTo(bitmap.AsSpan(minNonZero, bitmapLength));
            }

            ushort[] lut = BuildReverseLut(bitmap);
            ushort maxValue = lut[ushort.MaxValue];
            uint huffmanSize = ReadUInt32(packedData, ref offset);
            ReadOnlySpan<byte> huffmanData = ReadBytes(packedData, ref offset, checked((int)huffmanSize));

            ushort[] words = DecodeHuffman(huffmanData, expectedSize / 2);

            int wordOffset = 0;
            foreach (var channel in channels)
            {
                int wordsPerPixel = ExrFileLayout.GetBytesPerSample(channel.PixelType) / 2;
                int planeWordCount = checked(width * rowsInBlock * wordsPerPixel);

                for (int component = 0; component < wordsPerPixel; component++)
                    DecodeWavelet(words, wordOffset + component, width, wordsPerPixel, rowsInBlock, wordsPerPixel * width, maxValue);

                wordOffset += planeWordCount;
            }

            ApplyLut(lut, words);

            var output = new byte[expectedSize];
            for (int index = 0; index < words.Length; index++)
                BinaryPrimitives.WriteUInt16LittleEndian(output.AsSpan(index * sizeof(ushort), sizeof(ushort)), words[index]);

            if (offset != packedData.Length)
                throw new InvalidDataException("PIZ EXR block contained trailing bytes.");

            return output;
        }

        /// <summary>
        /// Builds the reverse PIZ lookup table from the serialized value bitmap.
        /// </summary>
        private static ushort[] BuildReverseLut(byte[] bitmap)
        {
            var lut = new ushort[UShortRange];
            int count = 0;

            for (int value = 0; value < UShortRange; value++)
            {
                if (value == 0 || (bitmap[value >> 3] & (1 << (value & 7))) != 0)
                    lut[count++] = (ushort)value;
            }

            int maxIndex = count - 1;
            while (count < lut.Length)
                lut[count++] = 0;

            lut[ushort.MaxValue] = (ushort)maxIndex;
            return lut;
        }

        /// <summary>
        /// Applies the reverse PIZ value lookup to the decoded word stream.
        /// </summary>
        private static void ApplyLut(ushort[] lut, ushort[] data)
        {
            for (int index = 0; index < data.Length; index++)
                data[index] = lut[data[index]];
        }

        /// <summary>
        /// Decodes the Huffman payload embedded inside a PIZ block.
        /// </summary>
        private static ushort[] DecodeHuffman(ReadOnlySpan<byte> compressedData, int expectedWordCount)
        {
            if (compressedData.Length < 20)
                throw new InvalidDataException("PIZ EXR block did not contain a complete Huffman payload.");

            int offset = 0;
            int minSymbol = checked((int)ReadUInt32(compressedData, ref offset));
            int maxSymbol = checked((int)ReadUInt32(compressedData, ref offset));
            int tableLength = checked((int)ReadUInt32(compressedData, ref offset));
            int bitLength = checked((int)ReadUInt32(compressedData, ref offset));
            _ = ReadUInt32(compressedData, ref offset);

            if (minSymbol < 0 || maxSymbol >= HufEncSize || minSymbol > maxSymbol)
                throw new InvalidDataException("PIZ EXR block contained an invalid Huffman symbol range.");

            ReadOnlySpan<byte> tableBytes = ReadBytes(compressedData, ref offset, tableLength);
            int dataByteLength = (bitLength + 7) / 8;
            ReadOnlySpan<byte> dataBytes = ReadBytes(compressedData, ref offset, dataByteLength);

            var encodingTable = new ulong[HufEncSize];
            UnpackEncodingTable(tableBytes, minSymbol, maxSymbol, encodingTable);

            int[] activeLengths = BuildActiveLengths(encodingTable, minSymbol, maxSymbol, out ulong[] minCodes, out int[][] symbolsByLength);
            var decoded = new ushort[expectedWordCount];
            var reader = new BitReader(dataBytes, bitLength);
            int outputIndex = 0;

            while (outputIndex < decoded.Length)
            {
                bool matched = false;

                foreach (int codeLength in activeLengths)
                {
                    if (!reader.TryPeekBits(codeLength, out ulong code))
                        continue;

                    ulong minCode = minCodes[codeLength];
                    int[] symbols = symbolsByLength[codeLength];
                    if (code < minCode)
                        continue;

                    ulong codeIndex = code - minCode;
                    if (codeIndex >= (ulong)symbols.Length)
                        continue;

                    reader.SkipBits(codeLength);
                    int symbol = symbols[checked((int)codeIndex)];

                    if (symbol == maxSymbol)
                    {
                        if (outputIndex == 0)
                            throw new InvalidDataException("PIZ EXR block contained an invalid Huffman run-length marker.");

                        int runLength = checked((int)reader.ReadBits(8));
                        if (runLength == 0 || outputIndex + runLength > decoded.Length)
                            throw new InvalidDataException("PIZ EXR block contained an invalid Huffman run length.");

                        ushort repeatedValue = decoded[outputIndex - 1];
                        decoded.AsSpan(outputIndex, runLength).Fill(repeatedValue);
                        outputIndex += runLength;
                    }
                    else
                    {
                        decoded[outputIndex++] = (ushort)symbol;
                    }

                    matched = true;
                    break;
                }

                if (!matched)
                    throw new InvalidDataException("PIZ EXR block contained an invalid Huffman code.");
            }

            if (reader.BitsRemaining != 0 || offset != compressedData.Length)
                throw new InvalidDataException("PIZ EXR block Huffman stream did not terminate cleanly.");

            return decoded;
        }

        /// <summary>
        /// Unpacks the serialized Huffman code lengths and rebuilds canonical codes.
        /// </summary>
        private static void UnpackEncodingTable(ReadOnlySpan<byte> tableBytes, int minSymbol, int maxSymbol, ulong[] encodingTable)
        {
            var reader = new BitReader(tableBytes, checked(tableBytes.Length * 8));

            for (int symbol = minSymbol; symbol <= maxSymbol; symbol++)
            {
                ulong codeLength = reader.ReadBits(6);

                if (codeLength == LongZeroCodeRun)
                {
                    ulong zeroRun = reader.ReadBits(8) + ShortestLongRun;
                    if (symbol + (int)zeroRun > maxSymbol + 1)
                        throw new InvalidDataException("PIZ EXR block Huffman table overran the symbol range.");

                    while (zeroRun-- > 0)
                        encodingTable[symbol++] = 0;

                    symbol--;
                    continue;
                }

                if (codeLength >= ShortZeroCodeRun)
                {
                    ulong zeroRun = codeLength - ShortZeroCodeRun + 2;
                    if (symbol + (int)zeroRun > maxSymbol + 1)
                        throw new InvalidDataException("PIZ EXR block Huffman table overran the symbol range.");

                    while (zeroRun-- > 0)
                        encodingTable[symbol++] = 0;

                    symbol--;
                    continue;
                }

                encodingTable[symbol] = codeLength;
            }

            CanonicalizeEncodingTable(encodingTable);
        }

        /// <summary>
        /// Converts stored code lengths into canonical Huffman code pairs.
        /// </summary>
        private static void CanonicalizeEncodingTable(ulong[] encodingTable)
        {
            var counts = new ulong[LongestCodeLength + 1];
            for (int index = 0; index < encodingTable.Length; index++)
                counts[encodingTable[index]]++;

            ulong code = 0;
            for (int length = LongestCodeLength; length > 0; length--)
            {
                ulong nextCode = (code + counts[length]) >> 1;
                counts[length] = code;
                code = nextCode;
            }

            for (int index = 0; index < encodingTable.Length; index++)
            {
                ulong length = encodingTable[index];
                if (length > 0)
                    encodingTable[index] = length | (counts[length]++ << 6);
            }
        }

        /// <summary>
        /// Builds contiguous per-length symbol tables for canonical Huffman decoding.
        /// </summary>
        private static int[] BuildActiveLengths(ulong[] encodingTable, int minSymbol, int maxSymbol, out ulong[] minCodes, out int[][] symbolsByLength)
        {
            minCodes = new ulong[LongestCodeLength + 1];
            symbolsByLength = new int[LongestCodeLength + 1][];
            var lengthLists = new List<int>[LongestCodeLength + 1];

            for (int index = 0; index < minCodes.Length; index++)
                minCodes[index] = ulong.MaxValue;

            var activeLengths = new List<int>();
            for (int symbol = minSymbol; symbol <= maxSymbol; symbol++)
            {
                int codeLength = checked((int)(encodingTable[symbol] & 63));
                if (codeLength == 0)
                    continue;

                ulong code = encodingTable[symbol] >> 6;
                if (lengthLists[codeLength] is null)
                {
                    lengthLists[codeLength] = new List<int>();
                    minCodes[codeLength] = code;
                    activeLengths.Add(codeLength);
                }

                lengthLists[codeLength]!.Add(symbol);
            }

            activeLengths.Sort();
            foreach (int codeLength in activeLengths)
                symbolsByLength[codeLength] = lengthLists[codeLength]!.ToArray();

            return activeLengths.ToArray();
        }

        /// <summary>
        /// Reverses the 2D Haar transform used by PIZ.
        /// </summary>
        private static void DecodeWavelet(ushort[] data, int baseIndex, int nx, int ox, int ny, int oy, ushort maxValue)
        {
            bool use14BitTransform = maxValue < (1 << 14);
            int n = Math.Min(nx, ny);
            int p = 1;

            while (p <= n)
                p <<= 1;

            p >>= 1;
            int p2 = p;
            p >>= 1;

            while (p >= 1)
            {
                int oy1 = oy * p;
                int oy2 = oy * p2;
                int ox1 = ox * p;
                int ox2 = ox * p2;
                int py = baseIndex;
                int ey = baseIndex + oy * (ny - p2);

                for (; py <= ey; py += oy2)
                {
                    int px = py;
                    int ex = py + ox * (nx - p2);

                    for (; px <= ex; px += ox2)
                    {
                        int p01 = px + ox1;
                        int p10 = px + oy1;
                        int p11 = p10 + ox1;

                        if (use14BitTransform)
                        {
                            Decode14BitQuad(data, px, p01, p10, p11);
                        }
                        else
                        {
                            Decode16Bit(data[px], data[p10], out ushort i00, out ushort i10);
                            Decode16Bit(data[p01], data[p11], out ushort i01, out ushort i11);
                            Decode16Bit(i00, i01, out data[px], out data[p01]);
                            Decode16Bit(i10, i11, out data[p10], out data[p11]);
                        }
                    }

                    if ((nx & p) != 0)
                    {
                        int p10 = px + oy1;
                        if (use14BitTransform)
                            Decode14Bit(data[px], data[p10], out data[px], out data[p10]);
                        else
                            Decode16Bit(data[px], data[p10], out data[px], out data[p10]);
                    }
                }

                if ((ny & p) != 0)
                {
                    int px = py;
                    int ex = py + ox * (nx - p2);

                    for (; px <= ex; px += ox2)
                    {
                        int p01 = px + ox1;
                        if (use14BitTransform)
                            Decode14Bit(data[px], data[p01], out data[px], out data[p01]);
                        else
                            Decode16Bit(data[px], data[p01], out data[px], out data[p01]);
                    }
                }

                p2 = p;
                p >>= 1;
            }
        }

        /// <summary>
        /// Reverses the 14-bit wavelet transform for a 2x2 group.
        /// </summary>
        private static void Decode14BitQuad(ushort[] data, int px, int p01, int p10, int p11)
        {
            short a = unchecked((short)data[px]);
            short b = unchecked((short)data[p10]);
            short c = unchecked((short)data[p01]);
            short d = unchecked((short)data[p11]);

            int i00 = a + (b & 1) + (b >> 1);
            int i10 = i00 - b;
            int i01 = c + (d & 1) + (d >> 1);
            int i11 = i01 - d;

            int ai = i00 + (i01 & 1) + (i01 >> 1);
            int bi = ai - i01;
            int ci = i10 + (i11 & 1) + (i11 >> 1);
            int di = ci - i11;

            data[px] = unchecked((ushort)ai);
            data[p01] = unchecked((ushort)bi);
            data[p10] = unchecked((ushort)ci);
            data[p11] = unchecked((ushort)di);
        }

        /// <summary>
        /// Reverses the 14-bit wavelet transform for a 1D pair.
        /// </summary>
        private static void Decode14Bit(ushort low, ushort high, out ushort a, out ushort b)
        {
            short lowSigned = unchecked((short)low);
            short highSigned = unchecked((short)high);
            int ai = lowSigned + (highSigned & 1) + (highSigned >> 1);
            a = unchecked((ushort)(short)ai);
            b = unchecked((ushort)(short)(ai - highSigned));
        }

        /// <summary>
        /// Reverses the 16-bit modulo wavelet transform for a 1D pair.
        /// </summary>
        private static void Decode16Bit(ushort low, ushort high, out ushort a, out ushort b)
        {
            const int ModMask = (1 << 16) - 1;
            const int AOffset = 1 << 15;

            int m = low;
            int d = high;
            int bb = (m - (d >> 1)) & ModMask;
            int aa = (d + bb - AOffset) & ModMask;
            a = (ushort)aa;
            b = (ushort)bb;
        }

        /// <summary>
        /// Reads a 16-bit little-endian integer.
        /// </summary>
        private static ushort ReadUInt16(ReadOnlySpan<byte> source, ref int offset)
        {
            ushort value = BinaryPrimitives.ReadUInt16LittleEndian(ReadBytes(source, ref offset, sizeof(ushort)));
            return value;
        }

        /// <summary>
        /// Reads a 32-bit little-endian integer.
        /// </summary>
        private static uint ReadUInt32(ReadOnlySpan<byte> source, ref int offset)
        {
            uint value = BinaryPrimitives.ReadUInt32LittleEndian(ReadBytes(source, ref offset, sizeof(uint)));
            return value;
        }

        /// <summary>
        /// Reads a fixed byte span from the compressed payload.
        /// </summary>
        private static ReadOnlySpan<byte> ReadBytes(ReadOnlySpan<byte> source, ref int offset, int count)
        {
            if (count < 0 || offset > source.Length - count)
                throw new InvalidDataException("Unexpected end of PIZ EXR data while reading a field.");

            ReadOnlySpan<byte> result = source.Slice(offset, count);
            offset += count;
            return result;
        }

        /// <summary>
        /// Maintains a forward-only bitstream cursor over an MSB-first payload.
        /// </summary>
        private ref struct BitReader
        {
            private readonly ReadOnlySpan<byte> data;
            private readonly int totalBits;
            private int byteOffset;
            private int bitsConsumed;
            private ulong buffer;
            private int bufferedBits;

            /// <summary>
            /// Creates a reader over the supplied byte payload.
            /// </summary>
            public BitReader(ReadOnlySpan<byte> data, int totalBits)
            {
                this.data = data;
                this.totalBits = totalBits;
                byteOffset = 0;
                bitsConsumed = 0;
                buffer = 0;
                bufferedBits = 0;
            }

            /// <summary>
            /// Gets the number of bits remaining in the logical payload.
            /// </summary>
            public int BitsRemaining => totalBits - bitsConsumed;

            /// <summary>
            /// Reads a fixed number of bits from the stream.
            /// </summary>
            public ulong ReadBits(int bitCount)
            {
                if (!TryPeekBits(bitCount, out ulong value))
                    throw new InvalidDataException("Unexpected end of Huffman data.");

                SkipBits(bitCount);
                return value;
            }

            /// <summary>
            /// Peeks ahead without consuming bits.
            /// </summary>
            public bool TryPeekBits(int bitCount, out ulong value)
            {
                if (bitCount < 0 || bitCount > 58)
                    throw new ArgumentOutOfRangeException(nameof(bitCount));
                if (bitCount > BitsRemaining)
                {
                    value = 0;
                    return false;
                }

                EnsureBits(bitCount);
                ulong mask = (1UL << bitCount) - 1;
                value = (buffer >> (bufferedBits - bitCount)) & mask;
                return true;
            }

            /// <summary>
            /// Consumes a fixed number of bits that were previously peeked.
            /// </summary>
            public void SkipBits(int bitCount)
            {
                EnsureBits(bitCount);
                bufferedBits -= bitCount;
                bitsConsumed += bitCount;
                if (bufferedBits == 0)
                    buffer = 0;
                else
                    buffer &= (1UL << bufferedBits) - 1;
            }

            /// <summary>
            /// Ensures the local buffer holds at least the requested number of bits.
            /// </summary>
            private void EnsureBits(int bitCount)
            {
                while (bufferedBits < bitCount)
                {
                    int streamBitsRead = bitsConsumed + bufferedBits;
                    int streamBitsRemaining = totalBits - streamBitsRead;
                    if (streamBitsRemaining <= 0 || byteOffset >= data.Length)
                        throw new InvalidDataException("Unexpected end of Huffman data.");

                    int bitsToAppend = Math.Min(8, streamBitsRemaining);
                    byte nextByte = data[byteOffset++];
                    if (bitsToAppend < 8)
                        nextByte = (byte)(nextByte >> (8 - bitsToAppend));

                    buffer = (buffer << bitsToAppend) | nextByte;
                    bufferedBits += bitsToAppend;
                }
            }
        }
    }
}
