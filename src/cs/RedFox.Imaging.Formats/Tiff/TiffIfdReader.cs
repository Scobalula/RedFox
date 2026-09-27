using System;
using System.Buffers.Binary;

namespace RedFox.Imaging.Formats.Tiff;

/// <summary>
/// Provides methods for parsing TIFF IFD (Image File Directory) structures
/// and extracting tag values from IFD entries.
/// </summary>
internal static class TiffIfdReader
{
    /// <summary>
    /// Parses all IFD entries starting at the given byte offset.
    /// </summary>
    /// <param name="data">The complete TIFF file data.</param>
    /// <param name="offset">Byte offset to the start of the IFD.</param>
    /// <param name="littleEndian"><see langword="true"/> for little-endian byte order.</param>
    /// <returns>An array of parsed <see cref="TiffIfdEntry"/> values.</returns>
    public static TiffIfdEntry[] ParseIFD(ReadOnlySpan<byte> data, uint offset, bool littleEndian)
    {
        if (data.Length < 2 || offset > int.MaxValue || offset > (uint)(data.Length - 2))
            throw new InvalidDataException("The TIFF IFD offset is outside the input data.");

        ushort count = ReadUInt16(data, (int)offset, littleEndian);
        if (2L + count * 12L > data.Length - (long)offset)
            throw new InvalidDataException("The TIFF IFD entries are truncated.");

        var entries = new TiffIfdEntry[count];

        for (int i = 0; i < count; i++)
        {
            int entryOffset = (int)offset + 2 + i * 12;
            entries[i] = new TiffIfdEntry(ReadUInt16(data, entryOffset, littleEndian), ReadUInt16(data, entryOffset + 2, littleEndian), ReadUInt32(data, entryOffset + 4, littleEndian), ReadUInt32(data, entryOffset + 8, littleEndian));
        }

        return entries;
    }

    /// <summary>
    /// Reads a single integer value from the IFD entry matching <paramref name="tagId"/>.
    /// Returns <c>0</c> when the tag is not found.
    /// </summary>
    /// <param name="tags">The parsed IFD entries.</param>
    /// <param name="tagId">The tag identifier to search for.</param>
    /// <param name="data">The complete TIFF file data (needed for offset-based values).</param>
    /// <param name="littleEndian"><see langword="true"/> for little-endian byte order.</param>
    /// <returns>The first value of the tag, or <c>0</c> if absent.</returns>
    public static int GetTagInt(ReadOnlySpan<TiffIfdEntry> tags, ushort tagId, ReadOnlySpan<byte> data, bool littleEndian)
    {
        return GetTagInt(tags, tagId, data, littleEndian, 0);
    }

    /// <summary>
    /// Reads a single integer value from the IFD entry matching <paramref name="tagId"/>.
    /// Correctly handles entries whose value is stored inline (count fits in 4 bytes)
    /// or at an offset (count exceeds inline capacity).
    /// </summary>
    /// <param name="tags">The parsed IFD entries.</param>
    /// <param name="tagId">The tag identifier to search for.</param>
    /// <param name="data">The complete TIFF file data (needed for offset-based values).</param>
    /// <param name="littleEndian"><see langword="true"/> for little-endian byte order.</param>
    /// <param name="defaultValue">Value returned when the tag is not found.</param>
    /// <returns>The first value of the tag, or <paramref name="defaultValue"/> if absent.</returns>
    public static int GetTagInt(ReadOnlySpan<TiffIfdEntry> tags, ushort tagId, ReadOnlySpan<byte> data, bool littleEndian, int defaultValue)
    {
        foreach (var entry in tags)
        {
            if (entry.Tag != tagId)
                continue;

            // Determine whether the value fits inline in the 4-byte value field
            // SHORT (2 bytes): fits inline if count <= 2
            // LONG  (4 bytes): fits inline if count == 1
            // BYTE  (1 byte):  fits inline if count <= 4
            bool isInline = entry.Type switch
            {
                TiffConstants.TypeShort => entry.Count <= 2,
                TiffConstants.TypeLong => entry.Count <= 1,
                TiffConstants.TypeByte => entry.Count <= 4,
                _ => entry.Count <= 1
            };

            if (isInline)
            {
                return entry.Type switch
                {
                    TiffConstants.TypeShort => ReadUInt16Inline(entry.ValueOrOffset, 0, littleEndian),
                    TiffConstants.TypeLong => (int)entry.ValueOrOffset,
                    TiffConstants.TypeByte => (int)((entry.ValueOrOffset >> (littleEndian ? 0 : 24)) & 0xFF),
                    _ => (int)entry.ValueOrOffset
                };
            }

            if (entry.ValueOrOffset > int.MaxValue)
                throw new InvalidDataException("The TIFF tag offset is outside the supported range.");

            int offset = (int)entry.ValueOrOffset;
            return entry.Type switch
            {
                TiffConstants.TypeShort => ReadUInt16(data, offset, littleEndian),
                TiffConstants.TypeLong => (int)ReadUInt32(data, offset, littleEndian),
                TiffConstants.TypeByte => data[offset],
                _ => (int)ReadUInt32(data, offset, littleEndian)
            };
        }

        return defaultValue;
    }

    /// <summary>
    /// Reads an array of unsigned 32-bit values from the IFD entry matching <paramref name="tagId"/>.
    /// Handles both inline and offset-based value storage.
    /// </summary>
    /// <param name="tags">The parsed IFD entries.</param>
    /// <param name="tagId">The tag identifier to search for.</param>
    /// <param name="data">The complete TIFF file data.</param>
    /// <param name="littleEndian"><see langword="true"/> for little-endian byte order.</param>
    /// <returns>An array of values, or an empty array if the tag is absent.</returns>
    public static uint[] GetTagUintArray(ReadOnlySpan<TiffIfdEntry> tags, ushort tagId, ReadOnlySpan<byte> data, bool littleEndian)
    {
        foreach (var entry in tags)
        {
            if (entry.Tag != tagId)
                continue;

            if (entry.Count == 1)
            {
                uint value = entry.Type switch
                {
                    TiffConstants.TypeByte => (entry.ValueOrOffset >> (littleEndian ? 0 : 24)) & 0xFF,
                    TiffConstants.TypeShort => ReadUInt16Inline(entry.ValueOrOffset, 0, littleEndian),
                    _ => entry.ValueOrOffset,
                };
                return [value];
            }

            if (entry.Type == TiffConstants.TypeShort && entry.Count == 2)
                return [ReadUInt16Inline(entry.ValueOrOffset, 0, littleEndian), ReadUInt16Inline(entry.ValueOrOffset, 1, littleEndian)];

            if (entry.Type == TiffConstants.TypeByte && entry.Count <= 4)
            {
                var inlineValues = new uint[(int)entry.Count];
                for (int index = 0; index < inlineValues.Length; index++)
                {
                    int shift = littleEndian ? index * 8 : (3 - index) * 8;
                    inlineValues[index] = (entry.ValueOrOffset >> shift) & 0xFF;
                }
                return inlineValues;
            }

            if (entry.Count > Array.MaxLength || entry.ValueOrOffset > int.MaxValue)
                throw new InvalidDataException("The TIFF tag array is too large.");

            int elementSize = entry.Type switch
            {
                TiffConstants.TypeByte => 1,
                TiffConstants.TypeShort => 2,
                TiffConstants.TypeLong => 4,
                _ => throw new InvalidDataException("The TIFF tag has an unsupported value type.")
            };
            int offset = (int)entry.ValueOrOffset;
            long byteCount = (long)entry.Count * elementSize;
            if (offset < 0 || byteCount > data.Length - (long)offset)
                throw new InvalidDataException("The TIFF tag values are truncated.");

            var values = new uint[(int)entry.Count];

            for (int i = 0; i < entry.Count; i++)
            {
                values[i] = entry.Type switch
                {
                    TiffConstants.TypeByte => data[offset + i],
                    TiffConstants.TypeShort => ReadUInt16(data, offset + i * 2, littleEndian),
                    _ => ReadUInt32(data, offset + i * 4, littleEndian)
                };
            }

            return values;
        }

        return [];
    }

    /// <summary>
    /// Reads a 16-bit unsigned integer from the data at the specified offset.
    /// </summary>
    /// <param name="data">The source byte data.</param>
    /// <param name="offset">The byte offset to read from.</param>
    /// <param name="littleEndian"><see langword="true"/> for little-endian byte order.</param>
    /// <returns>The decoded 16-bit value.</returns>
    public static ushort ReadUInt16(ReadOnlySpan<byte> data, int offset, bool littleEndian)
    {
        if (offset < 0 || offset > data.Length - 2)
            throw new InvalidDataException("The TIFF 16-bit value is outside the input data.");

        return littleEndian ? BinaryPrimitives.ReadUInt16LittleEndian(data[offset..]) : BinaryPrimitives.ReadUInt16BigEndian(data[offset..]);
    }

    /// <summary>
    /// Reads a 32-bit unsigned integer from the data at the specified offset.
    /// </summary>
    /// <param name="data">The source byte data.</param>
    /// <param name="offset">The byte offset to read from.</param>
    /// <param name="littleEndian"><see langword="true"/> for little-endian byte order.</param>
    /// <returns>The decoded 32-bit value.</returns>
    public static uint ReadUInt32(ReadOnlySpan<byte> data, int offset, bool littleEndian)
    {
        if (offset < 0 || offset > data.Length - 4)
            throw new InvalidDataException("The TIFF 32-bit value is outside the input data.");

        return littleEndian ? BinaryPrimitives.ReadUInt32LittleEndian(data[offset..]) : BinaryPrimitives.ReadUInt32BigEndian(data[offset..]);
    }

    /// <summary>
    /// Extracts one of two inline 16-bit values packed into a 32-bit IFD value field.
    /// </summary>
    /// <param name="valueField">The packed 32-bit IFD value field.</param>
    /// <param name="index">The inline SHORT index to read, either 0 or 1.</param>
    /// <param name="littleEndian"><see langword="true"/> for little-endian byte order.</param>
    /// <returns>The requested 16-bit value extracted from <paramref name="valueField"/>.</returns>
    public static ushort ReadUInt16Inline(uint valueField, int index, bool littleEndian)
    {
        // Inline SHORTs: In LE, first value is in low 16 bits. In BE, first is in high 16 bits.
        if (littleEndian)
            return (ushort)(valueField >> (index * 16));
        else
            return (ushort)(valueField >> ((1 - index) * 16));
    }
}
