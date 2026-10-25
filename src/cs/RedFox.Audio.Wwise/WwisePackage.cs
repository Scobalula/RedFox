using System.Buffers.Binary;
using System.Text;

namespace RedFox.Audio.Wwise;

public sealed class WwisePackage
{
    public Dictionary<uint, string> Languages { get; } = [];

    public List<WwisePackageEntry> Entries { get; } = [];

    public static WwisePackage Read(Stream stream)
    {
        Span<byte> prefix = stackalloc byte[28];
        stream.ReadExactly(prefix);

        if (!prefix.StartsWith("AKPK"u8))
            throw new InvalidDataException("The stream is not a Wwise package.");
        if (BinaryPrimitives.ReadUInt32LittleEndian(prefix[8..]) != 1)
            throw new NotSupportedException("Only Wwise package version 1 is supported.");

        long headerSize = BinaryPrimitives.ReadUInt32LittleEndian(prefix[4..]) + 8L;
        int languageSize = checked((int)BinaryPrimitives.ReadUInt32LittleEndian(prefix[12..]));
        int bankSize = checked((int)BinaryPrimitives.ReadUInt32LittleEndian(prefix[16..]));
        int mediaSize = checked((int)BinaryPrimitives.ReadUInt32LittleEndian(prefix[20..]));
        int externalSize = checked((int)BinaryPrimitives.ReadUInt32LittleEndian(prefix[24..]));
        long tableSize = (long)languageSize + bankSize + mediaSize + externalSize;

        if (headerSize < 28 || headerSize > stream.Length || tableSize > headerSize - 28)
            throw new InvalidDataException("Wwise package tables exceed the header.");

        byte[] tables = new byte[checked((int)tableSize)];
        stream.ReadExactly(tables);
        var package = new WwisePackage();
        package.ReadLanguages(tables.AsSpan(0, languageSize));
        package.ReadEntries(tables.AsSpan(languageSize, bankSize), WwisePackageEntryType.Bank, headerSize);
        package.ReadEntries(tables.AsSpan(languageSize + bankSize, mediaSize), WwisePackageEntryType.Media, headerSize);
        package.ReadEntries(tables.AsSpan(languageSize + bankSize + mediaSize, externalSize), WwisePackageEntryType.External, headerSize);
        return package;
    }

    public static byte[] ReadEntry(Stream stream, WwisePackageEntry entry)
    {
        if (entry.Offset < 0 || entry.Offset > stream.Length || entry.Size > stream.Length - entry.Offset)
            throw new InvalidDataException($"Wwise package entry {entry.Id} is missing its payload.");

        stream.Position = entry.Offset;
        byte[] data = new byte[checked((int)entry.Size)];
        stream.ReadExactly(data);
        return data;
    }

    private void ReadLanguages(ReadOnlySpan<byte> table)
    {
        if (table.IsEmpty)
            return;
        if (table.Length < 4)
            throw new InvalidDataException("Wwise language table is truncated.");

        uint count = BinaryPrimitives.ReadUInt32LittleEndian(table);

        if (count > (table.Length - 4) / 8)
            throw new InvalidDataException("Wwise language table has an invalid count.");

        for (int index = 0; index < count; index++)
        {
            int offset = checked((int)BinaryPrimitives.ReadUInt32LittleEndian(table[(4 + index * 8)..]));
            uint id = BinaryPrimitives.ReadUInt32LittleEndian(table[(8 + index * 8)..]);

            if (offset < 4 + count * 8 || offset > table.Length - 2 || (offset & 1) != 0)
                throw new InvalidDataException("Wwise language name has an invalid offset.");

            int end = offset;

            while (end <= table.Length - 2 && BinaryPrimitives.ReadUInt16LittleEndian(table[end..]) != 0)
                end += 2;

            if (end > table.Length - 2)
                throw new InvalidDataException("Wwise language name is not terminated.");

            Languages.Add(id, Encoding.Unicode.GetString(table[offset..end]));
        }
    }

    private void ReadEntries(ReadOnlySpan<byte> table, WwisePackageEntryType type, long headerSize)
    {
        if (table.IsEmpty)
            return;
        if (table.Length < 4)
            throw new InvalidDataException("Wwise file table is truncated.");

        int idSize = type == WwisePackageEntryType.External ? 8 : 4;
        int entrySize = idSize + 16;
        uint count = BinaryPrimitives.ReadUInt32LittleEndian(table);

        if (count > (table.Length - 4) / entrySize)
            throw new InvalidDataException("Wwise file table has an invalid count.");

        for (int index = 0; index < count; index++)
        {
            var entry = table.Slice(4 + index * entrySize, entrySize);
            ulong id = idSize == 8 ? BinaryPrimitives.ReadUInt64LittleEndian(entry) : BinaryPrimitives.ReadUInt32LittleEndian(entry);
            uint blockSize = BinaryPrimitives.ReadUInt32LittleEndian(entry[idSize..]);
            uint size = BinaryPrimitives.ReadUInt32LittleEndian(entry[(idSize + 4)..]);
            uint startBlock = BinaryPrimitives.ReadUInt32LittleEndian(entry[(idSize + 8)..]);
            uint languageId = BinaryPrimitives.ReadUInt32LittleEndian(entry[(idSize + 12)..]);
            long offset = checked((long)blockSize * startBlock);

            if (blockSize == 0 || offset < headerSize)
                throw new InvalidDataException($"Wwise package entry {id} has an invalid offset.");

            Entries.Add(new WwisePackageEntry(id, type, languageId, offset, size));
        }
    }
}
