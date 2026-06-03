using System.Security.Cryptography;
using System.Text;

namespace RedFox.GameExtraction.Hashing;

public static class NameFile
{
    
    const ulong MagicNumber = 0x454C494648534148;
    
    /// <summary>
    /// Loads a <see cref="NameTable"/> from the provided file.
    /// </summary>
    /// <param name="filePath">The file to load from.</param>
    /// <returns>Resulting <see cref="NameTable"/>.</returns>
    public static NameTable Load(string filePath)
    {
        using var stream = File.OpenRead(filePath);
        return Load(stream);
    }

    /// <summary>
    /// Loads a <see cref="NameTable"/> from the provided stream.
    /// </summary>
    /// <param name="stream">The stream to load from.</param>
    /// <returns>Resulting <see cref="NameTable"/>.</returns>
    public static NameTable Load(Stream stream)
    {
        using var reader = new BinaryReader(stream, Encoding.UTF8, true);
        return Load(reader);
    }

    /// <summary>
    /// Loads a <see cref="NameTable"/> from the provided file.
    /// </summary>
    /// <param name="reader">The reader to load from.</param>
    /// <returns>Resulting <see cref="NameTable"/>.</returns>
    public static NameTable Load(BinaryReader reader)
    {
        var magic = reader.ReadUInt64();

        if (magic != MagicNumber)
            throw new NotSupportedException($"Invalid magic number: {magic}");
        
        var flags = reader.ReadUInt64();
        var hashCount = reader.ReadInt32();
        var hashAlgorithm = reader.ReadString();
        
        throw new NotImplementedException();
    }

    public static void Save(string filePath, NameTable nameTable)
    {
        using var  stream = File.OpenWrite(filePath);
        Save(stream, nameTable);
    }

    public static void Save(Stream stream, NameTable nameTable)
    {
        using  var writer = new BinaryWriter(stream, Encoding.UTF8, true);
        Save(writer, nameTable);
    }

    public static void Save(BinaryWriter writer, NameTable nameTable)
    {
        writer.Write(MagicNumber);
        writer.Write((ulong)0); // No flags for now
        
        writer.Write(nameTable.HashCount);
        writer.Write(nameTable.HashAlgorithm);
    }
}