namespace RedFox.Audio.Wwise;

internal ref struct OggBitReader
{
    private readonly ReadOnlySpan<byte> _data;

    private int _bitPosition;

    public OggBitReader(ReadOnlySpan<byte> data)
    {
        _data = data;
    }

    public uint ReadBits(int count)
    {
        uint result = 0;

        var bitsRead = 0;

        while (bitsRead < count)
        {
            var byteIndex = _bitPosition >> 3;
            var bitIndex = _bitPosition & 7;

            if (byteIndex >= _data.Length)
                throw new InvalidDataException("Attempted to read past the end of the bitstream.");

            var bitsToRead = Math.Min(8 - bitIndex, count - bitsRead);
            var bits = (uint)(_data[byteIndex] >> bitIndex) & ((1u << bitsToRead) - 1);

            result |= bits << bitsRead;
            bitsRead += bitsToRead;
            _bitPosition += bitsToRead;
        }

        return result;
    }

    public bool ReadBit()
    {
        return ReadBits(1) != 0;
    }
}
