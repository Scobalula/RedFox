using System.Buffers.Binary;
using System.IO.Compression;
using System.Runtime.InteropServices;
using System.Text;

namespace RedFox.Graphics3D.Formats.KaydaraFbx;

/// <summary>
/// Reads and writes the Kaydara FBX binary encoding.
/// </summary>
public static class FbxBinarySerializer
{
    /// <summary>
    /// Gets the maximum node nesting depth accepted when reading.
    /// </summary>
    public const int MaxDepth = 256;

    /// <summary>
    /// Gets the array payload size in bytes above which arrays are compressed when writing.
    /// </summary>
    public const int CompressionThreshold = 128;

    /// <summary>
    /// Gets the 16-byte footer identifier written after the root null record.
    /// </summary>
    public static readonly byte[] FooterId = [0xFA, 0xBC, 0xAB, 0x09, 0xD0, 0xC8, 0xD4, 0x66, 0xB1, 0x76, 0xFB, 0x83, 0x1C, 0xF7, 0x26, 0x7E];

    /// <summary>
    /// Gets the 16-byte magic sequence that terminates a binary FBX file.
    /// </summary>
    public static readonly byte[] FooterMagic = [0xF8, 0x5A, 0x8C, 0x6A, 0xDE, 0xF5, 0xD9, 0x7E, 0xEC, 0xE9, 0x0C, 0xE3, 0x75, 0x8F, 0x29, 0x0B];

    /// <summary>
    /// Gets the binary file signature including the trailing <c>0x1A 0x00</c> bytes.
    /// </summary>
    public static ReadOnlySpan<byte> Header => "Kaydara FBX Binary  \0\u001A\0"u8;

    /// <summary>
    /// Determines whether the provided bytes start with the binary FBX signature.
    /// </summary>
    /// <param name="data">The leading bytes of a file.</param>
    /// <returns><see langword="true"/> when the signature matches.</returns>
    public static bool IsBinary(ReadOnlySpan<byte> data)
    {
        return data.StartsWith(Header[..21]);
    }

    /// <summary>
    /// Reads a binary FBX document.
    /// </summary>
    /// <param name="data">The complete file contents.</param>
    /// <returns>The parsed document.</returns>
    public static FbxDocument Read(byte[] data)
    {
        if (data.Length < Header.Length + 4 || !IsBinary(data))
            throw new InvalidDataException("The data is not a binary FBX file.");

        using MemoryStream	stream = new(data, writable: false);
        using BinaryReader	reader = new(stream);

        bool bigEndian = data[Header.Length - 1] != 0;

        stream.Position = Header.Length;

        FbxDocument document = new()
        {
            Format	= FbxFormat.Binary,
            Version	= (int)ReadUInt32(reader, bigEndian),
        };

        FbxBinaryLayout	layout			= new(document.Version >= 7500, bigEndian);
        int				nullRecordSize	= layout.Is64Bit ? 25 : 13;

        Dictionary<string, string>.AlternateLookup<ReadOnlySpan<char>> names = new Dictionary<string, string>(StringComparer.Ordinal).GetAlternateLookup<ReadOnlySpan<char>>();

        try
        {
            while (stream.Length - stream.Position >= nullRecordSize)
            {
                FbxNode? node = ReadNode(reader, layout, names, 0);

                if (node is null)
                    break;

                document.Nodes.Add(node);
            }
        }
        catch (EndOfStreamException exception)
        {
            throw new InvalidDataException("The binary FBX data is truncated.", exception);
        }

        return document;
    }

    /// <summary>
    /// Writes a binary FBX document.
    /// </summary>
    /// <param name="stream">The destination stream.</param>
    /// <param name="document">The document to write.</param>
    public static void Write(Stream stream, FbxDocument document)
    {
        using MemoryStream	buffer = new();
        using BinaryWriter	writer = new(buffer, Encoding.UTF8, leaveOpen: true);

        bool is64Bit = document.Version >= 7500;

        writer.Write(Header);
        writer.Write(document.Version);

        for (int i = 0; i < document.Nodes.Count; i++)
            WriteNode(writer, document.Nodes[i], is64Bit, false);

        WriteNullRecord(writer, is64Bit);
        WriteFooter(writer, document.Version);
        writer.Flush();

        stream.Write(buffer.GetBuffer(), 0, (int)buffer.Length);
    }

    private static FbxNode? ReadNode(BinaryReader reader, FbxBinaryLayout layout, Dictionary<string, string>.AlternateLookup<ReadOnlySpan<char>> names, int depth)
    {
        Stream	stream			= reader.BaseStream;
        ulong	endOffset		= layout.Is64Bit ? ReadUInt64(reader, layout.BigEndian) : ReadUInt32(reader, layout.BigEndian);
        ulong	propertyCount	= layout.Is64Bit ? ReadUInt64(reader, layout.BigEndian) : ReadUInt32(reader, layout.BigEndian);
        ulong	propertyLength	= layout.Is64Bit ? ReadUInt64(reader, layout.BigEndian) : ReadUInt32(reader, layout.BigEndian);
        int		nameLength		= reader.ReadByte();

        if (endOffset == 0)
            return null;

        if (depth >= MaxDepth)
            throw new InvalidDataException("FBX node nesting is too deep.");

        if (endOffset <= (ulong)stream.Position || endOffset > (ulong)stream.Length)
            throw new InvalidDataException("FBX node end offset is out of range.");

        string	name	= ReadName(reader, nameLength, names);
        long	end		= (long)endOffset;

        if (propertyLength > (ulong)(end - stream.Position) || propertyCount > propertyLength)
            throw new InvalidDataException($"FBX node '{name}' has an invalid property list.");

        long	propertiesEnd	= stream.Position + (long)propertyLength;
        FbxNode	node			= new(name);
        node.Properties.Capacity = (int)propertyCount;

        for (ulong i = 0; i < propertyCount; i++)
            node.Properties.Add(ReadProperty(reader, layout.BigEndian, propertiesEnd, names));

        if (stream.Position > propertiesEnd)
            throw new InvalidDataException($"FBX node '{name}' properties overrun their declared length.");

        stream.Position = propertiesEnd;

        while (stream.Position < end)
        {
            FbxNode? child = ReadNode(reader, layout, names, depth + 1);

            if (child is null)
                break;

            node.Children.Add(child);
        }

        if (stream.Position > end)
            throw new InvalidDataException($"FBX node '{name}' children overrun the node.");

        stream.Position = end;
        return node;
    }

    private static string ReadName(BinaryReader reader, int length, Dictionary<string, string>.AlternateLookup<ReadOnlySpan<char>> names)
    {
        Span<byte> bytes = stackalloc byte[length];
        reader.BaseStream.ReadExactly(bytes);

        Span<char>	chars	= stackalloc char[length];
        int			count	= Encoding.UTF8.GetChars(bytes, chars);

        return Intern(names, chars[..count]);
    }

    private static string Intern(Dictionary<string, string>.AlternateLookup<ReadOnlySpan<char>> names, ReadOnlySpan<char> value)
    {
        if (names.TryGetValue(value, out string? interned))
            return interned;

        string created = value.ToString();
        names[created] = created;
        return created;
    }

    private static FbxProperty ReadProperty(BinaryReader reader, bool bigEndian, long limit, Dictionary<string, string>.AlternateLookup<ReadOnlySpan<char>> names)
    {
        FbxPropertyType type = (FbxPropertyType)reader.ReadByte();

        return type switch
        {
            FbxPropertyType.Byte	=> new FbxProperty(reader.ReadByte()),
            FbxPropertyType.Int16	=> new FbxProperty(bigEndian ? BinaryPrimitives.ReverseEndianness(reader.ReadInt16()) : reader.ReadInt16()),
            FbxPropertyType.Boolean	=> FbxProperty.CreateBoolean(reader.ReadByte()),
            FbxPropertyType.Int32	=> new FbxProperty((int)ReadUInt32(reader, bigEndian)),
            FbxPropertyType.Single	=> new FbxProperty(BitConverter.Int32BitsToSingle((int)ReadUInt32(reader, bigEndian))),
            FbxPropertyType.Double	=> new FbxProperty(BitConverter.Int64BitsToDouble((long)ReadUInt64(reader, bigEndian))),
            FbxPropertyType.Int64	=> new FbxProperty((long)ReadUInt64(reader, bigEndian)),
            FbxPropertyType.String	=> new FbxProperty(ReadString(reader, ReadLength(reader, bigEndian, limit), names)),
            FbxPropertyType.Raw		=> new FbxProperty(reader.ReadBytes(ReadLength(reader, bigEndian, limit))),
            FbxPropertyType.SingleArray or FbxPropertyType.DoubleArray or FbxPropertyType.Int32Array or FbxPropertyType.Int64Array or FbxPropertyType.BooleanArray or FbxPropertyType.ByteArray => ReadArray(reader, bigEndian, type, limit),
            _ => throw new InvalidDataException($"Unknown FBX property type code 0x{(byte)type:X2}."),
        };
    }

    private static uint ReadUInt32(BinaryReader reader, bool bigEndian)
    {
        uint value = reader.ReadUInt32();
        return bigEndian ? BinaryPrimitives.ReverseEndianness(value) : value;
    }

    private static ulong ReadUInt64(BinaryReader reader, bool bigEndian)
    {
        ulong value = reader.ReadUInt64();
        return bigEndian ? BinaryPrimitives.ReverseEndianness(value) : value;
    }

    private static int ReadLength(BinaryReader reader, bool bigEndian, long limit)
    {
        uint length = ReadUInt32(reader, bigEndian);

        if (length > limit - reader.BaseStream.Position)
            throw new InvalidDataException("FBX property length exceeds the node.");

        return (int)length;
    }

    private static string ReadString(BinaryReader reader, int length, Dictionary<string, string>.AlternateLookup<ReadOnlySpan<char>> names)
    {
        if (length > 64)
            return Encoding.UTF8.GetString(reader.ReadBytes(length));

        Span<byte> bytes = stackalloc byte[length];
        reader.BaseStream.ReadExactly(bytes);

        Span<char>	chars	= stackalloc char[length];
        int			count	= Encoding.UTF8.GetChars(bytes, chars);

        return Intern(names, chars[..count]);
    }

    private static FbxProperty ReadArray(BinaryReader reader, bool bigEndian, FbxPropertyType type, long limit)
    {
        Stream	stream				= reader.BaseStream;
        uint	count				= ReadUInt32(reader, bigEndian);
        uint	encoding			= ReadUInt32(reader, bigEndian);
        uint	compressedLength	= ReadUInt32(reader, bigEndian);
        long	start				= stream.Position;
        long	byteLength			= (long)count * GetElementSize(type);

        if (compressedLength > limit - start)
            throw new InvalidDataException("FBX array payload exceeds the node.");

        if (byteLength > Array.MaxLength || (encoding == 0 && byteLength > compressedLength) || (encoding == 1 && byteLength > (long)compressedLength * 1032 + 64))
            throw new InvalidDataException("FBX array length is inconsistent with its payload.");

        Array array = type switch
        {
            FbxPropertyType.SingleArray		=> new float[count],
            FbxPropertyType.DoubleArray		=> new double[count],
            FbxPropertyType.Int32Array		=> new int[count],
            FbxPropertyType.Int64Array		=> new long[count],
            FbxPropertyType.BooleanArray	=> new bool[count],
            _								=> new byte[count],
        };

        Span<byte> destination = MemoryMarshal.CreateSpan(ref MemoryMarshal.GetArrayDataReference(array), (int)byteLength);

        if (encoding == 0)
        {
            stream.ReadExactly(destination);
        }
        else if (encoding == 1)
        {
            using ZLibStream zlib = new(stream, CompressionMode.Decompress, leaveOpen: true);
            zlib.ReadExactly(destination);
        }
        else
        {
            throw new InvalidDataException($"Unknown FBX array encoding {encoding}.");
        }

        stream.Position = start + compressedLength;

        if (bigEndian && GetElementSize(type) == 4)
            BinaryPrimitives.ReverseEndianness(MemoryMarshal.Cast<byte, int>(destination), MemoryMarshal.Cast<byte, int>(destination));
        else if (bigEndian && GetElementSize(type) == 8)
            BinaryPrimitives.ReverseEndianness(MemoryMarshal.Cast<byte, long>(destination), MemoryMarshal.Cast<byte, long>(destination));

        return array switch
        {
            float[] values	=> new FbxProperty(values),
            double[] values	=> new FbxProperty(values),
            int[] values	=> new FbxProperty(values),
            long[] values	=> new FbxProperty(values),
            bool[] values	=> new FbxProperty(NormalizeBooleans(values, destination)),
            _				=> FbxProperty.CreateByteArray((byte[])array),
        };
    }

    private static bool[] NormalizeBooleans(bool[] values, Span<byte> bytes)
    {
        for (int i = 0; i < bytes.Length; i++)
            bytes[i] = bytes[i] != 0 ? (byte)1 : (byte)0;

        return values;
    }

    private static int GetElementSize(FbxPropertyType type)
    {
        return type switch
        {
            FbxPropertyType.SingleArray or FbxPropertyType.Int32Array => 4,
            FbxPropertyType.DoubleArray or FbxPropertyType.Int64Array => 8,
            _ => 1,
        };
    }

    private static void WriteNode(BinaryWriter writer, FbxNode node, bool is64Bit, bool isLast)
    {
        Stream	stream	= writer.BaseStream;
        long	start	= stream.Position;
        byte[]	name	= Encoding.UTF8.GetBytes(node.Name);

        if (name.Length > byte.MaxValue)
            throw new InvalidDataException($"FBX node name '{node.Name}' is longer than 255 bytes.");

        WriteRecordHeader(writer, is64Bit, 0, 0, 0);
        writer.Write((byte)name.Length);
        writer.Write(name);

        long propertiesStart = stream.Position;

        foreach (FbxProperty property in node.Properties)
            WriteProperty(writer, property);

        long propertiesLength = stream.Position - propertiesStart;

        if (node.Children.Count > 0)
        {
            for (int i = 0; i < node.Children.Count; i++)
                WriteNode(writer, node.Children[i], is64Bit, i == node.Children.Count - 1);

            WriteNullRecord(writer, is64Bit);
        }
        else if (node.Properties.Count == 0 && !isLast)
        {
            WriteNullRecord(writer, is64Bit);
        }

        long end = stream.Position;

        if (!is64Bit && end > uint.MaxValue)
            throw new InvalidDataException("FBX files larger than 4 GB require version 7500 or later.");

        stream.Position = start;
        WriteRecordHeader(writer, is64Bit, end, node.Properties.Count, propertiesLength);
        stream.Position = end;
    }

    private static void WriteRecordHeader(BinaryWriter writer, bool is64Bit, long endOffset, long propertyCount, long propertyLength)
    {
        if (is64Bit)
        {
            writer.Write((ulong)endOffset);
            writer.Write((ulong)propertyCount);
            writer.Write((ulong)propertyLength);
        }
        else
        {
            writer.Write((uint)endOffset);
            writer.Write((uint)propertyCount);
            writer.Write((uint)propertyLength);
        }
    }

    private static void WriteNullRecord(BinaryWriter writer, bool is64Bit)
    {
        writer.Write(new byte[is64Bit ? 25 : 13]);
    }

    private static void WriteProperty(BinaryWriter writer, FbxProperty property)
    {
        writer.Write((byte)property.Type);

        switch (property.Type)
        {
            case FbxPropertyType.Byte:
                writer.Write((byte)property.AsInt64());
                break;
            case FbxPropertyType.Int16:
                writer.Write((short)property.AsInt64());
                break;
            case FbxPropertyType.Boolean:
                writer.Write((byte)property.AsInt64());
                break;
            case FbxPropertyType.Int32:
                writer.Write(property.AsInt32());
                break;
            case FbxPropertyType.Single:
                writer.Write(property.AsSingle());
                break;
            case FbxPropertyType.Double:
                writer.Write(property.AsDouble());
                break;
            case FbxPropertyType.Int64:
                writer.Write(property.AsInt64());
                break;
            case FbxPropertyType.String:
            case FbxPropertyType.Raw:
                byte[] bytes = property.AsBytes();
                writer.Write(bytes.Length);
                writer.Write(bytes);
                break;
            default:
                WriteArray(writer, (Array)property.Value, GetElementSize(property.Type));
                break;
        }
    }

    private static void WriteArray(BinaryWriter writer, Array array, int elementSize)
    {
        ReadOnlySpan<byte> bytes = MemoryMarshal.CreateReadOnlySpan(ref MemoryMarshal.GetArrayDataReference(array), array.Length * elementSize);

        writer.Write(array.Length);

        if (bytes.Length <= CompressionThreshold)
        {
            writer.Write(0);
            writer.Write(bytes.Length);
            writer.Write(bytes);
            return;
        }

        Stream	stream			= writer.BaseStream;
        long	lengthPosition	= stream.Position + 4;

        writer.Write(1);
        writer.Write(0);
        writer.Flush();

        long dataStart = stream.Position;

        using (ZLibStream zlib = new(stream, CompressionLevel.Fastest, leaveOpen: true))
            zlib.Write(bytes);

        long dataEnd = stream.Position;

        stream.Position = lengthPosition;
        writer.Write((int)(dataEnd - dataStart));
        stream.Position = dataEnd;
    }

    private static void WriteFooter(BinaryWriter writer, int version)
    {
        writer.Write(FooterId);
        writer.Write(0);

        int offset	= (int)(writer.BaseStream.Position & 0x0F);
        int padding	= ((offset + 15) & ~0x0F) - offset;

        writer.Write(new byte[padding == 0 ? 16 : padding]);
        writer.Write(version);
        writer.Write(new byte[120]);
        writer.Write(FooterMagic);
    }
}
