namespace RedFox.Graphics3D.ActorX;

/// <summary>
/// The 32-byte header that precedes every data chunk in an ActorX PSK/PSKX or PSA file.
/// It identifies the chunk, the size of a single record, and the number of records that follow.
/// </summary>
public struct ActorXChunkHeader
{
    /// <summary>The fixed length, in bytes, of the chunk identifier field.</summary>
    public const int IdLength = 20;

    /// <summary>Gets or sets the chunk identifier (see <see cref="ActorXChunkId"/>).</summary>
    public string ChunkId { get; set; }

    /// <summary>Gets or sets the format/version flag for the chunk.</summary>
    public int TypeFlag { get; set; }

    /// <summary>Gets or sets the size, in bytes, of a single record in the chunk body.</summary>
    public int DataSize { get; set; }

    /// <summary>Gets or sets the number of records contained in the chunk body.</summary>
    public int DataCount { get; set; }

    /// <summary>Gets the total size, in bytes, of the chunk body.</summary>
    public readonly long BodySize => (long)DataSize * DataCount;

    /// <summary>
    /// Reads a chunk header from the current position of the supplied reader.
    /// </summary>
    /// <param name="reader">The source reader.</param>
    /// <returns>The decoded header.</returns>
    public static ActorXChunkHeader Read(BinaryReader reader) => new()
    {
        ChunkId = ActorXBinary.ReadFixedString(reader, IdLength),
        TypeFlag = reader.ReadInt32(),
        DataSize = reader.ReadInt32(),
        DataCount = reader.ReadInt32(),
    };

    /// <summary>
    /// Writes a chunk header to the supplied writer.
    /// </summary>
    /// <param name="writer">The destination writer.</param>
    /// <param name="chunkId">The chunk identifier.</param>
    /// <param name="typeFlag">The format/version flag.</param>
    /// <param name="dataSize">The size, in bytes, of a single record.</param>
    /// <param name="dataCount">The number of records that follow.</param>
    public static void Write(BinaryWriter writer, string chunkId, int typeFlag, int dataSize, int dataCount)
    {
        ActorXBinary.WriteFixedString(writer, chunkId, IdLength);
        writer.Write(typeFlag);
        writer.Write(dataSize);
        writer.Write(dataCount);
    }
}
