// --------------------------------------------------------------------------------------
// Please see LICENSE.md for license information.
// This library is also bound by 3rd party licenses.
// --------------------------------------------------------------------------------------
using System.Reflection.PortableExecutable;
using System.Runtime.Intrinsics.Arm;
using System.Security.Cryptography;
using System.Text;

namespace RedFox.GameExtraction.Hashing;

/// <summary>
/// Provides methods for loading and saving name tables from various file formats.
/// </summary>
public static class NameFile
{
    private const ulong MagicNumber = 0x454C494648534148;
    private const int ChecksumSize = 32;

    /// <summary>
    /// Loads a name table from a file, automatically detecting the format from the file extension.
    /// </summary>
    /// <param name="filePath">The path to the name file.</param>
    /// <returns>The loaded name table.</returns>
    /// <exception cref="ArgumentException">Thrown when loading a .txt file without a hasher function.</exception>
    /// <exception cref="NotSupportedException">Thrown when the file format is not supported.</exception>
    public static NameTable Load(string filePath)
    {
        var ext = Path.GetExtension(filePath).ToLowerInvariant();

        return ext switch
        {
            ".namefile" => LoadBinary(filePath),
            ".csv" => NameTableCsvReader.FromFile(filePath, DeriveAlgorithm(filePath)),
            ".txt" => throw new ArgumentException("A hash algorithm and hasher function are required to load .txt files. Use the Load overload that accepts them."),
            _ => throw new NotSupportedException($"Unsupported file format: '{ext}'. Supported formats: .namefile, .csv, .txt")
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
        var ext = Path.GetExtension(filePath).ToLowerInvariant();

        return ext switch
        {
            ".namefile" => LoadBinary(filePath),
            ".csv" => NameTableCsvReader.FromFile(filePath, hashAlgorithm),
            ".txt" => throw new ArgumentException("A hasher function is required to load .txt files. Use the Load overload that accepts a hasher."),
            _ => throw new NotSupportedException($"Unsupported file format: '{ext}'. Supported formats: .namefile, .csv, .txt")
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

        var ext = Path.GetExtension(filePath).ToLowerInvariant();

        return ext switch
        {
            ".namefile" => LoadBinary(filePath),
            ".csv" => NameTableCsvReader.FromFile(filePath, hashAlgorithm),
            ".txt" => NameTableTxtReader.FromFile(filePath, hashAlgorithm, hasher),
            _ => throw new NotSupportedException($"Unsupported file format: '{ext}'. Supported formats: .namefile, .csv, .txt")
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
        var entryCount = reader.Read7BitEncodedInt();
        var hashAlgorithm = reader.ReadString();

        var table = new NameTable(hashAlgorithm);

        if (flags.HasFlag(NameFileFlags.Metadata))
        {
            var metadataCount = reader.Read7BitEncodedInt();

            for (var i = 0; i < metadataCount; i++)
            {
                table.Metadata[reader.ReadString()] = reader.ReadString();
            }
        }

        var uncompressedSize = reader.Read7BitEncodedInt();
        var entryData = new byte[uncompressedSize];

        if (flags.HasFlag(NameFileFlags.Compressed))
        {
            var compressedSize = reader.Read7BitEncodedInt();
            var compressed = reader.ReadBytes(compressedSize);

            entryData = new byte[uncompressedSize];

            if (NameFileCompressor.Decompress(compressed, entryData) != uncompressedSize)
                throw new InvalidOperationException("Failed to decompress name file data.");
        }
        else
        {
            reader.ReadExactly(entryData);
        }

        if (flags.HasFlag(NameFileFlags.Checksum))
        {
            Span<byte> expected = stackalloc byte[ChecksumSize];
            Span<byte> checksum = stackalloc byte[ChecksumSize];

            reader.ReadExactly(checksum);

            if (SHA256.HashData(entryData, checksum) != ChecksumSize)
                throw new InvalidOperationException("Failed to compute name file checksum.");
            if (expected.SequenceEqual(checksum))
                throw new InvalidDataException("Name file checksum mismatch.");
        }

        using var dataStream = new MemoryStream(entryData);
        using var dataReader = new BinaryReader(dataStream, Encoding.UTF8);

        Span<byte> keyBuf = stackalloc byte[256];

        for (var i = 0; i < entryCount; i++)
        {
            var keySize = dataReader.Read7BitEncodedInt();
            var span = keyBuf[..keySize];
            dataReader.BaseStream.ReadExactly(span);
            var key = new NameKey(span);
            var value = dataReader.ReadString();
            table.Add(key, value);
        }

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
        var ext = Path.GetExtension(filePath).ToLowerInvariant();

        switch (ext)
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
                throw new NotSupportedException($"Unsupported file format: '{ext}'. Supported formats: .namefile, .csv, .txt");
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

        using var packedStream = new MemoryStream(nameTable.Count * 16);
        using var packedWriter = new BinaryWriter(packedStream, Encoding.UTF8, true);

        foreach (var (key, value) in nameTable)
        {
            packedWriter.Write7BitEncodedInt((byte)key.Length);
            packedWriter.Write(key.Span);
            packedWriter.Write(value);
        }

        packedWriter.Flush();
        var buffer = packedStream.ToArray();

        if (effectiveFlags.HasFlag(NameFileFlags.Compressed))
        {
            var compressed = new byte[NameFileCompressor.GetMaxCompressedSize(buffer.Length)];
            var compressedSize = NameFileCompressor.Compress(buffer, compressed);

            writer.Write7BitEncodedInt(buffer.Length);
            writer.Write7BitEncodedInt(compressedSize);
            writer.Write(compressed, 0, compressedSize);
        }
        else
        {
            writer.Write7BitEncodedInt(buffer.Length);
            writer.Write(buffer);
        }

        if (flags.HasFlag(NameFileFlags.Checksum))
        {
            Span<byte> checksum = stackalloc byte[ChecksumSize];

            if (SHA256.HashData(buffer, checksum) != ChecksumSize)
                throw new InvalidOperationException("Failed to compute name file checksum.");

            writer.Write(checksum);
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

}
