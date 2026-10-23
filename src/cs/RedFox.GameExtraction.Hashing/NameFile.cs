// --------------------------------------------------------------------------------------
// Please see LICENSE.md for license information.
// This library is also bound by 3rd party licenses.
// --------------------------------------------------------------------------------------
using System.Buffers.Binary;
using System.Security.Cryptography;
using System.Text;

namespace RedFox.GameExtraction.Hashing;

/// <summary>
/// Provides methods for loading and saving name tables from various file formats.
/// </summary>
public static class NameFile
{
    private const ulong	MagicNumber		= 0x454C494648534148;
    private const int	ChecksumSize	= 32;
    private const int	BlockSize		= 1024 * 1024;

    /// <summary>
    /// Loads a name table from a file, automatically detecting the format from the file extension.
    /// </summary>
    /// <param name="filePath">The path to the name file.</param>
    /// <returns>The loaded name table.</returns>
    /// <exception cref="ArgumentException">Thrown when loading a .txt file without a hasher function.</exception>
    /// <exception cref="NotSupportedException">Thrown when the file format is not supported.</exception>
    public static NameTable Load(string filePath)
    {
        var extension = Path.GetExtension(filePath).ToLowerInvariant();

        return extension switch
        {
            ".namefile" => LoadBinary(filePath),
            ".csv"      => NameTableCsvReader.FromFile(filePath, DeriveAlgorithm(filePath)),
            ".txt"      => throw new ArgumentException("A hash algorithm and hasher function are required to load .txt files. Use the Load overload that accepts them."),
            _           => throw new NotSupportedException($"Unsupported file format: '{extension}'. Supported formats: .namefile, .csv, .txt")
        };
    }

    /// <summary>
    /// Loads a name table from a file with an explicit hash algorithm, automatically detecting the format from the file extension.
    /// </summary>
    /// <param name="filePath">The path to the name file.</param>
    /// <param name="hashAlgorithm">The name of the hash algorithm used to generate the file.</param>
    /// <returns>The loaded name table.</returns>
    /// <exception cref="ArgumentException">Thrown when loading a .txt file without a hasher function.</exception>
    /// <exception cref="NotSupportedException">Thrown when the file format is not supported.</exception>
    public static NameTable Load(string filePath, string hashAlgorithm)
    {
        var extension = Path.GetExtension(filePath).ToLowerInvariant();

        return extension switch
        {
            ".namefile" => LoadBinary(filePath),
            ".csv"      => NameTableCsvReader.FromFile(filePath, hashAlgorithm),
            ".txt"      => throw new ArgumentException("A hasher function is required to load .txt files. Use the Load overload that accepts a hasher."),
            _           => throw new NotSupportedException($"Unsupported file format: '{extension}'. Supported formats: .namefile, .csv, .txt")
        };
    }

    /// <summary>
    /// Loads a name table from a file with an explicit hash algorithm and hasher function, automatically detecting the format from the file extension.
    /// </summary>
    /// <param name="filePath">The path to the name file.</param>
    /// <param name="hashAlgorithm">The name of the hash algorithm used to generate the file.</param>
    /// <param name="hasher">The function to use for hashing names when loading .txt files.</param>
    /// <returns>The loaded name table.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="hasher"/> is <see langword="null"/>.</exception>
    /// <exception cref="NotSupportedException">Thrown when the file format is not supported.</exception>
    public static NameTable Load(string filePath, string hashAlgorithm, Func<ReadOnlySpan<char>, NameKey> hasher)
    {
        ArgumentNullException.ThrowIfNull(hasher);

        var extension = Path.GetExtension(filePath).ToLowerInvariant();

        return extension switch
        {
            ".namefile" => LoadBinary(filePath),
            ".csv"      => NameTableCsvReader.FromFile(filePath, hashAlgorithm),
            ".txt"      => NameTableTxtReader.FromFile(filePath, hashAlgorithm, hasher),
            _           => throw new NotSupportedException($"Unsupported file format: '{extension}'. Supported formats: .namefile, .csv, .txt")
        };
    }

    /// <summary>
    /// Loads a name table from a binary .namefile file.
    /// </summary>
    /// <param name="filePath">The path to the .namefile file.</param>
    /// <returns>The loaded name table.</returns>
    public static NameTable LoadBinary(string filePath)
    {
        using var stream = File.OpenRead(filePath);
        return LoadBinary(stream);
    }

    /// <summary>
    /// Loads a name table from a binary stream containing .namefile data.
    /// </summary>
    /// <param name="stream">The stream containing the binary name file data.</param>
    /// <returns>The loaded name table.</returns>
    public static NameTable LoadBinary(Stream stream)
    {
        using var reader = new BinaryReader(stream, Encoding.UTF8, true);
        return LoadBinary(reader);
    }

    /// <summary>
    /// Loads a name table from a <see cref="BinaryReader"/> containing .namefile data.
    /// </summary>
    /// <param name="reader">The binary reader positioned at the start of the name file data.</param>
    /// <returns>The loaded name table.</returns>
    public static NameTable LoadBinary(BinaryReader reader)
    {
        var magic = reader.ReadUInt64();

        if (magic != MagicNumber)
            throw new NotSupportedException($"Invalid magic number: {magic}");

        var flags = (NameFileFlags)reader.ReadUInt64();

        if ((flags & ~(NameFileFlags.Compressed | NameFileFlags.Metadata | NameFileFlags.Checksum)) != 0)
            throw new InvalidDataException("The name file flags are invalid.");

        var entryCount		= reader.Read7BitEncodedInt();
        var hashAlgorithm	= reader.ReadString();
        var metadata		= new Dictionary<string, string>();

        if (flags.HasFlag(NameFileFlags.Metadata))
        {
            var metadataCount = reader.Read7BitEncodedInt();

            if (metadataCount < 0)
                throw new InvalidDataException("The name file metadata count is invalid.");

            for (var i = 0; i < metadataCount; i++)
                metadata[reader.ReadString()] = reader.ReadString();
        }

        var keySize		= reader.Read7BitEncodedInt();
        var nameSize	= reader.Read7BitEncodedInt();

        if (keySize < 0 || keySize > Array.MaxLength || nameSize < 0 || nameSize > Array.MaxLength || entryCount < 0 || entryCount > keySize / 5)
            throw new InvalidDataException("The name file entry data size is invalid.");

        var table = new NameTable(hashAlgorithm, entryCount, keySize, nameSize) { Metadata = metadata };
        var compressed = flags.HasFlag(NameFileFlags.Compressed) ? new byte[NameFileCompressor.GetMaxCompressedSize(BlockSize)] : [];
        using var checksum = flags.HasFlag(NameFileFlags.Checksum) ? IncrementalHash.CreateHash(HashAlgorithmName.SHA256) : null;

        ReadSection(reader, table.KeyData, compressed, checksum);
        ReadSection(reader, table.NameData, compressed, checksum);

        if (checksum is not null)
        {
            Span<byte> expected	= stackalloc byte[ChecksumSize];
            Span<byte> actual	= stackalloc byte[ChecksumSize];

            reader.ReadExactly(expected);
            checksum.GetHashAndReset(actual);

            if (!expected.SequenceEqual(actual))
                throw new InvalidDataException("Name file checksum mismatch.");
        }

        table.BuildIndex();
        return table;
    }

    /// <summary>
    /// Saves a name table to a file, automatically detecting the format from the file extension.
    /// </summary>
    /// <param name="filePath">The path to write the file to.</param>
    /// <param name="nameTable">The name table to save.</param>
    /// <exception cref="NotSupportedException">Thrown when the file format is not supported.</exception>
    public static void Save(string filePath, NameTable nameTable)
    {
        Save(filePath, nameTable, NameFileFlags.None);
    }

    /// <summary>
    /// Saves a name table to a file with the specified flags, automatically detecting the format from the file extension.
    /// </summary>
    /// <param name="filePath">The path to write the file to.</param>
    /// <param name="nameTable">The name table to save.</param>
    /// <param name="flags">Flags controlling the save behavior, such as compression.</param>
    /// <exception cref="NotSupportedException">Thrown when the file format is not supported.</exception>
    public static void Save(string filePath, NameTable nameTable, NameFileFlags flags)
    {
        var extension = Path.GetExtension(filePath).ToLowerInvariant();

        switch (extension)
        {
            case ".namefile":
                SaveBinary(filePath, nameTable, flags);
                break;
            case ".csv":
                SaveCsv(filePath, nameTable);
                break;
            case ".txt":
                SaveTxt(filePath, nameTable);
                break;
            default:
                throw new NotSupportedException($"Unsupported file format: '{extension}'. Supported formats: .namefile, .csv, .txt");
        }
    }

    /// <summary>
    /// Saves a name table to a binary .namefile file.
    /// </summary>
    /// <param name="filePath">The path to write the .namefile to.</param>
    /// <param name="nameTable">The name table to save.</param>
    public static void SaveBinary(string filePath, NameTable nameTable)
    {
        SaveBinary(filePath, nameTable, NameFileFlags.None);
    }

    /// <summary>
    /// Saves a name table to a binary .namefile file with the specified flags.
    /// </summary>
    /// <param name="filePath">The path to write the .namefile to.</param>
    /// <param name="nameTable">The name table to save.</param>
    /// <param name="flags">Flags controlling the save behavior, such as compression.</param>
    public static void SaveBinary(string filePath, NameTable nameTable, NameFileFlags flags)
    {
        using var stream = File.Create(filePath);
        SaveBinary(stream, nameTable, flags);
    }

    /// <summary>
    /// Saves a name table as binary data to a stream.
    /// </summary>
    /// <param name="stream">The stream to write the binary data to.</param>
    /// <param name="nameTable">The name table to save.</param>
    public static void SaveBinary(Stream stream, NameTable nameTable)
    {
        SaveBinary(stream, nameTable, NameFileFlags.None);
    }

    /// <summary>
    /// Saves a name table as binary data to a stream with the specified flags.
    /// </summary>
    /// <param name="stream">The stream to write the binary data to.</param>
    /// <param name="nameTable">The name table to save.</param>
    /// <param name="flags">Flags controlling the save behavior, such as compression.</param>
    public static void SaveBinary(Stream stream, NameTable nameTable, NameFileFlags flags)
    {
        using var writer = new BinaryWriter(stream, Encoding.UTF8, true);
        SaveBinary(writer, nameTable, flags);
    }

    /// <summary>
    /// Saves a name table as binary data using a <see cref="BinaryWriter"/>.
    /// </summary>
    /// <param name="writer">The binary writer to write the data to.</param>
    /// <param name="nameTable">The name table to save.</param>
    public static void SaveBinary(BinaryWriter writer, NameTable nameTable)
    {
        SaveBinary(writer, nameTable, NameFileFlags.None);
    }

    /// <summary>
    /// Saves a name table as binary data using a <see cref="BinaryWriter"/> with the specified flags.
    /// </summary>
    /// <param name="writer">The binary writer to write the data to.</param>
    /// <param name="nameTable">The name table to save.</param>
    /// <param name="flags">Flags controlling the save behavior, such as compression.</param>
    public static void SaveBinary(BinaryWriter writer, NameTable nameTable, NameFileFlags flags)
    {
        var effectiveFlags = nameTable.Metadata.Count > 0 ? flags | NameFileFlags.Metadata : flags;
        var entries = nameTable.GetSortedEntries();

        writer.Write(MagicNumber);
        writer.Write((ulong)effectiveFlags);
        writer.Write7BitEncodedInt(nameTable.Count);
        writer.Write(nameTable.Name);

        if (effectiveFlags.HasFlag(NameFileFlags.Metadata))
        {
            writer.Write7BitEncodedInt(nameTable.Metadata.Count);

            foreach (var (key, value) in nameTable.Metadata)
            {
                writer.Write(key);
                writer.Write(value);
            }
        }

        writer.Write7BitEncodedInt(nameTable.KeyData.Length);
        writer.Write7BitEncodedInt(nameTable.GetPackedNameSize(entries));

        var block = new byte[BlockSize];
        var compressed = effectiveFlags.HasFlag(NameFileFlags.Compressed) ? new byte[NameFileCompressor.GetMaxCompressedSize(BlockSize)] : [];
        using var checksum = effectiveFlags.HasFlag(NameFileFlags.Checksum) ? IncrementalHash.CreateHash(HashAlgorithmName.SHA256) : null;
        ReadOnlySpan<byte> previous = [];
        Span<byte> offsetBytes = stackalloc byte[sizeof(int)];
        int blockLength = 0, nameOffset = 0, nextNameOffset = 0;

        foreach (var entry in entries)
        {
            var record = nameTable.GetNameRecord(entry);

            if (!record.SequenceEqual(previous))
            {
                nameOffset = nextNameOffset;
                nextNameOffset += record.Length;
                previous = record;
            }

            nameTable.GetKey(entry, out var valuePosition);
            WriteData(writer, nameTable.KeyData[entry..valuePosition], block, ref blockLength, compressed, checksum);
            BinaryPrimitives.WriteInt32LittleEndian(offsetBytes, nameOffset);
            WriteData(writer, offsetBytes, block, ref blockLength, compressed, checksum);
        }

        if (blockLength > 0)
            WriteBlock(writer, block.AsSpan(0, blockLength), compressed, checksum);

        blockLength = 0;
        previous = [];

        foreach (var entry in entries)
        {
            var record = nameTable.GetNameRecord(entry);

            if (record.SequenceEqual(previous))
                continue;

            WriteData(writer, record, block, ref blockLength, compressed, checksum);
            previous = record;
        }

        if (blockLength > 0)
            WriteBlock(writer, block.AsSpan(0, blockLength), compressed, checksum);

        if (checksum is not null)
        {
            Span<byte> hash = stackalloc byte[ChecksumSize];
            checksum.GetHashAndReset(hash);
            writer.Write(hash);
        }
    }

    /// <summary>
    /// Saves a name table to a CSV file with each line in the format <c>key,value</c>.
    /// </summary>
    /// <param name="filePath">The path to write the CSV file to.</param>
    /// <param name="nameTable">The name table to save.</param>
    public static void SaveCsv(string filePath, NameTable nameTable)
    {
        using var stream = File.Create(filePath);
        SaveCsv(stream, nameTable);
    }

    /// <summary>
    /// Saves a name table as CSV data to a stream with each line in the format <c>key,value</c>.
    /// </summary>
    /// <param name="stream">The stream to write the CSV data to.</param>
    /// <param name="nameTable">The name table to save.</param>
    public static void SaveCsv(Stream stream, NameTable nameTable)
    {
        using var writer = new StreamWriter(stream, Encoding.UTF8, leaveOpen: true);

        foreach (var (key, value) in nameTable)
            writer.WriteLine($"{key},{value}");
    }

    /// <summary>
    /// Saves a name table to a text file with one name per line.
    /// </summary>
    /// <param name="filePath">The path to write the text file to.</param>
    /// <param name="nameTable">The name table to save.</param>
    public static void SaveTxt(string filePath, NameTable nameTable)
    {
        using var stream = File.Create(filePath);
        SaveTxt(stream, nameTable);
    }

    /// <summary>
    /// Saves a name table as text data to a stream with one name per line.
    /// </summary>
    /// <param name="stream">The stream to write the text data to.</param>
    /// <param name="nameTable">The name table to save.</param>
    public static void SaveTxt(Stream stream, NameTable nameTable)
    {
        using var writer = new StreamWriter(stream, Encoding.UTF8, leaveOpen: true);

        foreach (var (_, value) in nameTable)
            writer.WriteLine(value);
    }

    /// <summary>
    /// Derives the hash algorithm name from a file by extracting it from the file name.
    /// </summary>
    /// <param name="filePath">The path to the file.</param>
    /// <returns>The derived hash algorithm name.</returns>
    public static string DeriveAlgorithm(string filePath)
    {
        var name = Path.GetFileNameWithoutExtension(filePath);

        var open = name.IndexOf('(');
        var close = name.LastIndexOf(')');

        if (open >= 0 && close > open)
            return name[(open + 1)..close].Trim();

        return "Unknown";
    }

    private static void ReadSection(BinaryReader reader, Span<byte> destination, byte[] compressed, IncrementalHash? checksum)
    {
        while (!destination.IsEmpty)
        {
            var decodedSize	= reader.Read7BitEncodedInt();
            var storedSize	= reader.Read7BitEncodedInt();

            if (decodedSize <= 0 || decodedSize > BlockSize || decodedSize > destination.Length || storedSize <= 0)
                throw new InvalidDataException("The name file block size is invalid.");

            var block = destination[..decodedSize];

            if (compressed.Length != 0)
            {
                if (storedSize > NameFileCompressor.GetMaxCompressedSize(decodedSize))
                    throw new InvalidDataException("The name file compressed block size is invalid.");

                reader.ReadExactly(compressed.AsSpan(0, storedSize));

                if (NameFileCompressor.Decompress(compressed.AsSpan(0, storedSize), block) != decodedSize)
                    throw new InvalidDataException("The name file decoded block size is invalid.");
            }
            else
            {
                if (storedSize != decodedSize)
                    throw new InvalidDataException("The name file block size is invalid.");

                reader.ReadExactly(block);
            }

            checksum?.AppendData(block);
            destination = destination[decodedSize..];
        }
    }

    private static void WriteData(BinaryWriter writer, ReadOnlySpan<byte> data, byte[] block, ref int blockLength, byte[] compressed, IncrementalHash? checksum)
    {
        while (!data.IsEmpty)
        {
            var length = Math.Min(data.Length, block.Length - blockLength);
            data[..length].CopyTo(block.AsSpan(blockLength));
            blockLength += length;
            data = data[length..];

            if (blockLength == block.Length)
            {
                WriteBlock(writer, block, compressed, checksum);
                blockLength = 0;
            }
        }
    }

    private static void WriteBlock(BinaryWriter writer, ReadOnlySpan<byte> block, byte[] compressed, IncrementalHash? checksum)
    {
        checksum?.AppendData(block);
        writer.Write7BitEncodedInt(block.Length);

        if (compressed.Length != 0)
        {
            var length = NameFileCompressor.Compress(block, compressed);
            writer.Write7BitEncodedInt(length);
            writer.Write(compressed.AsSpan(0, length));
        }
        else
        {
            writer.Write7BitEncodedInt(block.Length);
            writer.Write(block);
        }
    }
}
