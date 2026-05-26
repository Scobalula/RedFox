namespace RedFox.Compression.LZO;

/// <summary>
/// Provides LZO1X compression and decompression using the stream format accepted by the Linux kernel LZO decoder.
/// </summary>
public sealed class LzoCodec : CompressionCodec
{
    private const int EndMarker = 17;
    private const int FirstLiteralOffset = 17;
    private const int HashBits = 15;
    private const int HashSize = 1 << HashBits;
    private const int LiteralRunBase = 15;
    private const int MaxFirstLiteralRun = 238;
    private const int MaxM3Distance = 16 * 1024;
    private const int MaxM4Distance = 48 * 1024 - 1;
    private const int MinMatchLength = 3;
    private const int M2LargeDistanceOffset = 2048;
    private const int MinZeroRunLength = 4;
    private const int M3Marker = 32;
    private const int M4Marker = 16;
    private const int NotFound = -1;
    private const int RleDistanceMask = 0xFFFC;
    private const int StateMask = 0x03;
    private const int StateResetMask = 0xFC;
    private const int VersionMarker = 17;

    /// <inheritdoc/>
    public override CompressionCodecFlags Flags => CompressionCodecFlags.None;

    /// <inheritdoc/>
    public override int Compress(ReadOnlySpan<byte> source, Span<byte> destination)
    {
        int destinationIndex = 0;

        if (source.Length == 0)
        {
            WriteEndMarker(destination, ref destinationIndex);
            return destinationIndex;
        }

        int[] matchTable = new int[HashSize];
        Array.Fill(matchTable, NotFound);

        int sourceIndex = 0;
        int literalStart = 0;
        int lastStateIndex = NotFound;
        bool wroteInstruction = false;

        while (sourceIndex <= source.Length - MinMatchLength)
        {
            int matchKey = GetHash(source, sourceIndex);
            int matchIndex = matchTable[matchKey];
            matchTable[matchKey] = sourceIndex;

            if (matchIndex >= 0 && sourceIndex - matchIndex <= MaxM4Distance && HasMatch(source, matchIndex, sourceIndex))
            {
                int matchLength = CountMatchLength(source, matchIndex, sourceIndex);
                int matchDistance = sourceIndex - matchIndex;
                int matchStart = sourceIndex;

                EmitLiterals(source[literalStart..sourceIndex], destination, ref destinationIndex, ref wroteInstruction, ref lastStateIndex);
                EmitMatch(matchLength, matchDistance, destination, ref destinationIndex, ref lastStateIndex);

                sourceIndex += matchLength;
                literalStart = sourceIndex;

                int tableIndex = matchStart + 1;
                while (tableIndex < sourceIndex && tableIndex <= source.Length - MinMatchLength)
                {
                    matchTable[GetHash(source, tableIndex)] = tableIndex;
                    tableIndex++;
                }
            }
            else
            {
                sourceIndex++;
            }
        }

        EmitLiterals(source[literalStart..], destination, ref destinationIndex, ref wroteInstruction, ref lastStateIndex);
        WriteEndMarker(destination, ref destinationIndex);

        return destinationIndex;
    }

    /// <inheritdoc/>
    public override int Compress(ReadOnlySpan<byte> source, Span<byte> destination, ReadOnlySpan<byte> dictionary)
    {
        throw new NotSupportedException("LzoCodec does not support dictionaries.");
    }

    /// <inheritdoc/>
    public override int Decompress(ReadOnlySpan<byte> source, Span<byte> destination)
    {
        if (source.Length < 3)
        {
            throw new CompressionException("LZO input is too short to contain an end marker.");
        }

        int sourceIndex = 0;
        int destinationIndex = 0;
        int state = 0;
        int version = 0;

        if (source.Length >= 5 && source[0] == VersionMarker)
        {
            version = source[1];
            if (version > 1)
            {
                throw new CompressionException($"Unsupported LZO bitstream version: {version}.");
            }

            sourceIndex = 2;
        }

        if (source[sourceIndex] > FirstLiteralOffset)
        {
            int literalLength = source[sourceIndex++] - FirstLiteralOffset;
            CopyLiterals(source, ref sourceIndex, destination, ref destinationIndex, literalLength);
            state = literalLength < 4 ? literalLength : 4;
        }

        while (true)
        {
            EnsureInput(source, sourceIndex, 1);
            int token = source[sourceIndex++];

            if (token < 16)
            {
                if (state == 0)
                {
                    int literalLength = ReadLength(source, ref sourceIndex, token, LiteralRunBase, MinMatchLength);
                    CopyLiterals(source, ref sourceIndex, destination, ref destinationIndex, literalLength);
                    state = 4;
                    continue;
                }

                EnsureInput(source, sourceIndex, 1);
                int previousState = state;
                int distance = (source[sourceIndex++] << 2) + (token >> 2) + 1;
                state = token & StateMask;

                if (previousState == 4)
                {
                    distance += M2LargeDistanceOffset;
                    CopyMatch(destination, ref destinationIndex, distance, MinMatchLength);
                }
                else
                {
                    CopyMatch(destination, ref destinationIndex, distance, 2);
                }

                CopyLiterals(source, ref sourceIndex, destination, ref destinationIndex, state);
                continue;
            }

            if (token >= 64)
            {
                EnsureInput(source, sourceIndex, 1);
                int distance = (source[sourceIndex++] << 3) + ((token >> 2) & 0x07) + 1;
                int matchLength = (token >> 5) + 1;
                state = token & StateMask;
                CopyMatch(destination, ref destinationIndex, distance, matchLength);
                CopyLiterals(source, ref sourceIndex, destination, ref destinationIndex, state);
                continue;
            }

            if (token >= 32)
            {
                int matchLength = ReadLength(source, ref sourceIndex, token & 0x1F, 31, 2);
                int operand = ReadLittleEndianUInt16(source, ref sourceIndex);
                int distance = (operand >> 2) + 1;
                state = operand & StateMask;
                CopyMatch(destination, ref destinationIndex, distance, matchLength);
                CopyLiterals(source, ref sourceIndex, destination, ref destinationIndex, state);
                continue;
            }

            int next = ReadLittleEndianUInt16(source, sourceIndex);
            if (version == 1 && (next & RleDistanceMask) == RleDistanceMask && (token & 0xF8) == 0x18)
            {
                EnsureInput(source, sourceIndex, 3);
                int zeroRunLength = (source[sourceIndex + 2] << 3) + (token & 0x07) + MinZeroRunLength;
                EnsureOutput(destination, destinationIndex, zeroRunLength);
                destination.Slice(destinationIndex, zeroRunLength).Clear();
                destinationIndex += zeroRunLength;
                state = next & StateMask;
                sourceIndex += 3;
                CopyLiterals(source, ref sourceIndex, destination, ref destinationIndex, state);
                continue;
            }

            int m4Length = ReadLength(source, ref sourceIndex, token & 0x07, 7, 2);
            int m4Operand = ReadLittleEndianUInt16(source, ref sourceIndex);
            int m4Distance = MaxM3Distance + ((token & 0x08) << 11) + (m4Operand >> 2);
            state = m4Operand & StateMask;

            if (m4Distance == MaxM3Distance)
            {
                if (m4Length != MinMatchLength)
                {
                    throw new CompressionException("Invalid LZO end marker length.");
                }

                if (sourceIndex != source.Length)
                {
                    throw new CompressionException("LZO input contains trailing bytes after the end marker.");
                }

                return destinationIndex;
            }

            CopyMatch(destination, ref destinationIndex, m4Distance, m4Length);
            CopyLiterals(source, ref sourceIndex, destination, ref destinationIndex, state);
        }
    }

    /// <inheritdoc/>
    public override int Decompress(ReadOnlySpan<byte> source, Span<byte> destination, ReadOnlySpan<byte> dictionary)
    {
        throw new NotSupportedException("LzoCodec does not support dictionaries.");
    }

    /// <inheritdoc/>
    public override int GetMaxCompressedSize(int inputSize)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(inputSize);
        long maxSize = (long)inputSize + (inputSize / 16) + 67;

        if (maxSize > int.MaxValue)
        {
            throw new ArgumentOutOfRangeException(nameof(inputSize), "Input is too large to calculate an LZO bound as an Int32.");
        }

        return (int)maxSize;
    }

    /// <inheritdoc/>
    public override int GetDecompressedSize(ReadOnlySpan<byte> compressedBuffer)
    {
        throw new NotSupportedException("LZO does not store the decompressed size.");
    }

    private static void CopyLiterals(ReadOnlySpan<byte> source, ref int sourceIndex, Span<byte> destination, ref int destinationIndex, int length)
    {
        if (length == 0)
        {
            return;
        }

        EnsureInput(source, sourceIndex, length);
        EnsureOutput(destination, destinationIndex, length);
        source.Slice(sourceIndex, length).CopyTo(destination[destinationIndex..]);
        sourceIndex += length;
        destinationIndex += length;
    }

    private static void CopyMatch(Span<byte> destination, ref int destinationIndex, int distance, int length)
    {
        if (distance <= 0 || distance > destinationIndex)
        {
            throw new CompressionException("LZO match points before the start of the output buffer.");
        }

        EnsureOutput(destination, destinationIndex, length);
        int matchIndex = destinationIndex - distance;
        int matchEnd = destinationIndex + length;

        while (destinationIndex < matchEnd)
        {
            destination[destinationIndex++] = destination[matchIndex++];
        }
    }

    private static int CountMatchLength(ReadOnlySpan<byte> source, int matchIndex, int sourceIndex)
    {
        int length = MinMatchLength;

        while (sourceIndex + length < source.Length && source[matchIndex + length] == source[sourceIndex + length])
        {
            length++;
        }

        return length;
    }

    private static void EmitLiterals(ReadOnlySpan<byte> source, Span<byte> destination, ref int destinationIndex, ref bool wroteInstruction, ref int lastStateIndex)
    {
        if (source.Length == 0)
        {
            return;
        }

        if (lastStateIndex != NotFound && source.Length <= StateMask)
        {
            destination[lastStateIndex] = (byte)((destination[lastStateIndex] & StateResetMask) | source.Length);
            WriteBytes(source, destination, ref destinationIndex);
            lastStateIndex = NotFound;
            return;
        }

        lastStateIndex = NotFound;

        if (!wroteInstruction && source.Length <= MaxFirstLiteralRun)
        {
            WriteByte(source.Length + FirstLiteralOffset, destination, ref destinationIndex);
            WriteBytes(source, destination, ref destinationIndex);
            wroteInstruction = true;
            return;
        }

        EmitLongLiteralRun(source, destination, ref destinationIndex);
        wroteInstruction = true;
    }

    private static void EmitLongLiteralRun(ReadOnlySpan<byte> source, Span<byte> destination, ref int destinationIndex)
    {
        if (source.Length <= LiteralRunBase + MinMatchLength)
        {
            WriteByte(source.Length - MinMatchLength, destination, ref destinationIndex);
        }
        else
        {
            WriteByte(0, destination, ref destinationIndex);
            WriteLengthRemainder(source.Length - LiteralRunBase - MinMatchLength, destination, ref destinationIndex);
        }

        WriteBytes(source, destination, ref destinationIndex);
    }

    private static void EmitMatch(int length, int distance, Span<byte> destination, ref int destinationIndex, ref int lastStateIndex)
    {
        if (distance <= MaxM3Distance)
        {
            EmitM3Match(length, distance, destination, ref destinationIndex, ref lastStateIndex);
            return;
        }

        EmitM4Match(length, distance, destination, ref destinationIndex, ref lastStateIndex);
    }

    private static void EmitM3Match(int length, int distance, Span<byte> destination, ref int destinationIndex, ref int lastStateIndex)
    {
        if (length <= 33)
        {
            WriteByte(M3Marker | (length - 2), destination, ref destinationIndex);
        }
        else
        {
            WriteByte(M3Marker, destination, ref destinationIndex);
            WriteLengthRemainder(length - 33, destination, ref destinationIndex);
        }

        int operand = (distance - 1) << 2;
        lastStateIndex = destinationIndex;
        WriteLittleEndianUInt16(operand, destination, ref destinationIndex);
    }

    private static void EmitM4Match(int length, int distance, Span<byte> destination, ref int destinationIndex, ref int lastStateIndex)
    {
        int highDistance = distance >= MaxM3Distance * 2 ? 1 : 0;
        int encodedDistance = distance - MaxM3Distance - (highDistance << 14);

        if (length <= 9)
        {
            WriteByte(M4Marker | (highDistance << 3) | (length - 2), destination, ref destinationIndex);
        }
        else
        {
            WriteByte(M4Marker | (highDistance << 3), destination, ref destinationIndex);
            WriteLengthRemainder(length - 9, destination, ref destinationIndex);
        }

        int operand = encodedDistance << 2;
        lastStateIndex = destinationIndex;
        WriteLittleEndianUInt16(operand, destination, ref destinationIndex);
    }

    private static void EnsureInput(ReadOnlySpan<byte> source, int sourceIndex, int length)
    {
        if (length > source.Length - sourceIndex)
        {
            throw new CompressionException("LZO input ended before the current instruction was complete.");
        }
    }

    private static void EnsureOutput(Span<byte> destination, int destinationIndex, int length)
    {
        if (length > destination.Length - destinationIndex)
        {
            throw new CompressionException("Destination buffer is not large enough for the LZO output.");
        }
    }

    private static int GetHash(ReadOnlySpan<byte> source, int sourceIndex)
    {
        uint value = source[sourceIndex] | ((uint)source[sourceIndex + 1] << 8) | ((uint)source[sourceIndex + 2] << 16);
        return (int)((value * 2654435761u) >> (32 - HashBits));
    }

    private static bool HasMatch(ReadOnlySpan<byte> source, int matchIndex, int sourceIndex)
    {
        return source[matchIndex] == source[sourceIndex]
            && source[matchIndex + 1] == source[sourceIndex + 1]
            && source[matchIndex + 2] == source[sourceIndex + 2];
    }

    private static int ReadLength(ReadOnlySpan<byte> source, ref int sourceIndex, int value, int emptyBase, int adjustment)
    {
        long length = value;

        if (length == 0)
        {
            length = emptyBase;

            while (true)
            {
                EnsureInput(source, sourceIndex, 1);
                int next = source[sourceIndex++];
                length += next;

                if (next != 0)
                {
                    break;
                }

                length += byte.MaxValue;
            }
        }

        length += adjustment;

        if (length > int.MaxValue)
        {
            throw new CompressionException("LZO instruction length is too large.");
        }

        return (int)length;
    }

    private static int ReadLittleEndianUInt16(ReadOnlySpan<byte> source, int sourceIndex)
    {
        EnsureInput(source, sourceIndex, 2);
        return source[sourceIndex] | (source[sourceIndex + 1] << 8);
    }

    private static int ReadLittleEndianUInt16(ReadOnlySpan<byte> source, ref int sourceIndex)
    {
        int value = ReadLittleEndianUInt16(source, sourceIndex);
        sourceIndex += 2;
        return value;
    }

    private static void WriteByte(int value, Span<byte> destination, ref int destinationIndex)
    {
        EnsureOutput(destination, destinationIndex, 1);
        destination[destinationIndex++] = (byte)value;
    }

    private static void WriteBytes(ReadOnlySpan<byte> source, Span<byte> destination, ref int destinationIndex)
    {
        EnsureOutput(destination, destinationIndex, source.Length);
        source.CopyTo(destination[destinationIndex..]);
        destinationIndex += source.Length;
    }

    private static void WriteEndMarker(Span<byte> destination, ref int destinationIndex)
    {
        WriteByte(EndMarker, destination, ref destinationIndex);
        WriteByte(0, destination, ref destinationIndex);
        WriteByte(0, destination, ref destinationIndex);
    }

    private static void WriteLengthRemainder(int value, Span<byte> destination, ref int destinationIndex)
    {
        int remainder = value;

        while (remainder > byte.MaxValue)
        {
            WriteByte(0, destination, ref destinationIndex);
            remainder -= byte.MaxValue;
        }

        WriteByte(remainder, destination, ref destinationIndex);
    }

    private static void WriteLittleEndianUInt16(int value, Span<byte> destination, ref int destinationIndex)
    {
        EnsureOutput(destination, destinationIndex, 2);
        destination[destinationIndex++] = (byte)value;
        destination[destinationIndex++] = (byte)(value >> 8);
    }
}