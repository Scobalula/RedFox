namespace RedFox.Graphics3D.Buffers;

/// <summary>
/// Describes the contiguous, unconverted component payload of a <see cref="DataBuffer"/>.
/// </summary>
/// <remarks>
/// This value preserves the source component type and layout information without introducing a dependency
/// on a rendering backend. Consumers that require backend-specific element types must map
/// <see cref="ComponentType"/> within their own assembly.
/// </remarks>
public readonly ref struct DataBufferRawData
{
    /// <summary>
    /// Initializes a new instance of the <see cref="DataBufferRawData"/> structure.
    /// </summary>
    /// <param name="bytes">
    /// The contiguous byte payload containing the populated scalar components.
    /// </param>
    /// <param name="componentType">
    /// The unmanaged CLR type used to store each scalar component.
    /// </param>
    /// <param name="elementCount">
    /// The number of populated elements represented by <paramref name="bytes"/>.
    /// </param>
    /// <param name="valueCount">
    /// The number of values in each element.
    /// </param>
    /// <param name="componentCount">
    /// The number of scalar components in each value.
    /// </param>
    /// <param name="elementStrideBytes">
    /// The number of bytes from one element to the next element in the payload.
    /// </param>
    /// <param name="valueStrideBytes">
    /// The number of bytes from one value to the next value in an element.
    /// </param>
    /// <param name="componentSizeBytes">
    /// The number of bytes used by one scalar component.
    /// </param>
    public DataBufferRawData(
        ReadOnlySpan<byte> bytes,
        Type componentType,
        int elementCount,
        int valueCount,
        int componentCount,
        int elementStrideBytes,
        int valueStrideBytes,
        int componentSizeBytes)
    {
        Bytes = bytes;
        ComponentType = componentType;
        ElementCount = elementCount;
        ValueCount = valueCount;
        ComponentCount = componentCount;
        ElementStrideBytes = elementStrideBytes;
        ValueStrideBytes = valueStrideBytes;
        ComponentSizeBytes = componentSizeBytes;
    }

    /// <summary>
    /// Gets the contiguous byte payload containing the populated scalar components.
    /// </summary>
    public ReadOnlySpan<byte> Bytes { get; }

    /// <summary>
    /// Gets the unmanaged CLR type used to store each scalar component.
    /// </summary>
    public Type ComponentType { get; }

    /// <summary>
    /// Gets the number of populated elements represented by <see cref="Bytes"/>.
    /// </summary>
    public int ElementCount { get; }

    /// <summary>
    /// Gets the number of values in each element.
    /// </summary>
    public int ValueCount { get; }

    /// <summary>
    /// Gets the number of scalar components in each value.
    /// </summary>
    public int ComponentCount { get; }

    /// <summary>
    /// Gets the number of bytes from one element to the next element in the payload.
    /// </summary>
    public int ElementStrideBytes { get; }

    /// <summary>
    /// Gets the number of bytes from one value to the next value in an element.
    /// </summary>
    public int ValueStrideBytes { get; }

    /// <summary>
    /// Gets the number of bytes used by one scalar component.
    /// </summary>
    public int ComponentSizeBytes { get; }

    /// <summary>
    /// Gets the total number of bytes in <see cref="Bytes"/>.
    /// </summary>
    public int SizeBytes => Bytes.Length;
}
