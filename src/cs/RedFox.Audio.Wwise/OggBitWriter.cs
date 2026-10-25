namespace RedFox.Audio.Wwise;

internal sealed class OggBitWriter(int initialCapacity)
{
    private byte[] _buffer = new byte[Math.Max(initialCapacity, 1)];

    private int _bitPosition;

    public void WriteBits(uint value, int count)
    {
        var requiredBytes = (_bitPosition + count + 7) >> 3;

        if (requiredBytes > _buffer.Length)
            Array.Resize(ref _buffer, Math.Max(_buffer.Length * 2, requiredBytes));

        var bitsWritten = 0;

        while (bitsWritten < count)
        {
            var byteIndex = _bitPosition >> 3;
            var bitIndex = _bitPosition & 7;
            var bitsToWrite = Math.Min(8 - bitIndex, count - bitsWritten);
            var bits = (value >> bitsWritten) & ((1u << bitsToWrite) - 1);

            _buffer[byteIndex] |= (byte)(bits << bitIndex);
            bitsWritten += bitsToWrite;
            _bitPosition += bitsToWrite;
        }
    }

    public void CopyBits(ReadOnlySpan<byte> source, int bitOffset, int bitCount)
    {
        var reader = new OggBitReader(source);

        for (var skipped = 0; skipped < bitOffset; skipped++)
            reader.ReadBit();

        for (; bitCount >= 32; bitCount -= 32)
            WriteBits(reader.ReadBits(32), 32);

        WriteBits(reader.ReadBits(bitCount), bitCount);
    }

    public byte[] ToArray()
    {
        return _buffer[..((_bitPosition + 7) >> 3)];
    }
}
