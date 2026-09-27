using System;

namespace RedFox.Imaging.Formats.Tiff;

/// <summary>
/// Represents the mutable state used while decoding TIFF LZW code streams.
/// </summary>
/// <param name="sourceData">The compressed TIFF LZW byte stream.</param>
/// <param name="prefixes">The prefix table used to rebuild dictionary strings.</param>
/// <param name="suffixes">The suffix table used to rebuild dictionary strings.</param>
/// <param name="lengths">The cached string lengths for each dictionary entry.</param>
/// <param name="decodeBuffer">The scratch buffer used to materialize decoded strings.</param>
/// <param name="firstCode">The first non-control code in the dictionary.</param>
/// <param name="leastSignificantBitFirst"><see langword="true"/> to read codes LSB-first; otherwise MSB-first.</param>
internal ref struct TiffLzwDecoder(ReadOnlySpan<byte> sourceData, int[] prefixes, byte[] suffixes, int[] lengths, byte[] decodeBuffer, int firstCode, bool leastSignificantBitFirst)
{
    private readonly ReadOnlySpan<byte> _sourceData = sourceData;
    private int _bytePos;
    private uint _bitBuffer;
    private int _bitsInBuffer;
    private readonly int _totalBytes = sourceData.Length;
    private readonly int _firstCode = firstCode;
    private readonly bool _leastSignificantBitFirst = leastSignificantBitFirst;
    private readonly int[] _prefixes = prefixes;
    private readonly byte[] _suffixes = suffixes;
    private readonly int[] _lengths = lengths;
    private readonly byte[] _decodeBuffer = decodeBuffer;

    /// <summary>
    /// Gets the source LZW byte stream.
    /// </summary>
    public readonly ReadOnlySpan<byte> Source => _sourceData;

    /// <summary>
    /// Gets or sets the current byte position within <see cref="Source"/>.
    /// </summary>
    public int BytePosition
    {
        readonly get => _bytePos;
        set => _bytePos = value;
    }

    /// <summary>
    /// Gets or sets the current bit accumulator contents.
    /// </summary>
    public uint BitBuffer
    {
        readonly get => _bitBuffer;
        set => _bitBuffer = value;
    }

    /// <summary>
    /// Gets or sets the number of valid bits currently stored in <see cref="BitBuffer"/>.
    /// </summary>
    public int BitsInBuffer
    {
        readonly get => _bitsInBuffer;
        set => _bitsInBuffer = value;
    }

    /// <summary>
    /// Gets the total number of bytes available in <see cref="Source"/>.
    /// </summary>
    public readonly int TotalBytes => _totalBytes;

    /// <summary>
    /// Gets the first non-control code in the decoder table.
    /// </summary>
    public readonly int FirstCode => _firstCode;

    /// <summary>
    /// Gets a value indicating whether codes are read least-significant-bit first.
    /// </summary>
    public readonly bool LeastSignificantBitFirst => _leastSignificantBitFirst;

    /// <summary>
    /// Gets the prefix table used to reconstruct dictionary strings.
    /// </summary>
    public readonly Span<int> Prefixes => _prefixes;

    /// <summary>
    /// Gets the suffix table used to reconstruct dictionary strings.
    /// </summary>
    public readonly Span<byte> Suffixes => _suffixes;

    /// <summary>
    /// Gets the cached string lengths for each decoder table entry.
    /// </summary>
    public readonly Span<int> Lengths => _lengths;

    /// <summary>
    /// Gets or sets the next table entry index that will be assigned during decoding.
    /// </summary>
    public int NextCode { get; set; } = firstCode;

    /// <summary>
    /// Gets or sets the current number of bits used to read each code.
    /// </summary>
    public int CodeSize { get; set; } = TiffConstants.LzwInitialCodeSize;

    /// <summary>
    /// Gets the scratch buffer used to materialize decoded strings.
    /// </summary>
    public readonly Span<byte> DecodeBufferSpan => _decodeBuffer;

    /// <summary>
    /// Resets the decoder string table to its initial TIFF state.
    /// </summary>
    public void ResetTable()
    {
        for (int i = 0; i < 256; i++)
        {
            _prefixes[i] = -1;
            _suffixes[i] = (byte)i;
            _lengths[i] = 1;
        }

        NextCode = _firstCode;
        CodeSize = TiffConstants.LzwInitialCodeSize;
    }

    /// <summary>
    /// Attempts to read the next LZW code from the source stream.
    /// </summary>
    /// <param name="code">Receives the decoded code when one is available.</param>
    /// <returns><see langword="true"/> when a full code was read; otherwise <see langword="false"/>.</returns>
    public bool TryReadCode(out int code)
    {
        int remainingBits = ((_totalBytes - _bytePos) * 8) + _bitsInBuffer;
        if (remainingBits < CodeSize)
        {
            code = -1;
            return false;
        }

        if (_leastSignificantBitFirst)
        {
            while (_bitsInBuffer < CodeSize)
            {
                if (_bytePos >= _totalBytes)
                {
                    code = -1;
                    return false;
                }

                _bitBuffer |= (uint)_sourceData[_bytePos++] << _bitsInBuffer;
                _bitsInBuffer += 8;
            }

            uint lsbMask = (uint)((1 << CodeSize) - 1);
            code = (int)(_bitBuffer & lsbMask);
            _bitBuffer >>= CodeSize;
            _bitsInBuffer -= CodeSize;
            return true;
        }

        while (_bitsInBuffer < CodeSize)
        {
            if (_bytePos >= _totalBytes)
            {
                code = -1;
                return false;
            }

            _bitBuffer = (_bitBuffer << 8) | _sourceData[_bytePos++];
            _bitsInBuffer += 8;
        }

        int shift = _bitsInBuffer - CodeSize;
        uint mask = (uint)((1 << CodeSize) - 1);
        code = (int)((_bitBuffer >> shift) & mask);
        _bitsInBuffer -= CodeSize;

        if (_bitsInBuffer == 0)
        {
            _bitBuffer = 0;
        }
        else
        {
            _bitBuffer &= (uint)((1 << _bitsInBuffer) - 1);
        }

        return true;
    }

    /// <summary>
    /// Attempts to decode the specified code into <see cref="DecodeBufferSpan"/>.
    /// </summary>
    /// <param name="code">The dictionary code to decode.</param>
    /// <param name="length">Receives the decoded byte count when decoding succeeds.</param>
    /// <returns><see langword="true"/> when the code was decoded successfully; otherwise <see langword="false"/>.</returns>
    public readonly bool TryDecodeString(int code, out int length)
    {
        if ((uint)code >= TiffConstants.LzwMaxTableSize)
        {
            length = 0;
            return false;
        }

        length = _lengths[code];
        if (length <= 0 || length > _decodeBuffer.Length)
        {
            length = 0;
            return false;
        }

        int currentCode = code;
        int position = length - 1;
        int remaining = length;

        while (currentCode >= _firstCode)
        {
            if ((uint)currentCode >= TiffConstants.LzwMaxTableSize || remaining-- <= 0)
            {
                length = 0;
                return false;
            }

            int prefix = _prefixes[currentCode];
            if (prefix < 0)
            {
                length = 0;
                return false;
            }

            _decodeBuffer[position--] = _suffixes[currentCode];
            currentCode = prefix;
        }

        if ((uint)currentCode >= 256)
        {
            length = 0;
            return false;
        }

        _decodeBuffer[position] = _suffixes[currentCode];
        return true;
    }
}
