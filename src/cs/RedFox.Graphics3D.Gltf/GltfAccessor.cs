namespace RedFox.Graphics3D.Gltf;

/// <summary>
/// Provides typed access to elements stored within a <see cref="GltfBufferView"/>.
/// An accessor defines the component type, element type, and count of data elements
/// in the referenced buffer view region.
/// </summary>
public sealed class GltfAccessor
{
    /// <summary>
    /// Gets or sets the index of the <see cref="GltfBufferView"/> this accessor reads from.
    /// A value of -1 indicates no buffer view (all zeros).
    /// </summary>
    public int BufferView { get; set; } = -1;

    /// <summary>
    /// Gets or sets the byte offset relative to the start of the buffer view.
    /// </summary>
    public int ByteOffset { get; set; }

    /// <summary>
    /// Gets or sets the data type of each component (e.g., 5126 for FLOAT).
    /// </summary>
    public int ComponentType { get; set; }

    /// <summary>
    /// Gets or sets whether integer values should be normalized to [0,1] or [-1,1].
    /// </summary>
    public bool Normalized { get; set; }

    /// <summary>
    /// Gets or sets the number of elements described by this accessor.
    /// </summary>
    public int Count { get; set; }

    /// <summary>
    /// Gets or sets the element type string (e.g., "SCALAR", "VEC3", "MAT4").
    /// </summary>
    public string Type { get; set; } = GltfConstants.TypeScalar;

    /// <summary>
    /// Gets or sets the per-component minimum values for this accessor.
    /// </summary>
    public float[]? Min { get; set; }

    /// <summary>
    /// Gets or sets the per-component maximum values for this accessor.
    /// </summary>
    public float[]? Max { get; set; }

    /// <summary>
    /// Gets or sets the optional name of the accessor.
    /// </summary>
    public string? Name { get; set; }

    /// <summary>
    /// Gets the number of scalar components per element based on <see cref="Type"/>.
    /// </summary>
    public int ComponentCount => GltfConstants.GetComponentCount(Type);

    /// <summary>
    /// Gets the byte size of a single component based on <see cref="ComponentType"/>.
    /// </summary>
    public int ComponentSize => GltfConstants.GetComponentSize(ComponentType);

    /// <summary>
    /// Gets the total byte size of a single element (components × component size).
    /// </summary>
    public int ElementSize => ComponentCount * ComponentSize;
}
