using System;
using System.Collections.Generic;
using System.Text;

namespace RedFox.Imaging.Formats.Tiff;

/// <summary>
/// Writes MSB-first variable-width codes into a byte buffer.
/// </summary>
/// <remarks>
/// Initializes a bit writer over the specified destination buffer.
/// </remarks>
/// <param name="buffer">The destination buffer that receives encoded bytes.</param>
internal ref struct TiffBitWriter(byte[] buffer)
{
    private int _bytePos = 0;
    private ulong _bitBuffer = 0ul;
    private int _bitsInBuffer = 0;

    /// <summary>
    /// Gets the destination buffer that receives written bytes.
    /// </summary>
    public readonly byte[] Buffer => buffer;

    /// <summary>
    /// Gets or sets the current byte position in <see cref="Buffer"/>.
    /// </summary>
    public int BytePosition
    {
        readonly get => _bytePos;
        set => _bytePos = value;
    }

    /// <summary>
    /// Gets or sets the current bit accumulator contents.
    /// </summary>
    public ulong BitBuffer
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
    /// Writes the <paramref name="code"/> using <paramref name="codeSize"/> bits (MSB-first).
    /// </summary>
    /// <param name="code">The code value to write.</param>
    /// <param name="codeSize">The number of bits from <paramref name="code"/> to write.</param>
    public void Write(int code, int codeSize)
    {
        _bitBuffer = (_bitBuffer << codeSize) | (uint)code;
        _bitsInBuffer += codeSize;

        while (_bitsInBuffer >= 8)
        {
            int shift = _bitsInBuffer - 8;
            byte b = (byte)((_bitBuffer >> shift) & 0xFFu);
            if (_bytePos >= buffer.Length)
                throw new InvalidOperationException("BitWriter buffer overflow: buffer too small.");
            buffer[_bytePos++] = b;
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
    /// <returns>The total number of bytes written into <see cref="Buffer"/>.</returns>
    public int Flush()
    {
        if (_bitsInBuffer > 0)
        {
            byte b = (byte)(_bitBuffer << (8 - _bitsInBuffer));
            if (_bytePos >= buffer.Length)
                throw new InvalidOperationException("BitWriter buffer overflow: buffer too small.");
            buffer[_bytePos++] = b;
            _bitsInBuffer = 0;
            _bitBuffer = 0;
        }
        return _bytePos;
    }
}
