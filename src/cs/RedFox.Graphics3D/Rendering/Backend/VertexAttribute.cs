namespace RedFox.Graphics3D.Rendering.Backend;

/// <summary>
/// Describes a single vertex attribute consumed by a graphics pipeline.
/// </summary>
public readonly record struct VertexAttribute
{
    /// <summary>
    /// Gets the attribute name.
    /// </summary>
    public string Name { get; }

    /// <summary>
    /// Gets the component count for the attribute.
    /// </summary>
    public int ComponentCount { get; }

    /// <summary>
    /// Gets the component storage type.
    /// </summary>
    public VertexAttributeType Type { get; }

    /// <summary>
    /// Gets the byte offset of the attribute within a vertex element.
    /// </summary>
    public int OffsetBytes { get; }

    /// <summary>
    /// Gets the byte stride between consecutive attribute elements.
    /// </summary>
    public int StrideBytes { get; }

    /// <summary>
    /// Initializes a new <see cref="VertexAttribute"/> value.
    /// </summary>
    /// <param name="name">The attribute name.</param>
    /// <param name="componentCount">The component count.</param>
    /// <param name="type">The component storage type.</param>
    /// <param name="offsetBytes">The byte offset of the attribute.</param>
    /// <param name="strideBytes">The byte stride between elements.</param>
    public VertexAttribute(string name, int componentCount, VertexAttributeType type, int offsetBytes, int strideBytes)
    {
        Name = name;
        ComponentCount = componentCount;
        Type = type;
        OffsetBytes = offsetBytes;
        StrideBytes = strideBytes;
    }
}