namespace RedFox.Graphics3D.Formats.ActorX;

/// <summary>
/// The 32-byte header that precedes every data chunk in an ActorX PSK/PSKX or PSA file.
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
    public static ActorXChunkHeader Read(BinaryReader reader)
    {
        ActorXChunkHeader chunk = new()
        {
            ChunkId		= ActorXBinary.ReadFixedString(reader, IdLength),
            TypeFlag	= reader.ReadInt32(),
            DataSize	= reader.ReadInt32(),
            DataCount	= reader.ReadInt32(),
        };
        int expectedSize = chunk.ChunkId switch
        {
            ActorXChunkId.MeshHeader or ActorXChunkId.AnimationHeader			 => 0,
            ActorXChunkId.Points or ActorXChunkId.Faces or ActorXChunkId.Weights => 12,
            ActorXChunkId.Wedges or ActorXChunkId.WedgesExtended				 => 16,
            ActorXChunkId.FacesExtended											 => 18,
            ActorXChunkId.Materials												 => 88,
            ActorXChunkId.Bones or ActorXChunkId.BoneNames						 => 120,
            ActorXChunkId.AnimationInfo											 => 168,
            ActorXChunkId.AnimationKeys											 => 32,
            _																	 => chunk.DataSize,
        };
        if (chunk.DataSize < 0 || chunk.DataCount < 0 || chunk.DataSize != expectedSize || chunk.BodySize > reader.BaseStream.Length - reader.BaseStream.Position)
            throw new InvalidDataException("Invalid ActorX chunk dimensions.");
        return chunk;
    }

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
