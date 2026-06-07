// --------------------------------------------------------------------------------------
// Please see LICENSE.md for license information.
// This library is also bound by 3rd party licenses.
// --------------------------------------------------------------------------------------
using System.Buffers.Binary;
using System.Numerics;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace RedFox.GameExtraction.Hashing;

/// <summary>
/// Represents an immutable hash key that stores raw byte data, using inline storage for keys of 16 bytes or fewer.
/// </summary>
public readonly record struct NameKey : IEquatable<NameKey>
{
    private readonly byte[]? _data;
    private readonly UInt128 _inline;
    private readonly int _hashCode;
    private readonly int _length;

    /// <summary>
    /// Gets the raw bytes of the key.
    /// </summary>
    public ReadOnlySpan<byte> Span
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get
        {
            if (_data is not null)
                return _data;
            return MemoryMarshal.AsBytes(MemoryMarshal.CreateReadOnlySpan(in _inline, 1))[.._length];
        }
    }

    /// <summary>
    /// Gets the length of the key in bytes.
    /// </summary>
    public int Length => _length;

    /// <summary>
    /// Initializes a new instance of the <see cref="NameKey"/> struct from a byte array.
    /// </summary>
    /// <param name="data">The byte array to use as the key data.</param>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="data"/> is <see langword="null"/>.</exception>
    public NameKey(byte[] data) : this(data.AsSpan())
    {
        ArgumentNullException.ThrowIfNull(data);
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="NameKey"/> struct from a read-only span of bytes.
    /// </summary>
    /// <param name="data">The byte span to use as the key data.</param>
    public NameKey(ReadOnlySpan<byte> data)
    {
        _length = data.Length;

        if (data.Length <= 16)
        {
            _data = null;
            UInt128 inline = 0;
            for (var i = 0; i < data.Length; i++)
                inline |= (UInt128)data[i] << (i * 8);
            _inline = inline;
        }
        else
        {
            _data = data.ToArray();
            _inline = 0;
        }

        _hashCode = ComputeHashCode(data);
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="NameKey"/> struct from a 32-bit signed integer, stored in big-endian order.
    /// </summary>
    /// <param name="value">The integer value to use as the key.</param>
    public NameKey(int value) : this((uint)value) { }

    /// <summary>
    /// Initializes a new instance of the <see cref="NameKey"/> struct from a 32-bit unsigned integer, stored in minimal big-endian order.
    /// </summary>
    /// <remarks>
    /// Insignificant leading zero bytes are trimmed so that the key represents the logical value rather than a
    /// fixed width, allowing numerically equal values to compare equal regardless of the integer type they came from.
    /// </remarks>
    /// <param name="value">The unsigned integer value to use as the key.</param>
    public NameKey(uint value)
    {
        var length = (BitOperations.Log2(value) >> 3) + 1;
        var inline = BinaryPrimitives.ReverseEndianness(value) >> ((sizeof(uint) - length) << 3);

        _data = null;
        _inline = inline;
        _length = length;
        _hashCode = ComputeInlineHash(inline, length);
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="NameKey"/> struct from a 64-bit signed integer, stored in big-endian order.
    /// </summary>
    /// <param name="value">The long value to use as the key.</param>
    public NameKey(long value) : this((ulong)value) { }

    /// <summary>
    /// Initializes a new instance of the <see cref="NameKey"/> struct from a 64-bit unsigned integer, stored in minimal big-endian order.
    /// </summary>
    /// <remarks>
    /// Insignificant leading zero bytes are trimmed so that the key represents the logical value rather than a
    /// fixed width, allowing numerically equal values to compare equal regardless of the integer type they came from.
    /// </remarks>
    /// <param name="value">The unsigned long value to use as the key.</param>
    public NameKey(ulong value)
    {
        var length = (BitOperations.Log2(value) >> 3) + 1;
        var inline = BinaryPrimitives.ReverseEndianness(value) >> ((sizeof(ulong) - length) << 3);

        _data = null;
        _inline = inline;
        _length = length;
        _hashCode = ComputeInlineHash(inline, length);
    }

    /// <summary>
    /// Implicitly converts a byte array to a <see cref="NameKey"/>.
    /// </summary>
    /// <param name="data">The byte array to convert.</param>
    public static implicit operator NameKey(byte[] data) => new(data);

    /// <summary>
    /// Implicitly converts a 32-bit signed integer to a <see cref="NameKey"/>.
    /// </summary>
    /// <param name="value">The integer value to convert.</param>
    public static implicit operator NameKey(int value) => new(value);

    /// <summary>
    /// Implicitly converts a 32-bit unsigned integer to a <see cref="NameKey"/>.
    /// </summary>
    /// <param name="value">The unsigned integer value to convert.</param>
    public static implicit operator NameKey(uint value) => new(value);

    /// <summary>
    /// Implicitly converts a 64-bit signed integer to a <see cref="NameKey"/>.
    /// </summary>
    /// <param name="value">The long value to convert.</param>
    public static implicit operator NameKey(long value) => new(value);

    /// <summary>
    /// Implicitly converts a 64-bit unsigned integer to a <see cref="NameKey"/>.
    /// </summary>
    /// <param name="value">The unsigned long value to convert.</param>
    public static implicit operator NameKey(ulong value) => new(value);

    /// <inheritdoc/>
    public bool Equals(NameKey other) => Span.SequenceEqual(other.Span);

    /// <inheritdoc/>
    public override int GetHashCode() => _hashCode;

    /// <summary>
    /// Returns the hex string representation of the key.
    /// </summary>
    /// <returns>The key as an uppercase hex string.</returns>
    public override string ToString() => Convert.ToHexString(Span);

    /// <summary>
    /// Computes the FNV-1a hash code for a span of bytes.
    /// </summary>
    /// <param name="data">The byte span to hash.</param>
    /// <returns>The computed FNV-1a hash code.</returns>
    public static int ComputeHashCode(ReadOnlySpan<byte> data)
    {
        uint hash = 2166136261;
        foreach (var b in data)
            hash = (hash ^ b) * 16777619;
        return (int)hash;
    }

    /// <summary>
    /// Computes the FNV-1a hash code for an inline integer value.
    /// </summary>
    /// <param name="data">The integer value to hash, stored in little-endian byte order.</param>
    /// <param name="length">The number of bytes to include in the hash.</param>
    /// <returns>The computed FNV-1a hash code.</returns>
    public static int ComputeInlineHash(ulong data, int length)
    {
        uint hash = 2166136261;
        for (var i = 0; i < length; i++)
        {
            var b = (byte)(data >> (i * 8));
            hash = (hash ^ b) * 16777619;
        }
        return (int)hash;
    }

    /// <summary>
    /// Creates a <see cref="NameKey"/> from a hex string, automatically padding odd-length strings with a leading zero.
    /// </summary>
    /// <param name="hex">The hex string to parse.</param>
    /// <returns>A new <see cref="NameKey"/> representing the parsed bytes.</returns>
    public static NameKey FromHexString(ReadOnlySpan<char> hex)
    {
        if (hex.IsEmpty)
            return new NameKey([]);

        if ((hex.Length & 1) == 0)
            return new NameKey(Convert.FromHexString(hex));

        Span<char> padded = stackalloc char[hex.Length + 1];
        padded[0] = '0';
        hex.CopyTo(padded[1..]);
        return new NameKey(Convert.FromHexString(padded));
    }
}
