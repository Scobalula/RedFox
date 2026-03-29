using System.Buffers.Binary;
using static RedFox.Graphics2D.Tiff.TiffConstants;

namespace RedFox.Graphics2D.Tiff
{
    /// <summary>
    /// Provides methods for parsing TIFF IFD (Image File Directory) structures
    /// and extracting tag values from IFD entries.
    /// </summary>
    public static class TiffIfdReader
    {
        /// <summary>
        /// Parses all IFD entries starting at the given byte offset.
        /// </summary>
        /// <param name="data">The complete TIFF file data.</param>
        /// <param name="offset">Byte offset to the start of the IFD.</param>
        /// <param name="le"><see langword="true"/> for little-endian byte order.</param>
        /// <returns>An array of parsed <see cref="TiffIfdEntry"/> values.</returns>
        public static TiffIfdEntry[] ParseIFD(ReadOnlySpan<byte> data, uint offset, bool le)
        {
            ushort count = ReadUInt16(data, (int)offset, le);
            var entries = new TiffIfdEntry[count];

            for (int i = 0; i < count; i++)
            {
                int pos = (int)offset + 2 + i * 12;
                entries[i] = new TiffIfdEntry(
                    ReadUInt16(data, pos, le),
                    ReadUInt16(data, pos + 2, le),
                    ReadUInt32(data, pos + 4, le),
                    ReadUInt32(data, pos + 8, le));
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
        /// <param name="le"><see langword="true"/> for little-endian byte order.</param>
        /// <returns>The first value of the tag, or <c>0</c> if absent.</returns>
        public static int GetTagInt(ReadOnlySpan<TiffIfdEntry> tags, ushort tagId, ReadOnlySpan<byte> data, bool le)
        {
            return GetTagInt(tags, tagId, data, le, 0);
        }

        /// <summary>
        /// Reads a single integer value from the IFD entry matching <paramref name="tagId"/>.
        /// Correctly handles entries whose value is stored inline (count fits in 4 bytes)
        /// or at an offset (count exceeds inline capacity).
        /// </summary>
        /// <param name="tags">The parsed IFD entries.</param>
        /// <param name="tagId">The tag identifier to search for.</param>
        /// <param name="data">The complete TIFF file data (needed for offset-based values).</param>
        /// <param name="le"><see langword="true"/> for little-endian byte order.</param>
        /// <param name="defaultValue">Value returned when the tag is not found.</param>
        /// <returns>The first value of the tag, or <paramref name="defaultValue"/> if absent.</returns>
        public static int GetTagInt(ReadOnlySpan<TiffIfdEntry> tags, ushort tagId, ReadOnlySpan<byte> data, bool le, int defaultValue)
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
                    TypeShort => entry.Count <= 2,
                    TypeLong => entry.Count <= 1,
                    TypeByte => entry.Count <= 4,
                    _ => entry.Count <= 1
                };

                if (isInline)
                {
                    return entry.Type switch
                    {
                        TypeShort => (int)(entry.ValueOrOffset & 0xFFFF),
                        TypeLong => (int)entry.ValueOrOffset,
                        TypeByte => (int)(entry.ValueOrOffset & 0xFF),
                        _ => (int)entry.ValueOrOffset
                    };
                }

                // Value is stored at an offset — read the first value from the data
                int offset = (int)entry.ValueOrOffset;
                return entry.Type switch
                {
                    TypeShort => ReadUInt16(data, offset, le),
                    TypeLong => (int)ReadUInt32(data, offset, le),
                    TypeByte => data[offset],
                    _ => (int)ReadUInt32(data, offset, le)
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
        /// <param name="le"><see langword="true"/> for little-endian byte order.</param>
        /// <returns>An array of values, or an empty array if the tag is absent.</returns>
        public static uint[] GetTagUintArray(ReadOnlySpan<TiffIfdEntry> tags, ushort tagId, ReadOnlySpan<byte> data, bool le)
        {
            foreach (var entry in tags)
            {
                if (entry.Tag != tagId)
                    continue;

                if (entry.Count == 1)
                {
                    uint val = entry.Type == TypeShort
                        ? entry.ValueOrOffset & 0xFFFF
                        : entry.ValueOrOffset;
                    return [val];
                }

                // For SHORT with count == 2, values are packed inline
                if (entry.Type == TypeShort && entry.Count == 2)
                {
                    return
                    [
                        ReadUInt16Inline(entry.ValueOrOffset, 0, le),
                        ReadUInt16Inline(entry.ValueOrOffset, 1, le)
                    ];
                }

                // Values stored at offset
                var values = new uint[entry.Count];
                int offset = (int)entry.ValueOrOffset;

                for (int i = 0; i < entry.Count; i++)
                {
                    values[i] = entry.Type == TypeShort
                        ? ReadUInt16(data, offset + i * 2, le)
                        : ReadUInt32(data, offset + i * 4, le);
                }

                return values;
            }

            return [];
        }

        // ──────────────────────────────────────────────
        // Binary read helpers
        // ──────────────────────────────────────────────

        /// <summary>
        /// Reads a 16-bit unsigned integer from the data at the specified offset.
        /// </summary>
        /// <param name="data">The source byte data.</param>
        /// <param name="offset">The byte offset to read from.</param>
        /// <param name="le"><see langword="true"/> for little-endian byte order.</param>
        /// <returns>The decoded 16-bit value.</returns>
        public static ushort ReadUInt16(ReadOnlySpan<byte> data, int offset, bool le)
        {
            return le
                ? BinaryPrimitives.ReadUInt16LittleEndian(data[offset..])
                : BinaryPrimitives.ReadUInt16BigEndian(data[offset..]);
        }

        /// <summary>
        /// Reads a 32-bit unsigned integer from the data at the specified offset.
        /// </summary>
        /// <param name="data">The source byte data.</param>
        /// <param name="offset">The byte offset to read from.</param>
        /// <param name="le"><see langword="true"/> for little-endian byte order.</param>
        /// <returns>The decoded 32-bit value.</returns>
        public static uint ReadUInt32(ReadOnlySpan<byte> data, int offset, bool le)
        {
            return le
                ? BinaryPrimitives.ReadUInt32LittleEndian(data[offset..])
                : BinaryPrimitives.ReadUInt32BigEndian(data[offset..]);
        }

        /// <summary>
        /// Extracts one of two inline 16-bit values packed into a 32-bit IFD value field.
        /// </summary>
        /// <param name="valueField">The packed 32-bit IFD value field.</param>
        /// <param name="index">The inline SHORT index to read, either 0 or 1.</param>
        /// <param name="le"><see langword="true"/> for little-endian byte order.</param>
        /// <returns>The requested 16-bit value extracted from <paramref name="valueField"/>.</returns>
        public static ushort ReadUInt16Inline(uint valueField, int index, bool le)
        {
            // Inline SHORTs: In LE, first value is in low 16 bits. In BE, first is in high 16 bits.
            if (le)
                return (ushort)(valueField >> (index * 16));
            else
                return (ushort)(valueField >> ((1 - index) * 16));
        }
    }
}
