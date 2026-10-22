// --------------------------------------------------------------------------------------
// Please see LICENSE.md for license information.
// This library is also bound by 3rd party licenses.
// --------------------------------------------------------------------------------------
using System.Buffers.Binary;
using System.Collections;
using System.Diagnostics.CodeAnalysis;
using System.Text;
using System.Text.Unicode;

namespace RedFox.GameExtraction.Hashing;

/// <summary>
/// Represents a name table that maps <see cref="NameKey"/> values to their corresponding string names.
/// </summary>
/// <param name="hashAlgorithm">The name of the hash algorithm used to generate the keys in this table.</param>
public sealed class NameTable(string hashAlgorithm) : IEnumerable<KeyValuePair<NameKey, string>>
{
    private byte[]	_keys		= [];
    private byte[]	_names		= [];
    private int[]	_buckets	= [];
    private int		_keyLength;
    private int		_nameLength;

    internal Span<byte> KeyData => _keys.AsSpan(0, _keyLength);
    internal Span<byte> NameData => _names.AsSpan(0, _nameLength);

    /// <summary>
    /// Gets or sets the name of the hash algorithm used to generate the keys in this table.
    /// </summary>
    public string Name { get; set; } = hashAlgorithm;

    /// <summary>
    /// Gets the number of entries in the name table.
    /// </summary>
    public int Count { get; private set; }

    /// <summary>
    /// Gets or sets optional metadata associated with the name table, such as source information or comments.
    /// </summary>
    public Dictionary<string, string> Metadata { get; set; } = [];

    internal NameTable(string hashAlgorithm, int count, int keyLength, int nameLength) : this(hashAlgorithm)
    {
        _keys		= new byte[keyLength];
        _names		= new byte[nameLength];
        _keyLength	= keyLength;
        _nameLength	= nameLength;

        Count = count;
    }

    /// <summary>
    /// Adds an entry to the name table, overwriting any existing entry with the same key.
    /// </summary>
    /// <param name="key">The hash key of the name.</param>
    /// <param name="value">The string name associated with the key.</param>
    /// <exception cref="ArgumentException">Thrown when <paramref name="value"/> is <see langword="null"/> or whitespace.</exception>
    public void Add(NameKey key, string value) => Add(key.Span, value);

    /// <summary>
    /// Adds an entry to the name table using a byte array key, overwriting any existing entry with the same key.
    /// </summary>
    /// <param name="key">The byte array representing the hash key.</param>
    /// <param name="value">The string name associated with the key.</param>
    public void Add(byte[] key, string value)
    {
        ArgumentNullException.ThrowIfNull(key);
        Add(key.AsSpan(), value);
    }

    /// <summary>
    /// Attempts to retrieve the string name associated with the specified key.
    /// </summary>
    /// <param name="key">The hash key to look up.</param>
    /// <param name="value">When this method returns, contains the string name if the key was found; otherwise, <see langword="null"/>.</param>
    /// <returns><see langword="true"/> if the key was found; otherwise, <see langword="false"/>.</returns>
    public bool TryGetValue(NameKey key, [NotNullWhen(true)] out string? value) => TryGetValue(key.Span, out value);

    /// <summary>
    /// Attempts to retrieve the string name associated with the specified byte array key.
    /// </summary>
    /// <param name="key">The byte array representing the hash key to look up.</param>
    /// <param name="value">When this method returns, contains the string name if the key was found; otherwise, <see langword="null"/>.</param>
    /// <returns><see langword="true"/> if the key was found; otherwise, <see langword="false"/>.</returns>
    public bool TryGetValue(byte[] key, [NotNullWhen(true)] out string? value)
    {
        ArgumentNullException.ThrowIfNull(key);
        return TryGetValue(key.AsSpan(), out value);
    }

    /// <summary>
    /// Attempts to retrieve the string name associated with the specified byte array key.
    /// </summary>
    /// <param name="key">The span representing the hash key to look up.</param>
    /// <param name="value">When this method returns, contains the string name if the key was found; otherwise, <see langword="null"/>.</param>
    /// <returns><see langword="true"/> if the key was found; otherwise, <see langword="false"/>.</returns>
    public bool TryGetValue(ReadOnlySpan<byte> key, [NotNullWhen(true)] out string? value)
    {
        if (_buckets.Length != 0)
        {
            var entry = _buckets[FindBucket(key)] - 1;

            if (entry >= 0)
            {
                value = Encoding.UTF8.GetString(GetName(entry));
                return true;
            }
        }

        value = null;
        return false;
    }

    /// <summary>
    /// Removes all entries from the name table.
    /// </summary>
    public void Clear()
    {
        _keys		= [];
        _names		= [];
        _buckets	= [];
        _keyLength	= 0;
        _nameLength	= 0;

        Count = 0;
    }

    /// <summary>
    /// Trims excess bytes from the buffers.
    /// </summary>
    public void TrimExcess()
    {
        var entries = GetSortedEntries();
        var names = new byte[GetPackedNameSize(entries)];
        ReadOnlySpan<byte> previous = [];
        int position = 0, nameOffset = 0;

        foreach (var entry in entries)
        {
            var record = GetNameRecord(entry);

            if (!record.SequenceEqual(previous))
            {
                nameOffset = position;
                record.CopyTo(names.AsSpan(position));
                position += record.Length;
                previous = record;
            }

            GetKey(entry, out var valuePosition);
            BinaryPrimitives.WriteInt32LittleEndian(_keys.AsSpan(valuePosition, sizeof(int)), nameOffset);
        }

        _names = names;
        _nameLength = names.Length;
        Array.Resize(ref _keys, _keyLength);
    }

    /// <summary>
    /// Returns an enumerator that iterates through the entries in the name table.
    /// </summary>
    public IEnumerator<KeyValuePair<NameKey, string>> GetEnumerator()
    {
        var position = 0;

        while (position < _keyLength)
        {
            var entry = position;
            var key = new NameKey(GetKey(entry, out var valuePosition));
            position = valuePosition + sizeof(int);
            yield return new KeyValuePair<NameKey, string>(key, Encoding.UTF8.GetString(GetName(entry)));
        }
    }

    /// <inheritdoc/>
    IEnumerator<KeyValuePair<NameKey, string>> IEnumerable<KeyValuePair<NameKey, string>>.GetEnumerator() => GetEnumerator();

    /// <inheritdoc/>
    IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();

    internal void BuildIndex()
    {
        ResizeIndex(Count);
        int position = 0, previousNameOffset = -1, nextNameOffset = 0;

        for (var i = 0; i < Count; i++)
        {
            var entry = position;
            var key = GetKey(entry, out var valuePosition);

            if (valuePosition > _keyLength - sizeof(int))
                throw new InvalidDataException("The name file key data is truncated.");

            position = valuePosition + sizeof(int);
            var nameOffset = BinaryPrimitives.ReadInt32LittleEndian(_keys.AsSpan(valuePosition, sizeof(int)));

            if (nameOffset < 0)
                throw new InvalidDataException("The name file name offset is invalid.");

            // Saved keys follow the shared name pool in order, including repeated references.
            if (nameOffset != previousNameOffset)
            {
                if (nameOffset != nextNameOffset)
                    throw new InvalidDataException("The name file name offset is invalid.");

                var length = ReadLength(NameData, ref nextNameOffset);

                if (length == 0 || length > _nameLength - nextNameOffset || !Utf8.IsValid(_names.AsSpan(nextNameOffset, length)))
                    throw new InvalidDataException("The name file name data is invalid.");

                nextNameOffset += length;
                previousNameOffset = nameOffset;
            }

            var bucket = FindBucket(key);

            if (_buckets[bucket] != 0)
                throw new InvalidDataException("The name file contains a duplicate key.");

            _buckets[bucket] = entry + 1;
        }

        if (position != _keyLength || nextNameOffset != _nameLength)
            throw new InvalidDataException("The name file entry data size is invalid.");
    }

    internal int[] GetSortedEntries()
    {
        var entries = new int[Count];
        var position = 0;

        for (var i = 0; i < entries.Length; i++)
        {
            entries[i] = position;
            GetKey(position, out var valuePosition);
            position = valuePosition + sizeof(int);
        }

        Array.Sort(entries, (left, right) => GetName(left).SequenceCompareTo(GetName(right)));
        return entries;
    }

    internal int GetPackedNameSize(int[] entries)
    {
        ReadOnlySpan<byte> previous = [];
        var size = 0;

        foreach (var entry in entries)
        {
            var record = GetNameRecord(entry);

            if (record.SequenceEqual(previous))
                continue;

            size = checked(size + record.Length);
            previous = record;
        }

        return size;
    }

    internal ReadOnlySpan<byte> GetKey(int entry, out int valuePosition)
    {
        var position = entry;
        var length = ReadLength(KeyData, ref position);

        if (length > _keyLength - position)
            throw new InvalidDataException("The name file key length is invalid.");

        valuePosition = position + length;
        return _keys.AsSpan(position, length);
    }

    internal ReadOnlySpan<byte> GetNameRecord(int entry)
    {
        GetKey(entry, out var valuePosition);

        var offset = BinaryPrimitives.ReadInt32LittleEndian(_keys.AsSpan(valuePosition, sizeof(int)));
        var position = offset;
        var length = ReadLength(NameData, ref position);

        return _names.AsSpan(offset, position - offset + length);
    }

    private void Add(ReadOnlySpan<byte> key, string value)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(value);
        var bucket		= _buckets.Length == 0 ? -1 : FindBucket(key);
        var entry		= bucket < 0 ? -1 : _buckets[bucket] - 1;
        var nameLength	= Encoding.UTF8.GetByteCount(value);
        var nameOffset	= _nameLength;

        EnsureCapacity(ref _names, checked(_nameLength + 5 + nameLength));
        _nameLength += WriteLength(_names.AsSpan(_nameLength), nameLength);
        _nameLength += Encoding.UTF8.GetBytes(value, _names.AsSpan(_nameLength));

        int valuePosition;

        if (entry >= 0)
        {
            GetKey(entry, out valuePosition);
        }
        else
        {
            if (_buckets.Length == 0 || Count + 1 > _buckets.Length - _buckets.Length / 4)
                ResizeIndex(Count + 1);

            bucket = FindBucket(key);
            EnsureCapacity(ref _keys, checked(_keyLength + 5 + key.Length + sizeof(int)));

            _buckets[bucket] = _keyLength + 1;
            _keyLength += WriteLength(_keys.AsSpan(_keyLength), key.Length);

            key.CopyTo(_keys.AsSpan(_keyLength));

            _keyLength += key.Length;
            valuePosition = _keyLength;
            _keyLength += sizeof(int);
            Count++;
        }

        BinaryPrimitives.WriteInt32LittleEndian(_keys.AsSpan(valuePosition, sizeof(int)), nameOffset);
    }

    private ReadOnlySpan<byte> GetName(int entry)
    {
        GetKey(entry, out var valuePosition);
        var position = BinaryPrimitives.ReadInt32LittleEndian(_keys.AsSpan(valuePosition, sizeof(int)));
        var length = ReadLength(NameData, ref position);
        return _names.AsSpan(position, length);
    }

    private int FindBucket(ReadOnlySpan<byte> key)
    {
        var bucket = (int)((uint)NameKey.ComputeHashCode(key) & (uint)(_buckets.Length - 1));

        while (_buckets[bucket] != 0)
        {
            if (GetKey(_buckets[bucket] - 1, out _).SequenceEqual(key))
                break;

            bucket = (bucket + 1) & (_buckets.Length - 1);
        }

        return bucket;
    }

    private void ResizeIndex(int count)
    {
        if (count == 0)
            return;

        var capacity = 16;

        while (count > capacity - capacity / 4)
            capacity = checked(capacity * 2);

        var previous = _buckets;
        _buckets = new int[capacity];

        foreach (var offset in previous)
        {
            if (offset != 0)
                _buckets[FindBucket(GetKey(offset - 1, out _))] = offset;
        }
    }

    private static void EnsureCapacity(ref byte[] data, int length)
    {
        if (length > data.Length)
            Array.Resize(ref data, (int)Math.Min(Array.MaxLength, Math.Max(length, Math.Max(256, (long)data.Length * 2))));
    }

    private static int ReadLength(ReadOnlySpan<byte> data, ref int position)
    {
        uint value = 0;

        for (var shift = 0; shift < 35; shift += 7)
        {
            if ((uint)position >= (uint)data.Length)
                throw new InvalidDataException("The name file entry length is truncated.");

            var current = data[position++];

            if (shift == 28 && current > 7)
                throw new InvalidDataException("The name file entry length is invalid.");

            value |= (uint)(current & 127) << shift;

            if (current < 128)
                return (int)value;
        }

        throw new InvalidDataException("The name file entry length is invalid.");
    }

    private static int WriteLength(Span<byte> data, int value)
    {
        var position = 0;

        while (value >= 128)
        {
            data[position++] = (byte)(value | 128);
            value >>= 7;
        }

        data[position++] = (byte)value;
        return position;
    }
}
