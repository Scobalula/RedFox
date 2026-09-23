using System;

namespace RedFox.Graphics3D.Rendering;

/// <summary>
/// Describes a contiguous CPU byte view and layout metadata that can be uploaded to a GPU buffer.
/// </summary>
public readonly ref struct GpuBufferData
{
    /// <summary>
    /// Initializes a new <see cref="GpuBufferData"/> value.
    /// </summary>
    /// <param name="bytes">The contiguous bytes to upload.</param>
    /// <param name="elementType">The scalar or packed storage type represented by the bytes.</param>
    /// <param name="elementCount">The number of logical source elements.</param>
    /// <param name="valueCount">The number of values in each logical source element.</param>
    /// <param name="componentCount">The number of components in each value.</param>
    /// <param name="elementStrideBytes">The byte stride between logical source elements.</param>
    /// <param name="valueStrideBytes">The byte stride between values within a logical source element.</param>
    /// <param name="componentSizeBytes">The size of a single scalar or packed component in bytes.</param>
    public GpuBufferData(
        ReadOnlySpan<byte> bytes,
        GpuBufferElementType elementType,
        int elementCount,
        int valueCount,
        int componentCount,
        int elementStrideBytes,
        int valueStrideBytes,
        int componentSizeBytes)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(elementCount);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(valueCount);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(componentCount);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(elementStrideBytes);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(valueStrideBytes);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(componentSizeBytes);

        Bytes = bytes;
        ElementType = elementType;
        ElementCount = elementCount;
        ValueCount = valueCount;
        ComponentCount = componentCount;
        ElementStrideBytes = elementStrideBytes;
        ValueStrideBytes = valueStrideBytes;
        ComponentSizeBytes = componentSizeBytes;
    }

    /// <summary>
    /// Gets the contiguous bytes to upload.
    /// </summary>
    public ReadOnlySpan<byte> Bytes { get; }

    /// <summary>
    /// Gets the number of bytes in <see cref="Bytes"/>.
    /// </summary>
    public int SizeBytes => Bytes.Length;

    /// <summary>
    /// Gets the scalar or packed storage type represented by <see cref="Bytes"/>.
    /// </summary>
    public GpuBufferElementType ElementType { get; }

    /// <summary>
    /// Gets the number of logical source elements.
    /// </summary>
    public int ElementCount { get; }

    /// <summary>
    /// Gets the number of values in each logical source element.
    /// </summary>
    public int ValueCount { get; }

    /// <summary>
    /// Gets the number of components in each value.
    /// </summary>
    public int ComponentCount { get; }

    /// <summary>
    /// Gets the byte stride between logical source elements.
    /// </summary>
    public int ElementStrideBytes { get; }

    /// <summary>
    /// Gets the byte stride between values within a logical source element.
    /// </summary>
    public int ValueStrideBytes { get; }

    /// <summary>
    /// Gets the size of a single scalar or packed component in bytes.
    /// </summary>
    public int ComponentSizeBytes { get; }

    /// <summary>
    /// Gets the total number of source components represented by the data.
    /// </summary>
    public int TotalComponentCount => ElementCount * ValueCount * ComponentCount;

    /// <summary>
    /// Gets a value indicating whether the bytes are tightly packed without inter-element padding.
    /// </summary>
    public bool IsTightlyPacked
        => ElementStrideBytes == ValueCount * ValueStrideBytes
            && ValueStrideBytes == ComponentCount * ComponentSizeBytes
            && SizeBytes == TotalComponentCount * ComponentSizeBytes;
}
