using System.Globalization;
using System.IO.Compression;
using System.Text;

namespace RedFox.Graphics3D.KaydaraFbx;

/// <summary>
/// Provides low-level FBX ASCII and binary serialization routines.
/// </summary>
public static class FbxDocumentSerializer
{
    /// <summary>
    /// Gets the RedFox-authored creator string written into top-level FBX metadata.
    /// </summary>
    public const string RedFoxTopLevelCreator = "RedFox Graphics3D Kaydara FBX";

    /// <summary>
    /// Gets the Maya-compatible RedFox creation time written into top-level metadata.
    /// </summary>
    public const string RedFoxCompatibleCreationTime = "2026-03-24 15:33:49:359";

    /// <summary>
    /// Gets the Maya-compatible FileId used for RedFox-authored exports.
    /// </summary>
    public static readonly byte[] RedFoxCompatibleFileId =
    [
        0x2C, 0xBE, 0x27, 0xE4, 0xB9, 0x2E, 0xC4, 0xCF,
        0xB1, 0xC3, 0xB8, 0x2B, 0xAD, 0x29, 0xFD, 0xF3,
    ];

    /// <summary>
    /// Gets the default 16-byte FBX footer identifier fallback.
    /// </summary>
    public static readonly byte[] FooterId =
    [
        0xFA, 0xBC, 0xAB, 0x09, 0xD0, 0xCA, 0xD6, 0x63,
        0xB6, 0x73, 0xFC, 0x8C, 0x12, 0xF8, 0x2D, 0x72,
    ];

    /// <summary>
    /// Gets the 16-byte FBX binary footer magic sequence.
    /// </summary>
    public static readonly byte[] FooterMagic =
    [
        0xF8, 0x5A, 0x8C, 0x6A, 0xDE, 0xF5, 0xD9, 0x7E,
        0xEC, 0xE9, 0x0C, 0xE3, 0x75, 0x8F, 0x29, 0x0B,
    ];

    /// <summary>
    /// Gets the canonical FBX binary file header.
    /// </summary>
    public static readonly byte[] BinaryHeader = [
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
        if (header.Length < BinaryHeader.Length)
        {
            return false;
        }

        return header[..BinaryHeader.Length].SequenceEqual(BinaryHeader);
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
            Span<byte> probe = stackalloc byte[BinaryHeader.Length];
            int read = workingStream.Read(probe);
            workingStream.Position = originalPosition;

            if (read == BinaryHeader.Length && IsBinaryHeader(probe))
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

        NormalizeRedFoxTopLevelMetadata(document);

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

        byte[] header = reader.ReadBytes(BinaryHeader.Length);
        if (!header.AsSpan().SequenceEqual(BinaryHeader))
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

    /// <summary>Reads and optionally decompresses an FBX binary array property.</summary>
    /// <param name="reader">The binary reader positioned at the array header.</param>
    /// <param name="elementSize">The byte size of each element.</param>
    /// <param name="factory">The delegate that converts raw bytes into a typed array object.</param>
    /// <returns>The decoded array object.</returns>
    public static object ReadArrayProperty(BinaryReader reader, int elementSize, FbxArrayFactory factory)
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

    /// <summary>
    /// Attempts to decompress the input stream using the zlib algorithm.
    /// </summary>
    /// <param name="input">The compressed source stream.</param>
    /// <param name="output">The destination stream that receives decompressed bytes.</param>
    /// <returns><see langword="true"/> when decompression succeeds; otherwise <see langword="false"/>.</returns>
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

    /// <summary>
    /// Decompresses the input stream using the raw deflate algorithm.
    /// </summary>
    /// <param name="input">The compressed source stream.</param>
    /// <param name="output">The destination stream that receives decompressed bytes.</param>
    public static void TryDecompressWithDeflate(Stream input, Stream output)
    {
        using DeflateStream deflate = new(input, CompressionMode.Decompress, leaveOpen: true);
        deflate.CopyTo(output);
    }

    /// <summary>
    /// Decodes a raw byte span into a <see cref="float"/> array.
    /// </summary>
    /// <param name="bytes">The source bytes in little-endian layout.</param>
    /// <param name="count">The number of elements to decode.</param>
    /// <returns>The decoded array.</returns>
    public static float[] ReadFloatArray(ReadOnlySpan<byte> bytes, int count)
    {
        float[] values = new float[count];
        for (int i = 0; i < count; i++)
        {
            values[i] = BitConverter.ToSingle(bytes.Slice(i * sizeof(float), sizeof(float)));
        }

        return values;
    }

    /// <summary>
    /// Decodes a raw byte span into a <see cref="double"/> array.
    /// </summary>
    /// <param name="bytes">The source bytes in little-endian layout.</param>
    /// <param name="count">The number of elements to decode.</param>
    /// <returns>The decoded array.</returns>
    public static double[] ReadDoubleArray(ReadOnlySpan<byte> bytes, int count)
    {
        double[] values = new double[count];
        for (int i = 0; i < count; i++)
        {
            values[i] = BitConverter.ToDouble(bytes.Slice(i * sizeof(double), sizeof(double)));
        }

        return values;
    }

    /// <summary>
    /// Decodes a raw byte span into a <see cref="long"/> array.
    /// </summary>
    /// <param name="bytes">The source bytes in little-endian layout.</param>
    /// <param name="count">The number of elements to decode.</param>
    /// <returns>The decoded array.</returns>
    public static long[] ReadInt64Array(ReadOnlySpan<byte> bytes, int count)
    {
        long[] values = new long[count];
        for (int i = 0; i < count; i++)
        {
            values[i] = BitConverter.ToInt64(bytes.Slice(i * sizeof(long), sizeof(long)));
        }

        return values;
    }

    /// <summary>
    /// Decodes a raw byte span into an <see cref="int"/> array.
    /// </summary>
    /// <param name="bytes">The source bytes in little-endian layout.</param>
    /// <param name="count">The number of elements to decode.</param>
    /// <returns>The decoded array.</returns>
    public static int[] ReadInt32Array(ReadOnlySpan<byte> bytes, int count)
    {
        int[] values = new int[count];
        for (int i = 0; i < count; i++)
        {
            values[i] = BitConverter.ToInt32(bytes.Slice(i * sizeof(int), sizeof(int)));
        }

        return values;
    }

    /// <summary>
    /// Decodes a raw byte span into a <see cref="bool"/> array where any non-zero byte is <see langword="true"/>.
    /// </summary>
    /// <param name="bytes">The source bytes.</param>
    /// <param name="count">The number of elements to decode.</param>
    /// <returns>The decoded array.</returns>
    public static bool[] ReadBoolArray(ReadOnlySpan<byte> bytes, int count)
    {
        bool[] values = new bool[count];
        for (int i = 0; i < count; i++)
        {
            values[i] = bytes[i] != 0;
        }

        return values;
    }

    /// <summary>
    /// Reads a length-prefixed UTF-8 string from the binary reader.
    /// </summary>
    /// <param name="reader">The binary reader positioned at the 4-byte length prefix.</param>
    /// <returns>The decoded string.</returns>
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

    /// <summary>
    /// Reads a length-prefixed raw byte array from the binary reader.
    /// </summary>
    /// <param name="reader">The binary reader positioned at the 4-byte length prefix.</param>
    /// <returns>The decoded byte array.</returns>
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

        writer.Write(BinaryHeader);
        writer.Write(document.Version <= 0 ? 7400 : document.Version);

        bool is64BitNodeRecords = document.Version >= 7500;

        for (int nodeIndex = 0; nodeIndex < document.Nodes.Count; nodeIndex++)
        {
            FbxNode node = document.Nodes[nodeIndex];
            bool isLast = nodeIndex == document.Nodes.Count - 1;
            WriteBinaryNode(writer, stream, node, is64BitNodeRecords, isLast);
        }

        WriteBinaryNullRecord(writer, is64BitNodeRecords);
        WriteBinaryFooter(writer, stream, document, document.Version <= 0 ? 7400 : document.Version);
    }

    /// <summary>
    /// Writes a single FBX node and its children to the binary stream.
    /// </summary>
    /// <param name="writer">The binary writer.</param>
    /// <param name="stream">The backing stream used for seek-back operations.</param>
    /// <param name="node">The node to serialise.</param>
    /// <param name="is64BitNodeRecords">Whether to use 64-bit node record headers.</param>
    /// <param name="isLast">Whether the node is the last sibling in its current scope.</param>
    public static void WriteBinaryNode(BinaryWriter writer, Stream stream, FbxNode node, bool is64BitNodeRecords, bool isLast)
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

        for (int childIndex = 0; childIndex < node.Children.Count; childIndex++)
        {
            FbxNode child = node.Children[childIndex];
            bool childIsLast = childIndex == node.Children.Count - 1;
            WriteBinaryNode(writer, stream, child, is64BitNodeRecords, childIsLast);
        }

        if (ShouldWriteBlockSentinel(node, isLast))
        {
            WriteBinaryNullRecord(writer, is64BitNodeRecords);
        }

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
    /// Determines whether a node needs a trailing block sentinel in binary form.
    /// </summary>
    /// <param name="node">The node being written.</param>
    /// <param name="isLast">Whether the node is the last sibling in its scope.</param>
    /// <returns><see langword="true"/> when a trailing sentinel must be written.</returns>
    public static bool ShouldWriteBlockSentinel(FbxNode node, bool isLast)
    {
        _ = isLast;
        return node.Children.Count > 0;
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
    /// Creates the 16-byte FBX footer identifier associated with a 16-byte <c>FileId</c> payload.
    /// </summary>
    /// <param name="fileId">The 16-byte FBX <c>FileId</c> value.</param>
    /// <returns>The 16-byte footer identifier expected by Maya-compatible binary FBX files.</returns>
    public static byte[] CreateFooterId(ReadOnlySpan<byte> fileId)
    {
        if (fileId.Length != 16)
        {
            throw new ArgumentException("The FBX FileId must contain exactly 16 bytes.", nameof(fileId));
        }

        return
        [
            0xFA,
            0xBC,
            (byte)(fileId[0] ^ 0x83),
            (byte)(fileId[1] ^ 0xBA),
            (byte)(fileId[0] ^ fileId[1] ^ 0x4B),
            0xCA,
            (byte)(fileId[0] ^ 0xFE),
            (byte)(fileId[7] ^ 0xA1),
            (byte)(fileId[0] ^ fileId[7] ^ 0x5C),
            0x73,
            (byte)(fileId[0] ^ fileId[7] ^ fileId[8] ^ 0xA9),
            (byte)(fileId[11] ^ 0xA6),
            (byte)(fileId[0] ^ fileId[7] ^ fileId[8] ^ fileId[11] ^ 0x6D),
            0xF8,
            (byte)(fileId[0] ^ fileId[7] ^ fileId[8] ^ 0x78),
            (byte)(fileId[11] ^ 0x58),
        ];
    }

    /// <summary>
    /// Resolves the footer identifier that should be written for an FBX document.
    /// </summary>
    /// <param name="document">The document being serialized.</param>
    /// <returns>The footer identifier derived from the document <c>FileId</c>, or the default fallback when unavailable.</returns>
    public static byte[] ResolveFooterId(FbxDocument document)
    {
        ArgumentNullException.ThrowIfNull(document);

        FbxNode? fileIdNode = document.FirstNode("FileId");
        if (fileIdNode is not null && fileIdNode.Properties.Count > 0 && fileIdNode.Properties[0].Value is byte[] fileId && fileId.Length == 16)
        {
            return CreateFooterId(fileId);
        }

        return (byte[])FooterId.Clone();
    }

    /// <summary>
    /// Normalizes RedFox-authored top-level metadata so exported documents use a Maya-compatible FileId and CreationTime pair.
    /// </summary>
    /// <param name="document">The document being prepared for serialization.</param>
    public static void NormalizeRedFoxTopLevelMetadata(FbxDocument document)
    {
        ArgumentNullException.ThrowIfNull(document);

        FbxNode? creatorNode = document.Nodes.FirstOrDefault(static node => node.Name == "Creator" && node.Properties.Count > 0);
        if (creatorNode is null || !string.Equals(creatorNode.Properties[0].AsString(), RedFoxTopLevelCreator, StringComparison.Ordinal))
        {
            return;
        }

        FbxNode? fileIdNode = document.FirstNode("FileId");
        if (fileIdNode is null)
        {
            fileIdNode = new FbxNode("FileId");
            document.Nodes.Insert(Math.Min(1, document.Nodes.Count), fileIdNode);
        }

        fileIdNode.Properties.Clear();
        fileIdNode.Properties.Add(new FbxProperty('R', (byte[])RedFoxCompatibleFileId.Clone()));

        FbxNode? creationTimeNode = document.FirstNode("CreationTime");
        if (creationTimeNode is null)
        {
            creationTimeNode = new FbxNode("CreationTime");
            int creatorIndex = document.Nodes.FindIndex(static node => node.Name == "Creator");
            int insertIndex = creatorIndex >= 0 ? creatorIndex : document.Nodes.Count;
            document.Nodes.Insert(insertIndex, creationTimeNode);
        }

        creationTimeNode.Properties.Clear();
        creationTimeNode.Properties.Add(new FbxProperty('S', RedFoxCompatibleCreationTime));
    }

    /// <summary>
    /// Writes the binary FBX footer that follows the root sentinel.
    /// </summary>
    /// <param name="writer">The binary writer.</param>
    /// <param name="stream">The backing stream.</param>
    /// <param name="document">The document whose footer is being written.</param>
    /// <param name="version">The FBX file version.</param>
    public static void WriteBinaryFooter(BinaryWriter writer, Stream stream, FbxDocument document, int version)
    {
        writer.Write(ResolveFooterId(document));
        writer.Write(0);

        long offset = stream.Position;
        int padding = (int)(((offset + 15L) & ~15L) - offset);
        if (padding == 0)
        {
            padding = 16;
        }

        writer.Write(new byte[padding]);
        writer.Write(version);
        writer.Write(new byte[120]);
        writer.Write(FooterMagic);
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

    /// <summary>
    /// Writes an uncompressed binary FBX array property containing <see cref="float"/> elements.
    /// </summary>
    /// <param name="writer">The binary writer.</param>
    /// <param name="values">The array elements.</param>
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

    /// <summary>
    /// Writes an uncompressed binary FBX array property containing <see cref="double"/> elements.
    /// </summary>
    /// <param name="writer">The binary writer.</param>
    /// <param name="values">The array elements.</param>
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

    /// <summary>
    /// Writes an uncompressed binary FBX array property containing <see cref="long"/> elements.
    /// </summary>
    /// <param name="writer">The binary writer.</param>
    /// <param name="values">The array elements.</param>
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

    /// <summary>
    /// Writes an uncompressed binary FBX array property containing <see cref="int"/> elements.
    /// </summary>
    /// <param name="writer">The binary writer.</param>
    /// <param name="values">The array elements.</param>
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

    /// <summary>
    /// Writes an uncompressed binary FBX array property containing <see cref="bool"/> elements.
    /// </summary>
    /// <param name="writer">The binary writer.</param>
    /// <param name="values">The array elements.</param>
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
        FbxDocument document = new()
        {
            Format = FbxFormat.Ascii,
            Version = 7400,
        };

        foreach ((int start, int end) in EnumerateAsciiTopLevelNodeRanges(text))
        {
            FbxAsciiTokenizer tokenizer = new(text, start, end - start);
            FbxNode? node = ParseAsciiNode(tokenizer);
            if (node is not null)
            {
                document.Nodes.Add(node);
            }
        }

        return document;
    }

    /// <summary>
    /// Enumerates raw text slices for each top-level ASCII FBX node.
    /// </summary>
    /// <param name="text">The full ASCII FBX document text.</param>
    /// <returns>A sequence of top-level node text spans.</returns>
    public static IEnumerable<string> EnumerateAsciiTopLevelNodeTexts(string text)
    {
        foreach ((int start, int end) in EnumerateAsciiTopLevelNodeRanges(text))
        {
            yield return text[start..end];
        }
    }

    /// <summary>
    /// Enumerates absolute source ranges for each top-level ASCII FBX node.
    /// </summary>
    /// <param name="text">The full ASCII FBX document text.</param>
    /// <returns>A sequence of inclusive-start/exclusive-end node ranges.</returns>
    public static IEnumerable<(int Start, int End)> EnumerateAsciiTopLevelNodeRanges(string text)
    {
        return EnumerateAsciiTopLevelNodeRanges(text, 0, text.Length);
    }

    /// <summary>
    /// Enumerates absolute source ranges for each top-level ASCII FBX node within the specified source window.
    /// </summary>
    /// <param name="text">The full ASCII FBX document text.</param>
    /// <param name="start">The inclusive start offset of the scan window.</param>
    /// <param name="end">The exclusive end offset of the scan window.</param>
    /// <returns>A sequence of inclusive-start/exclusive-end node ranges.</returns>
    public static IEnumerable<(int Start, int End)> EnumerateAsciiTopLevelNodeRanges(string text, int start, int end)
    {
        int position = start;
        while (position < end)
        {
            SkipAsciiWhitespaceAndComments(text, ref position, end);
            if (position >= end)
            {
                yield break;
            }

            int nodeStart = position;
            int nodeEnd = FindAsciiTopLevelNodeEnd(text, position, end);
            if (nodeEnd <= nodeStart)
            {
                position++;
                continue;
            }

            yield return (nodeStart, nodeEnd);
            position = nodeEnd;
        }
    }

    /// <summary>
    /// Skips ASCII whitespace and semicolon comments from the current text position.
    /// </summary>
    /// <param name="text">The source ASCII FBX text.</param>
    /// <param name="position">The mutable character offset.</param>
    public static void SkipAsciiWhitespaceAndComments(string text, ref int position)
    {
        SkipAsciiWhitespaceAndComments(text, ref position, text.Length);
    }

    /// <summary>
    /// Skips ASCII whitespace and semicolon comments from the current text position inside the specified source window.
    /// </summary>
    /// <param name="text">The source ASCII FBX text.</param>
    /// <param name="position">The mutable character offset.</param>
    /// <param name="end">The exclusive end offset of the scan window.</param>
    public static void SkipAsciiWhitespaceAndComments(string text, ref int position, int end)
    {
        while (position < end)
        {
            char current = text[position];
            if (char.IsWhiteSpace(current))
            {
                position++;
                continue;
            }

            if (current != ';')
            {
                break;
            }

            while (position < end && text[position] != '\n')
            {
                position++;
            }
        }
    }

    /// <summary>
    /// Finds the exclusive end offset of a top-level ASCII FBX node starting at the specified position.
    /// </summary>
    /// <param name="text">The source ASCII FBX text.</param>
    /// <param name="start">The start offset of the node.</param>
    /// <returns>The exclusive end offset of the node text.</returns>
    public static int FindAsciiTopLevelNodeEnd(string text, int start)
    {
        return FindAsciiTopLevelNodeEnd(text, start, text.Length);
    }

    /// <summary>
    /// Finds the exclusive end offset of a top-level ASCII FBX node starting at the specified position within a source window.
    /// </summary>
    /// <param name="text">The source ASCII FBX text.</param>
    /// <param name="start">The start offset of the node.</param>
    /// <param name="end">The exclusive end offset of the scan window.</param>
    /// <returns>The exclusive end offset of the node text.</returns>
    public static int FindAsciiTopLevelNodeEnd(string text, int start, int end)
    {
        bool inString = false;
        bool inComment = false;
        bool sawBlock = false;
        int depth = 0;

        for (int position = start; position < end; position++)
        {
            char current = text[position];

            if (inComment)
            {
                if (current == '\n')
                {
                    inComment = false;
                    if (!sawBlock)
                    {
                        return position + 1;
                    }
                }

                continue;
            }

            if (inString)
            {
                if (current == '\\' && position + 1 < end)
                {
                    position++;
                    continue;
                }

                if (current == '"')
                {
                    inString = false;
                }

                continue;
            }

            if (current == ';')
            {
                inComment = true;
                continue;
            }

            if (current == '"')
            {
                inString = true;
                continue;
            }

            if (current == '{')
            {
                sawBlock = true;
                depth++;
                continue;
            }

            if (current == '}')
            {
                if (depth <= 0)
                {
                    continue;
                }

                depth--;
                if (depth == 0)
                {
                    return position + 1;
                }

                continue;
            }

            if (!sawBlock && (current == '\n' || current == '\r'))
            {
                return position + 1;
            }
        }

        return end;
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
        bool openedBlock = false;

        while (!tokenizer.IsEnd)
        {
            tokenizer.TryConsumeCommentOrWhitespace();

            if (tokenizer.TryConsume('{'))
            {
                openedBlock = true;
                break;
            }

            if (tokenizer.PeekChar() == '\n' || tokenizer.PeekChar() == '\r')
            {
                tokenizer.ConsumeLineEndings();
                break;
            }

            int propertyStart = tokenizer.Position;
            FbxProperty? property = ParseAsciiProperty(tokenizer);
            if (property is not null)
            {
                node.Properties.Add(property);
            }

            tokenizer.TryConsume(',');
            tokenizer.TryConsumeCommentOrWhitespace();
            if (tokenizer.Position == propertyStart)
            {
                tokenizer.AdvanceOne();
            }
        }

        if (!openedBlock)
        {
            return node;
        }

        (int childBlockStart, int childBlockEnd) = tokenizer.ReadBlockRange();
        foreach ((int childNodeStart, int childNodeEnd) in EnumerateAsciiTopLevelNodeRanges(tokenizer.Text, childBlockStart, childBlockEnd))
        {
            FbxAsciiTokenizer childTokenizer = new(tokenizer.Text, childNodeStart, childNodeEnd - childNodeStart);
            FbxNode? child = ParseAsciiNode(childTokenizer);
            if (child is not null)
            {
                node.Children.Add(child);
            }
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
            return ParseAsciiArrayProperty(tokenizer);
        }

        if (tokenizer.PeekChar() == '"')
        {
            return new FbxProperty('S', tokenizer.ReadQuotedString());
        }

        if (tokenizer.TryReadNumberRange(out int numberStart, out int numberLength))
        {
            return ParseAsciiNumericProperty(tokenizer.Text, numberStart, numberLength);
        }

        if (!tokenizer.TryReadIdentifierRange(out int identifierStart, out int identifierLength))
        {
            return null;
        }

        return ParseAsciiBooleanOrStringProperty(tokenizer.Text, identifierStart, identifierLength);
    }

    /// <summary>
    /// Parses an ASCII FBX array property of the form <c>*count { a: v0,v1,... }</c> from the tokenizer.
    /// The leading <c>*</c> must already be consumed before calling this method.
    /// </summary>
    /// <param name="tokenizer">The tokenizer positioned immediately after the <c>*</c> token.</param>
    /// <returns>The inferred typed array property, or <see langword="null"/> when the array header is malformed.</returns>
    public static FbxProperty? ParseAsciiArrayProperty(FbxAsciiTokenizer tokenizer)
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

            int valueStart = tokenizer.Position;
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
            if (tokenizer.Position == valueStart)
            {
                tokenizer.AdvanceOne();
            }
        }

        return InferArrayProperty(values);
    }

    /// <summary>
    /// Parses a numeric ASCII property from a pre-read number token range.
    /// Integers produce an <c>L</c> (Int64) property, floating-point values produce a <c>D</c> (Double) property.
    /// </summary>
    /// <param name="text">The backing tokenizer text.</param>
    /// <param name="start">The inclusive start offset of the numeric token.</param>
    /// <param name="length">The length of the numeric token.</param>
    /// <returns>The typed numeric property.</returns>
    public static FbxProperty ParseAsciiNumericProperty(string text, int start, int length)
    {
        ReadOnlySpan<char> number = text.AsSpan(start, length);
        if (number.IndexOfAny('.', 'e', 'E') >= 0)
        {
            double floating = double.Parse(number, CultureInfo.InvariantCulture);
            return new FbxProperty('D', floating);
        }

        long integer = long.Parse(number, CultureInfo.InvariantCulture);
        return new FbxProperty('L', integer);
    }

    /// <summary>
    /// Evaluates an identifier token as either a boolean (<c>C</c>) or string (<c>S</c>) property.
    /// Tokens matching <c>Y</c>, <c>T</c>, or <c>true</c> produce a boolean <see langword="true"/>;
    /// tokens matching <c>N</c>, <c>F</c>, or <c>false</c> produce a boolean <see langword="false"/>;
    /// all other identifiers produce a string property.
    /// </summary>
    /// <param name="text">The backing tokenizer text.</param>
    /// <param name="start">The inclusive start offset of the identifier token.</param>
    /// <param name="length">The length of the identifier token.</param>
    /// <returns>The typed property.</returns>
    public static FbxProperty ParseAsciiBooleanOrStringProperty(string text, int start, int length)
    {
        ReadOnlySpan<char> identifier = text.AsSpan(start, length);

        if (identifier.Equals("Y".AsSpan(), StringComparison.OrdinalIgnoreCase)
            || identifier.Equals("T".AsSpan(), StringComparison.OrdinalIgnoreCase)
            || identifier.Equals("true".AsSpan(), StringComparison.OrdinalIgnoreCase))
        {
            return new FbxProperty('C', true);
        }

        if (identifier.Equals("N".AsSpan(), StringComparison.OrdinalIgnoreCase)
            || identifier.Equals("F".AsSpan(), StringComparison.OrdinalIgnoreCase)
            || identifier.Equals("false".AsSpan(), StringComparison.OrdinalIgnoreCase))
        {
            return new FbxProperty('C', false);
        }

        return new FbxProperty('S', text.Substring(start, length));
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

    /// <summary>
    /// Writes a single FBX node and its children to the ASCII stream.
    /// </summary>
    /// <param name="writer">The text writer.</param>
    /// <param name="node">The node to serialize.</param>
    /// <param name="indent">The current indentation depth.</param>
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

    /// <summary>
    /// Writes a single FBX property value to the ASCII stream.
    /// </summary>
    /// <param name="writer">The text writer.</param>
    /// <param name="property">The property to serialize.</param>
    public static void WriteAsciiProperty(StreamWriter writer, FbxProperty property)
    {
        switch (property.TypeCode)
        {
            case 'S':
                writer.Write('"');
                writer.Write(FormatAsciiString(property.AsString()));
                writer.Write('"');
                return;
            case 'C':
                writer.Write(Convert.ToBoolean(property.Value, CultureInfo.InvariantCulture) ? 'T' : 'F');
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

    /// <summary>
    /// Writes an ASCII FBX array property containing <see cref="int"/> elements.
    /// </summary>
    /// <param name="writer">The text writer.</param>
    /// <param name="values">The array elements.</param>
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

    /// <summary>
    /// Writes an ASCII FBX array property containing <see cref="long"/> elements.
    /// </summary>
    /// <param name="writer">The text writer.</param>
    /// <param name="values">The array elements.</param>
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

    /// <summary>
    /// Writes an ASCII FBX array property containing <see cref="double"/> elements.
    /// </summary>
    /// <param name="writer">The text writer.</param>
    /// <param name="values">The array elements.</param>
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

    /// <summary>
    /// Writes an ASCII FBX array property containing <see cref="float"/> elements.
    /// </summary>
    /// <param name="writer">The text writer.</param>
    /// <param name="values">The array elements.</param>
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

    /// <summary>
    /// Writes an ASCII FBX array property containing <see cref="bool"/> elements.
    /// </summary>
    /// <param name="writer">The text writer.</param>
    /// <param name="values">The array elements.</param>
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

    /// <summary>
    /// Formats an FBX string for ASCII output, restoring binary object-name markers to the
    /// equivalent <c>Class::Name</c> representation before escaping the value.
    /// </summary>
    /// <param name="value">The raw FBX string value.</param>
    /// <returns>The escaped ASCII representation without surrounding quotes.</returns>
    public static string FormatAsciiString(string value)
    {
        int separatorIndex = value.IndexOf('\0', StringComparison.Ordinal);
        if (separatorIndex >= 0 && separatorIndex + 1 < value.Length && value[separatorIndex + 1] == '\u0001')
        {
            string instanceName = value[..separatorIndex];
            string className = value[(separatorIndex + 2)..];
            value = className + "::" + instanceName;
        }

        return value.Replace("\\", "\\\\", StringComparison.Ordinal).Replace("\"", "\\\"", StringComparison.Ordinal);
    }

}
