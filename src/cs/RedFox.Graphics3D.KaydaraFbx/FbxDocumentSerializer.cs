using System.Globalization;
using System.IO.Compression;
using System.Text;

namespace RedFox.Graphics3D.KaydaraFbx;

/// <summary>
/// Provides low-level FBX ASCII and binary serialization routines.
/// </summary>
public static class FbxDocumentSerializer
{
    public static readonly byte[] s_binaryHeader = [
        (byte)'K', (byte)'a', (byte)'y', (byte)'d', (byte)'a', (byte)'r', (byte)'a', (byte)' ',
        (byte)'F', (byte)'B', (byte)'X', (byte)' ', (byte)'B', (byte)'i', (byte)'n', (byte)'a',
        (byte)'r', (byte)'y', (byte)' ', (byte)' ', 0x00, 0x1A, 0x00,
    ];

    /// <summary>
    /// Determines whether the provided bytes start with the Kaydara FBX binary header.
    /// </summary>
    /// <param name="header">The candidate header bytes.</param>
    /// <returns><see langword="true"/> when the header matches the FBX binary signature.</returns>
    public static bool IsBinaryHeader(ReadOnlySpan<byte> header)
    {
        if (header.Length < s_binaryHeader.Length)
        {
            return false;
        }

        return header[..s_binaryHeader.Length].SequenceEqual(s_binaryHeader);
    }

    /// <summary>
    /// Reads an FBX document from a stream, auto-detecting ASCII or binary representation.
    /// </summary>
    /// <param name="stream">The source stream.</param>
    /// <returns>The parsed document.</returns>
    public static FbxDocument Read(Stream stream)
    {
        ArgumentNullException.ThrowIfNull(stream);

        if (!stream.CanRead)
        {
            throw new InvalidOperationException("Cannot read FBX document from a non-readable stream.");
        }

        Stream workingStream = stream;
        bool disposeWorkingStream = false;
        if (!stream.CanSeek)
        {
            MemoryStream copy = new();
            stream.CopyTo(copy);
            copy.Position = 0;
            workingStream = copy;
            disposeWorkingStream = true;
        }

        try
        {
            long originalPosition = workingStream.Position;
            Span<byte> probe = stackalloc byte[s_binaryHeader.Length];
            int read = workingStream.Read(probe);
            workingStream.Position = originalPosition;

            if (read == s_binaryHeader.Length && IsBinaryHeader(probe))
            {
                return ReadBinary(workingStream);
            }

            return ReadAscii(workingStream);
        }
        finally
        {
            if (disposeWorkingStream)
            {
                workingStream.Dispose();
            }
        }
    }

    /// <summary>
    /// Writes an FBX document to a stream in the selected representation.
    /// </summary>
    /// <param name="stream">The destination stream.</param>
    /// <param name="document">The document to serialize.</param>
    /// <param name="format">The target representation.</param>
    public static void Write(Stream stream, FbxDocument document, FbxFormat format)
    {
        ArgumentNullException.ThrowIfNull(stream);
        ArgumentNullException.ThrowIfNull(document);

        if (format == FbxFormat.Binary)
        {
            WriteBinary(stream, document);
            return;
        }

        WriteAscii(stream, document);
    }

    /// <summary>
    /// Reads a binary FBX document from a stream that is already positioned past the format header.
    /// </summary>
    /// <param name="stream">A seekable, readable stream positioned at the start of the file.</param>
    /// <returns>The parsed FBX document.</returns>
    public static FbxDocument ReadBinary(Stream stream)
    {
        using BinaryReader reader = new(stream, Encoding.UTF8, leaveOpen: true);

        byte[] header = reader.ReadBytes(s_binaryHeader.Length);
        if (!header.AsSpan().SequenceEqual(s_binaryHeader))
        {
            throw new InvalidDataException("Invalid FBX binary header.");
        }

        int version = reader.ReadInt32();
        bool is64BitNodeRecords = version >= 7500;

        FbxDocument document = new()
        {
            Format = FbxFormat.Binary,
            Version = version,
        };

        while (stream.Position < stream.Length)
        {
            FbxNode? node = ReadBinaryNode(reader, stream, is64BitNodeRecords);
            if (node is null)
            {
                break;
            }

            document.Nodes.Add(node);
        }

        return document;
    }

    /// <summary>
    /// Reads a single FBX node record from a binary stream.
    /// </summary>
    /// <param name="reader">The binary reader positioned at the start of the node record.</param>
    /// <param name="stream">The backing stream used for seek-back operations.</param>
    /// <param name="is64BitNodeRecords">Whether the file uses 64-bit offset headers (FBX 7.5+).</param>
    /// <returns>The parsed node, or <see langword="null"/> when a null record is encountered.</returns>
    public static FbxNode? ReadBinaryNode(BinaryReader reader, Stream stream, bool is64BitNodeRecords)
    {
        ulong endOffset;
        ulong propertyCount;
        ulong propertyListLength;
        byte nameLength;

        if (is64BitNodeRecords)
        {
            endOffset = reader.ReadUInt64();
            propertyCount = reader.ReadUInt64();
            propertyListLength = reader.ReadUInt64();
            nameLength = reader.ReadByte();
        }
        else
        {
            endOffset = reader.ReadUInt32();
            propertyCount = reader.ReadUInt32();
            propertyListLength = reader.ReadUInt32();
            nameLength = reader.ReadByte();
        }

        if (endOffset == 0 && propertyCount == 0 && propertyListLength == 0 && nameLength == 0)
        {
            return null;
        }

        string name = Encoding.UTF8.GetString(reader.ReadBytes(nameLength));
        FbxNode node = new(name);

        for (ulong i = 0; i < propertyCount; i++)
        {
            node.Properties.Add(ReadBinaryProperty(reader));
        }

        while ((ulong)stream.Position < endOffset)
        {
            long beforeChildRead = stream.Position;
            FbxNode? child = ReadBinaryNode(reader, stream, is64BitNodeRecords);
            if (child is null)
            {
                break;
            }

            if (stream.Position <= beforeChildRead)
            {
                throw new InvalidDataException("Invalid FBX node structure detected while parsing child records.");
            }

            node.Children.Add(child);
        }

        stream.Position = (long)endOffset;
        return node;
    }

    /// <summary>
    /// Reads a single typed property from the binary FBX stream.
    /// </summary>
    /// <param name="reader">The binary reader positioned at the property type byte.</param>
    /// <returns>The parsed <see cref="FbxProperty"/>.</returns>
    public static FbxProperty ReadBinaryProperty(BinaryReader reader)
    {
        char typeCode = (char)reader.ReadByte();

        object value = typeCode switch
        {
            'Y' => reader.ReadInt16(),
            'C' => reader.ReadByte() != 0,
            'I' => reader.ReadInt32(),
            'F' => reader.ReadSingle(),
            'D' => reader.ReadDouble(),
            'L' => reader.ReadInt64(),
            'S' => ReadLengthPrefixedString(reader),
            'R' => ReadLengthPrefixedBytes(reader),
            'f' => ReadArrayProperty(reader, sizeof(float), static (span, count) => ReadFloatArray(span, count)),
            'd' => ReadArrayProperty(reader, sizeof(double), static (span, count) => ReadDoubleArray(span, count)),
            'l' => ReadArrayProperty(reader, sizeof(long), static (span, count) => ReadInt64Array(span, count)),
            'i' => ReadArrayProperty(reader, sizeof(int), static (span, count) => ReadInt32Array(span, count)),
            'b' => ReadArrayProperty(reader, sizeof(byte), static (span, count) => ReadBoolArray(span, count)),
            _ => throw new InvalidDataException($"Unsupported FBX property type '{typeCode}'."),
        };

        return new FbxProperty(typeCode, value);
    }

    public delegate object ArrayFactory(ReadOnlySpan<byte> bytes, int count);

    /// <summary>Reads and optionally decompresses an FBX binary array property.</summary>
    /// <param name="reader">The binary reader positioned at the array header.</param>
    /// <param name="elementSize">The byte size of each element.</param>
    /// <param name="factory">The delegate that converts raw bytes into a typed array object.</param>
    /// <returns>The decoded array object.</returns>
    public static object ReadArrayProperty(BinaryReader reader, int elementSize, ArrayFactory factory)
    {
        int arrayLength = reader.ReadInt32();
        int encoding = reader.ReadInt32();
        int compressedLength = reader.ReadInt32();

        if (arrayLength < 0)
        {
            throw new InvalidDataException("Invalid FBX array length.");
        }

        int expectedByteLength = checked(arrayLength * elementSize);
        byte[] rawBytes;

        if (encoding == 0)
        {
            rawBytes = reader.ReadBytes(expectedByteLength);
            if (rawBytes.Length != expectedByteLength)
            {
                throw new EndOfStreamException("Unexpected end of stream while reading FBX array property.");
            }
        }
        else
        {
            byte[] compressed = reader.ReadBytes(compressedLength);
            if (compressed.Length != compressedLength)
            {
                throw new EndOfStreamException("Unexpected end of stream while reading compressed FBX array property.");
            }

            rawBytes = DecompressArray(compressed, expectedByteLength);
        }

        return factory(rawBytes, arrayLength);
    }

    /// <summary>
    /// Decompresses a zlib- or deflate-compressed byte array used in FBX binary array properties.
    /// </summary>
    /// <param name="compressed">The compressed input bytes.</param>
    /// <param name="expectedByteLength">The expected decompressed byte count.</param>
    /// <returns>The decompressed byte array trimmed to <paramref name="expectedByteLength"/>.</returns>
    public static byte[] DecompressArray(byte[] compressed, int expectedByteLength)
    {
        using MemoryStream input = new(compressed, writable: false);
        using MemoryStream output = new(expectedByteLength);

        bool successWithZlib = TryDecompressWithZlib(input, output);
        if (!successWithZlib)
        {
            input.Position = 0;
            output.SetLength(0);
            TryDecompressWithDeflate(input, output);
        }

        byte[] bytes = output.ToArray();
        if (bytes.Length < expectedByteLength)
        {
            throw new InvalidDataException("Compressed FBX array produced fewer bytes than expected.");
        }

        if (bytes.Length == expectedByteLength)
        {
            return bytes;
        }

        byte[] exact = new byte[expectedByteLength];
        Array.Copy(bytes, exact, expectedByteLength);
        return exact;
    }

    public static bool TryDecompressWithZlib(Stream input, Stream output)
    {
        try
        {
            using ZLibStream zlib = new(input, CompressionMode.Decompress, leaveOpen: true);
            zlib.CopyTo(output);
            return true;
        }
        catch
        {
            return false;
        }
    }

    public static void TryDecompressWithDeflate(Stream input, Stream output)
    {
        using DeflateStream deflate = new(input, CompressionMode.Decompress, leaveOpen: true);
        deflate.CopyTo(output);
    }

    public static float[] ReadFloatArray(ReadOnlySpan<byte> bytes, int count)
    {
        float[] values = new float[count];
        for (int i = 0; i < count; i++)
        {
            values[i] = BitConverter.ToSingle(bytes.Slice(i * sizeof(float), sizeof(float)));
        }

        return values;
    }

    public static double[] ReadDoubleArray(ReadOnlySpan<byte> bytes, int count)
    {
        double[] values = new double[count];
        for (int i = 0; i < count; i++)
        {
            values[i] = BitConverter.ToDouble(bytes.Slice(i * sizeof(double), sizeof(double)));
        }

        return values;
    }

    public static long[] ReadInt64Array(ReadOnlySpan<byte> bytes, int count)
    {
        long[] values = new long[count];
        for (int i = 0; i < count; i++)
        {
            values[i] = BitConverter.ToInt64(bytes.Slice(i * sizeof(long), sizeof(long)));
        }

        return values;
    }

    public static int[] ReadInt32Array(ReadOnlySpan<byte> bytes, int count)
    {
        int[] values = new int[count];
        for (int i = 0; i < count; i++)
        {
            values[i] = BitConverter.ToInt32(bytes.Slice(i * sizeof(int), sizeof(int)));
        }

        return values;
    }

    public static bool[] ReadBoolArray(ReadOnlySpan<byte> bytes, int count)
    {
        bool[] values = new bool[count];
        for (int i = 0; i < count; i++)
        {
            values[i] = bytes[i] != 0;
        }

        return values;
    }

    public static string ReadLengthPrefixedString(BinaryReader reader)
    {
        int length = reader.ReadInt32();
        byte[] bytes = reader.ReadBytes(length);
        if (bytes.Length != length)
        {
            throw new EndOfStreamException("Unexpected end of stream while reading FBX string property.");
        }

        return Encoding.UTF8.GetString(bytes);
    }

    public static byte[] ReadLengthPrefixedBytes(BinaryReader reader)
    {
        int length = reader.ReadInt32();
        byte[] bytes = reader.ReadBytes(length);
        if (bytes.Length != length)
        {
            throw new EndOfStreamException("Unexpected end of stream while reading FBX raw property.");
        }

        return bytes;
    }

    /// <summary>
    /// Writes an FBX document to a stream using binary encoding.
    /// </summary>
    /// <param name="stream">A seekable, writable destination stream.</param>
    /// <param name="document">The document to write.</param>
    public static void WriteBinary(Stream stream, FbxDocument document)
    {
        if (!stream.CanWrite)
        {
            throw new InvalidOperationException("Cannot write FBX document to a non-writable stream.");
        }

        if (!stream.CanSeek)
        {
            throw new InvalidOperationException("Binary FBX writing requires a seekable stream.");
        }

        using BinaryWriter writer = new(stream, Encoding.UTF8, leaveOpen: true);

        writer.Write(s_binaryHeader);
        writer.Write(document.Version <= 0 ? 7400 : document.Version);

        bool is64BitNodeRecords = document.Version >= 7500;

        foreach (FbxNode node in document.Nodes)
        {
            WriteBinaryNode(writer, stream, node, is64BitNodeRecords);
        }

        WriteBinaryNullRecord(writer, is64BitNodeRecords);
    }

    /// <summary>
    /// Writes a single FBX node and its children to the binary stream.
    /// </summary>
    /// <param name="writer">The binary writer.</param>
    /// <param name="stream">The backing stream used for seek-back operations.</param>
    /// <param name="node">The node to serialise.</param>
    /// <param name="is64BitNodeRecords">Whether to use 64-bit node record headers.</param>
    public static void WriteBinaryNode(BinaryWriter writer, Stream stream, FbxNode node, bool is64BitNodeRecords)
    {
        long recordStart = stream.Position;

        if (is64BitNodeRecords)
        {
            writer.Write(0UL);
            writer.Write((ulong)node.Properties.Count);
            writer.Write(0UL);
            writer.Write((byte)node.Name.Length);
        }
        else
        {
            writer.Write(0U);
            writer.Write((uint)node.Properties.Count);
            writer.Write(0U);
            writer.Write((byte)node.Name.Length);
        }

        byte[] nameBytes = Encoding.UTF8.GetBytes(node.Name);
        writer.Write(nameBytes);

        long propertyListStart = stream.Position;
        foreach (FbxProperty property in node.Properties)
        {
            WriteBinaryProperty(writer, property);
        }

        long propertyListEnd = stream.Position;
        ulong propertyListLength = (ulong)(propertyListEnd - propertyListStart);

        foreach (FbxNode child in node.Children)
        {
            WriteBinaryNode(writer, stream, child, is64BitNodeRecords);
        }

        WriteBinaryNullRecord(writer, is64BitNodeRecords);
        long endOffset = stream.Position;

        stream.Position = recordStart;
        if (is64BitNodeRecords)
        {
            writer.Write((ulong)endOffset);
            writer.Write((ulong)node.Properties.Count);
            writer.Write(propertyListLength);
            writer.Write((byte)node.Name.Length);
        }
        else
        {
            writer.Write((uint)endOffset);
            writer.Write((uint)node.Properties.Count);
            writer.Write((uint)propertyListLength);
            writer.Write((byte)node.Name.Length);
        }

        writer.Write(nameBytes);
        stream.Position = endOffset;
    }

    /// <summary>
    /// Writes an all-zero FBX node record used as a sentinel at the end of a scope.
    /// </summary>
    /// <param name="writer">The binary writer.</param>
    /// <param name="is64BitNodeRecords">Whether to write a 64-bit null record.</param>
    public static void WriteBinaryNullRecord(BinaryWriter writer, bool is64BitNodeRecords)
    {
        if (is64BitNodeRecords)
        {
            writer.Write(0UL);
            writer.Write(0UL);
            writer.Write(0UL);
            writer.Write((byte)0);
            return;
        }

        writer.Write(0U);
        writer.Write(0U);
        writer.Write(0U);
        writer.Write((byte)0);
    }

    /// <summary>
    /// Writes a single FBX property value to the binary stream.
    /// </summary>
    /// <param name="writer">The binary writer.</param>
    /// <param name="property">The property to write.</param>
    public static void WriteBinaryProperty(BinaryWriter writer, FbxProperty property)
    {
        writer.Write((byte)property.TypeCode);

        switch (property.TypeCode)
        {
            case 'Y':
                writer.Write(Convert.ToInt16(property.Value, CultureInfo.InvariantCulture));
                return;
            case 'C':
                writer.Write((byte)(Convert.ToBoolean(property.Value, CultureInfo.InvariantCulture) ? 1 : 0));
                return;
            case 'I':
                writer.Write(Convert.ToInt32(property.Value, CultureInfo.InvariantCulture));
                return;
            case 'F':
                writer.Write(Convert.ToSingle(property.Value, CultureInfo.InvariantCulture));
                return;
            case 'D':
                writer.Write(Convert.ToDouble(property.Value, CultureInfo.InvariantCulture));
                return;
            case 'L':
                writer.Write(Convert.ToInt64(property.Value, CultureInfo.InvariantCulture));
                return;
            case 'S':
            {
                byte[] utf8 = Encoding.UTF8.GetBytes(property.AsString());
                writer.Write(utf8.Length);
                writer.Write(utf8);
                return;
            }
            case 'R':
            {
                byte[] bytes = property.Value as byte[] ?? [];
                writer.Write(bytes.Length);
                writer.Write(bytes);
                return;
            }
            case 'f':
                WriteArrayProperty(writer, property.Value as float[] ?? []);
                return;
            case 'd':
                WriteArrayProperty(writer, property.Value as double[] ?? []);
                return;
            case 'l':
                WriteArrayProperty(writer, property.Value as long[] ?? []);
                return;
            case 'i':
                WriteArrayProperty(writer, property.Value as int[] ?? []);
                return;
            case 'b':
                WriteArrayProperty(writer, property.Value as bool[] ?? []);
                return;
            default:
                throw new InvalidDataException($"Unsupported FBX property type '{property.TypeCode}' for binary writing.");
        }
    }

    public static void WriteArrayProperty(BinaryWriter writer, float[] values)
    {
        writer.Write(values.Length);
        writer.Write(0);
        writer.Write(values.Length * sizeof(float));

        for (int i = 0; i < values.Length; i++)
        {
            writer.Write(values[i]);
        }
    }

    public static void WriteArrayProperty(BinaryWriter writer, double[] values)
    {
        writer.Write(values.Length);
        writer.Write(0);
        writer.Write(values.Length * sizeof(double));

        for (int i = 0; i < values.Length; i++)
        {
            writer.Write(values[i]);
        }
    }

    public static void WriteArrayProperty(BinaryWriter writer, long[] values)
    {
        writer.Write(values.Length);
        writer.Write(0);
        writer.Write(values.Length * sizeof(long));

        for (int i = 0; i < values.Length; i++)
        {
            writer.Write(values[i]);
        }
    }

    public static void WriteArrayProperty(BinaryWriter writer, int[] values)
    {
        writer.Write(values.Length);
        writer.Write(0);
        writer.Write(values.Length * sizeof(int));

        for (int i = 0; i < values.Length; i++)
        {
            writer.Write(values[i]);
        }
    }

    public static void WriteArrayProperty(BinaryWriter writer, bool[] values)
    {
        writer.Write(values.Length);
        writer.Write(0);
        writer.Write(values.Length);

        for (int i = 0; i < values.Length; i++)
        {
            writer.Write((byte)(values[i] ? 1 : 0));
        }
    }

    /// <summary>
    /// Reads an ASCII FBX document from the provided text stream.
    /// </summary>
    /// <param name="stream">A readable text stream containing ASCII FBX data.</param>
    /// <returns>The parsed FBX document.</returns>
    public static FbxDocument ReadAscii(Stream stream)
    {
        using StreamReader reader = new(stream, Encoding.UTF8, detectEncodingFromByteOrderMarks: true, leaveOpen: true);
        string text = reader.ReadToEnd();

        FbxAsciiTokenizer tokenizer = new(text);
        FbxDocument document = new()
        {
            Format = FbxFormat.Ascii,
            Version = 7400,
        };

        while (!tokenizer.IsEnd)
        {
            if (tokenizer.TryConsumeCommentOrWhitespace())
            {
                continue;
            }

            FbxNode? node = ParseAsciiNode(tokenizer);
            if (node is not null)
            {
                document.Nodes.Add(node);
                continue;
            }

            tokenizer.AdvanceOne();
        }

        return document;
    }

    /// <summary>
    /// Parses a single FBX node from the ASCII tokenizer, including its properties and children.
    /// </summary>
    /// <param name="tokenizer">The tokenizer positioned at the start of the node.</param>
    /// <returns>The parsed node, or <see langword="null"/> when no node was found.</returns>
    public static FbxNode? ParseAsciiNode(FbxAsciiTokenizer tokenizer)
    {
        string? nodeName = tokenizer.ReadIdentifier();
        if (string.IsNullOrWhiteSpace(nodeName))
        {
            return null;
        }

        if (!tokenizer.TryConsume(':'))
        {
            return null;
        }

        FbxNode node = new(nodeName);

        while (!tokenizer.IsEnd)
        {
            tokenizer.TryConsumeCommentOrWhitespace();

            if (tokenizer.PeekChar() == '{')
            {
                tokenizer.AdvanceOne();
                break;
            }

            if (tokenizer.PeekChar() == '\n' || tokenizer.PeekChar() == '\r')
            {
                tokenizer.ConsumeLineEndings();
                break;
            }

            FbxProperty? property = ParseAsciiProperty(tokenizer);
            if (property is not null)
            {
                node.Properties.Add(property);
            }

            tokenizer.TryConsume(',');
            tokenizer.TryConsumeCommentOrWhitespace();
        }

        if (!tokenizer.LastOpenedBlock)
        {
            return node;
        }

        while (!tokenizer.IsEnd)
        {
            tokenizer.TryConsumeCommentOrWhitespace();
            if (tokenizer.TryConsume('}'))
            {
                break;
            }

            FbxNode? child = ParseAsciiNode(tokenizer);
            if (child is not null)
            {
                node.Children.Add(child);
                continue;
            }

            tokenizer.AdvanceOne();
        }

        return node;
    }

    /// <summary>
    /// Parses a single property value from the ASCII tokenizer.
    /// </summary>
    /// <param name="tokenizer">The tokenizer positioned at the start of the property.</param>
    /// <returns>The parsed <see cref="FbxProperty"/>, or <see langword="null"/> when no property was found.</returns>
    public static FbxProperty? ParseAsciiProperty(FbxAsciiTokenizer tokenizer)
    {
        tokenizer.TryConsumeCommentOrWhitespace();

        if (tokenizer.TryConsume('*'))
        {
            string? countText = tokenizer.ReadNumberToken();
            _ = int.TryParse(countText, NumberStyles.Integer, CultureInfo.InvariantCulture, out int _);
            tokenizer.TryConsumeCommentOrWhitespace();
            if (!tokenizer.TryConsume('{'))
            {
                return null;
            }

            tokenizer.TryConsumeCommentOrWhitespace();
            string? arrayPrefix = tokenizer.ReadIdentifier();
            if (!string.Equals(arrayPrefix, "a", StringComparison.OrdinalIgnoreCase))
            {
                return null;
            }

            tokenizer.TryConsumeCommentOrWhitespace();
            tokenizer.TryConsume(':');

            List<string> values = [];
            while (!tokenizer.IsEnd)
            {
                tokenizer.TryConsumeCommentOrWhitespace();
                if (tokenizer.PeekChar() == '}')
                {
                    tokenizer.AdvanceOne();
                    break;
                }

                string? token = tokenizer.ReadNumberToken();
                if (string.IsNullOrWhiteSpace(token))
                {
                    token = tokenizer.ReadIdentifier();
                }

                if (!string.IsNullOrWhiteSpace(token))
                {
                    values.Add(token);
                }

                tokenizer.TryConsume(',');
            }

            return InferArrayProperty(values);
        }

        if (tokenizer.PeekChar() == '"')
        {
            string text = tokenizer.ReadQuotedString();
            return new FbxProperty('S', text);
        }

        string? number = tokenizer.ReadNumberToken();
        if (!string.IsNullOrWhiteSpace(number))
        {
            if (number.Contains('.') || number.Contains('e', StringComparison.OrdinalIgnoreCase))
            {
                double floating = double.Parse(number, CultureInfo.InvariantCulture);
                return new FbxProperty('D', floating);
            }

            long integer = long.Parse(number, CultureInfo.InvariantCulture);
            return new FbxProperty('L', integer);
        }

        string? identifier = tokenizer.ReadIdentifier();
        if (string.IsNullOrWhiteSpace(identifier))
        {
            return null;
        }

        if (string.Equals(identifier, "Y", StringComparison.OrdinalIgnoreCase)
            || string.Equals(identifier, "T", StringComparison.OrdinalIgnoreCase)
            || string.Equals(identifier, "true", StringComparison.OrdinalIgnoreCase))
        {
            return new FbxProperty('C', true);
        }

        if (string.Equals(identifier, "N", StringComparison.OrdinalIgnoreCase)
            || string.Equals(identifier, "F", StringComparison.OrdinalIgnoreCase)
            || string.Equals(identifier, "false", StringComparison.OrdinalIgnoreCase))
        {
            return new FbxProperty('C', false);
        }

        return new FbxProperty('S', identifier);
    }

    /// <summary>
    /// Infers the most specific FBX property type for a list of ASCII token strings.
    /// </summary>
    /// <param name="values">The string token list representing the array elements.</param>
    /// <returns>A typed <see cref="FbxProperty"/> wrapping the inferred array.</returns>
    public static FbxProperty InferArrayProperty(List<string> values)
    {
        if (values.Count == 0)
        {
            return new FbxProperty('i', Array.Empty<int>());
        }

        bool allIntegers = true;
        bool allNumeric = true;

        for (int i = 0; i < values.Count; i++)
        {
            string value = values[i];
            if (!double.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out double _))
            {
                allNumeric = false;
                allIntegers = false;
                break;
            }

            if (!long.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out long _))
            {
                allIntegers = false;
            }
        }

        if (!allNumeric)
        {
            string joined = string.Join(",", values);
            return new FbxProperty('S', joined);
        }

        if (allIntegers)
        {
            int[] ints = new int[values.Count];
            for (int i = 0; i < values.Count; i++)
            {
                ints[i] = int.Parse(values[i], CultureInfo.InvariantCulture);
            }

            return new FbxProperty('i', ints);
        }

        double[] doubles = new double[values.Count];
        for (int i = 0; i < values.Count; i++)
        {
            doubles[i] = double.Parse(values[i], CultureInfo.InvariantCulture);
        }

        return new FbxProperty('d', doubles);
    }

    /// <summary>
    /// Writes an FBX document to a stream using ASCII text encoding.
    /// </summary>
    /// <param name="stream">A writable destination stream.</param>
    /// <param name="document">The document to write.</param>
    public static void WriteAscii(Stream stream, FbxDocument document)
    {
        using StreamWriter writer = new(stream, new UTF8Encoding(false), leaveOpen: true);

        writer.WriteLine("; FBX 7.4.0 project file");
        writer.WriteLine("; Generated by RedFox");

        foreach (FbxNode node in document.Nodes)
        {
            WriteAsciiNode(writer, node, 0);
        }
    }

    public static void WriteAsciiNode(StreamWriter writer, FbxNode node, int indent)
    {
        string indentText = new(' ', indent * 2);
        writer.Write(indentText);
        writer.Write(node.Name);
        writer.Write(':');

        if (node.Properties.Count > 0)
        {
            writer.Write(' ');
            for (int i = 0; i < node.Properties.Count; i++)
            {
                if (i != 0)
                {
                    writer.Write(", ");
                }

                WriteAsciiProperty(writer, node.Properties[i]);
            }
        }

        if (node.Children.Count == 0)
        {
            writer.WriteLine();
            return;
        }

        writer.WriteLine(" {");
        for (int i = 0; i < node.Children.Count; i++)
        {
            WriteAsciiNode(writer, node.Children[i], indent + 1);
        }

        writer.Write(indentText);
        writer.WriteLine("}");
    }

    public static void WriteAsciiProperty(StreamWriter writer, FbxProperty property)
    {
        switch (property.TypeCode)
        {
            case 'S':
                writer.Write('"');
                writer.Write(property.AsString().Replace("\\", "\\\\", StringComparison.Ordinal).Replace("\"", "\\\"", StringComparison.Ordinal));
                writer.Write('"');
                return;
            case 'C':
                writer.Write(Convert.ToBoolean(property.Value, CultureInfo.InvariantCulture) ? 'Y' : 'N');
                return;
            case 'Y':
            case 'I':
            case 'L':
                writer.Write(Convert.ToInt64(property.Value, CultureInfo.InvariantCulture).ToString(CultureInfo.InvariantCulture));
                return;
            case 'F':
            case 'D':
                writer.Write(Convert.ToDouble(property.Value, CultureInfo.InvariantCulture).ToString("R", CultureInfo.InvariantCulture));
                return;
            case 'i':
                WriteAsciiArray(writer, property.Value as int[] ?? []);
                return;
            case 'l':
                WriteAsciiArray(writer, property.Value as long[] ?? []);
                return;
            case 'd':
                WriteAsciiArray(writer, property.Value as double[] ?? []);
                return;
            case 'f':
                WriteAsciiArray(writer, property.Value as float[] ?? []);
                return;
            case 'b':
                WriteAsciiArray(writer, property.Value as bool[] ?? []);
                return;
            default:
                writer.Write('"');
                writer.Write(property.AsString());
                writer.Write('"');
                return;
        }
    }

    public static void WriteAsciiArray(StreamWriter writer, int[] values)
    {
        writer.Write('*');
        writer.Write(values.Length.ToString(CultureInfo.InvariantCulture));
        writer.Write(" { a: ");
        for (int i = 0; i < values.Length; i++)
        {
            if (i != 0)
            {
                writer.Write(',');
            }

            writer.Write(values[i].ToString(CultureInfo.InvariantCulture));
        }

        writer.Write(" }");
    }

    public static void WriteAsciiArray(StreamWriter writer, long[] values)
    {
        writer.Write('*');
        writer.Write(values.Length.ToString(CultureInfo.InvariantCulture));
        writer.Write(" { a: ");
        for (int i = 0; i < values.Length; i++)
        {
            if (i != 0)
            {
                writer.Write(',');
            }

            writer.Write(values[i].ToString(CultureInfo.InvariantCulture));
        }

        writer.Write(" }");
    }

    public static void WriteAsciiArray(StreamWriter writer, double[] values)
    {
        writer.Write('*');
        writer.Write(values.Length.ToString(CultureInfo.InvariantCulture));
        writer.Write(" { a: ");
        for (int i = 0; i < values.Length; i++)
        {
            if (i != 0)
            {
                writer.Write(',');
            }

            writer.Write(values[i].ToString("R", CultureInfo.InvariantCulture));
        }

        writer.Write(" }");
    }

    public static void WriteAsciiArray(StreamWriter writer, float[] values)
    {
        writer.Write('*');
        writer.Write(values.Length.ToString(CultureInfo.InvariantCulture));
        writer.Write(" { a: ");
        for (int i = 0; i < values.Length; i++)
        {
            if (i != 0)
            {
                writer.Write(',');
            }

            writer.Write(values[i].ToString("R", CultureInfo.InvariantCulture));
        }

        writer.Write(" }");
    }

    public static void WriteAsciiArray(StreamWriter writer, bool[] values)
    {
        writer.Write('*');
        writer.Write(values.Length.ToString(CultureInfo.InvariantCulture));
        writer.Write(" { a: ");
        for (int i = 0; i < values.Length; i++)
        {
            if (i != 0)
            {
                writer.Write(',');
            }

            writer.Write(values[i] ? '1' : '0');
        }

        writer.Write(" }");
    }

    /// <summary>Provides a lightweight tokenizer for FBX ASCII text.</summary>
    public sealed class FbxAsciiTokenizer
    {
        private readonly string _text;
        private int _position;

        /// <summary>Initializes a new tokenizer over the given FBX ASCII text.</summary>
        /// <param name="text">The full ASCII FBX document text.</param>
        public FbxAsciiTokenizer(string text)
        {
            _text = text;
        }

        public bool IsEnd => _position >= _text.Length;

        public bool LastOpenedBlock { get; private set; }

        public char PeekChar()
        {
            if (IsEnd)
            {
                return '\0';
            }

            return _text[_position];
        }

        public void AdvanceOne()
        {
            if (!IsEnd)
            {
                _position++;
            }
        }

        public bool TryConsume(char expected)
        {
            TryConsumeCommentOrWhitespace();
            if (PeekChar() != expected)
            {
                if (expected == '{')
                {
                    LastOpenedBlock = false;
                }

                return false;
            }

            _position++;
            LastOpenedBlock = expected == '{';
            return true;
        }

        public void ConsumeLineEndings()
        {
            while (!IsEnd)
            {
                char c = PeekChar();
                if (c != '\n' && c != '\r')
                {
                    break;
                }

                _position++;
            }
        }

        public bool TryConsumeCommentOrWhitespace()
        {
            bool consumed = false;
            while (!IsEnd)
            {
                char c = PeekChar();
                if (char.IsWhiteSpace(c))
                {
                    consumed = true;
                    _position++;
                    continue;
                }

                if (c == ';')
                {
                    consumed = true;
                    while (!IsEnd)
                    {
                        char commentChar = PeekChar();
                        _position++;
                        if (commentChar == '\n')
                        {
                            break;
                        }
                    }

                    continue;
                }

                break;
            }

            return consumed;
        }

        public string? ReadIdentifier()
        {
            TryConsumeCommentOrWhitespace();
            if (IsEnd)
            {
                return null;
            }

            int start = _position;
            char first = PeekChar();
            if (!(char.IsLetter(first) || first == '_' || first == '|'))
            {
                return null;
            }

            _position++;
            while (!IsEnd)
            {
                char c = PeekChar();
                if (!(char.IsLetterOrDigit(c) || c == '_' || c == '-' || c == '|'))
                {
                    break;
                }

                _position++;
            }

            return _text[start.._position];
        }

        public string? ReadNumberToken()
        {
            TryConsumeCommentOrWhitespace();
            if (IsEnd)
            {
                return null;
            }

            int start = _position;
            char first = PeekChar();
            if (!(char.IsDigit(first) || first == '-' || first == '+'))
            {
                return null;
            }

            _position++;
            while (!IsEnd)
            {
                char c = PeekChar();
                if (!(char.IsDigit(c) || c == '.' || c == 'e' || c == 'E' || c == '-' || c == '+'))
                {
                    break;
                }

                _position++;
            }

            return _text[start.._position];
        }

        public string ReadQuotedString()
        {
            TryConsumeCommentOrWhitespace();
            if (PeekChar() != '"')
            {
                return string.Empty;
            }

            _position++;
            StringBuilder builder = new();
            while (!IsEnd)
            {
                char c = PeekChar();
                _position++;

                if (c == '"')
                {
                    break;
                }

                if (c == '\\' && !IsEnd)
                {
                    char escaped = PeekChar();
                    _position++;
                    builder.Append(escaped);
                    continue;
                }

                builder.Append(c);
            }

            return builder.ToString();
        }
    }
}
