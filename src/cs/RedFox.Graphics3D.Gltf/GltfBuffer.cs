namespace RedFox.Graphics3D.Gltf;

/// <summary>
/// Represents a raw binary data buffer referenced by buffer views.
/// In a GLB file this is typically the single embedded binary chunk.
/// </summary>
public sealed class GltfBuffer
{
    /// <summary>
    /// Gets or sets the total byte length of the buffer.
    /// </summary>
    public int ByteLength { get; set; }

    /// <summary>
    /// Gets or sets the optional URI for external buffer data (not used in GLB).
    /// </summary>
    public string? Uri { get; set; }

    /// <summary>
    /// Gets or sets the resolved binary data for this buffer.
    /// </summary>
    public byte[]? Data { get; set; }

    /// <summary>
    /// Gets or sets the optional name of the buffer.
    /// </summary>
    public string? Name { get; set; }
}
