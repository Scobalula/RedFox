namespace RedFox.Graphics3D.Gltf;

/// <summary>
/// Describes a contiguous region within a <see cref="GltfBuffer"/> that can be
/// used as vertex attribute data, index data, or animation keyframe storage.
/// </summary>
public sealed class GltfBufferView
{
    /// <summary>
    /// Gets or sets the index of the <see cref="GltfBuffer"/> this view references.
    /// </summary>
    public int Buffer { get; set; }

    /// <summary>
    /// Gets or sets the byte offset into the referenced buffer.
    /// </summary>
    public int ByteOffset { get; set; }

    /// <summary>
    /// Gets or sets the total byte length of this view.
    /// </summary>
    public int ByteLength { get; set; }

    /// <summary>
    /// Gets or sets the stride, in bytes, between consecutive elements.
    /// A value of zero indicates tightly packed data.
    /// </summary>
    public int ByteStride { get; set; }

    /// <summary>
    /// Gets or sets the target hint (e.g., array buffer or element array buffer).
    /// </summary>
    public int Target { get; set; }

    /// <summary>
    /// Gets or sets the optional name of the buffer view.
    /// </summary>
    public string? Name { get; set; }
}
