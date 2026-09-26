namespace RedFox.Compression.GDeflate;

/// <summary>
/// Holds a pointer and byte count
/// for one compressed GDeflate tile.
/// </summary>
public unsafe struct GDeflatePage
{
    /// <summary>
    /// Points to the compressed tile
    /// data.
    /// </summary>
    public void* Data;

    /// <summary>
    /// Stores the number of bytes
    /// in the compressed tile.
    /// </summary>
    public nuint ByteCount;
}
