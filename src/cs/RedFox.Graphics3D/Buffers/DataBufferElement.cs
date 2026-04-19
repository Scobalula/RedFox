using System.Numerics;

namespace RedFox.Graphics3D.Buffers;

/// <summary>
/// Represents a sequential write cursor over a single reserved <see cref="DataBuffer"/> element.
/// </summary>
public struct DataBufferElement
{
    private int _nextValueIndex;
    private int _nextComponentIndex;

    internal DataBufferElement(DataBuffer buffer, int elementIndex)
    {
        Buffer = buffer ?? throw new ArgumentNullException(nameof(buffer));
        ElementIndex = elementIndex;
        _nextValueIndex = 0;
        _nextComponentIndex = 0;
    }

    /// <summary>
    /// Gets the buffer that owns this element.
    /// </summary>
    public DataBuffer Buffer { get; }

    /// <summary>
    /// Gets the zero-based index of the reserved element.
    /// </summary>
    public int ElementIndex { get; }

    /// <summary>
    /// Gets the next value index targeted by sequential writes.
    /// </summary>
    public readonly int NextValueIndex => _nextValueIndex;

    /// <summary>
    /// Gets the next component index targeted by sequential writes.
    /// </summary>
    public readonly int NextComponentIndex => _nextComponentIndex;

    /// <summary>
    /// Gets a value indicating whether all slots in the reserved element have been consumed by sequential writes.
    /// </summary>
    public readonly bool IsComplete => _nextValueIndex >= Buffer.ValueCount;

    /// <summary>
    /// Appends the next scalar component in sequence.
    /// </summary>
    /// <typeparam name="TInput">The numeric type to store.</typeparam>
    /// <param name="value">The scalar value to store.</param>
    /// <returns>The updated element cursor.</returns>
    public DataBufferElement Add<TInput>(TInput value) where TInput : INumber<TInput>
    {
        EnsureCanWriteScalar();

        Buffer.Set(ElementIndex, _nextValueIndex, _nextComponentIndex, value);
        AdvanceScalar();
        return this;
    }

    /// <summary>
    /// Appends a <see cref="Vector2"/> within the current value.
    /// </summary>
    /// <param name="value">The vector to store.</param>
    /// <returns>The updated element cursor.</returns>
    public DataBufferElement Add(Vector2 value)
    {
        EnsureCanWriteVector(2);

        Buffer.Set(ElementIndex, _nextValueIndex, _nextComponentIndex, value.X);
        Buffer.Set(ElementIndex, _nextValueIndex, _nextComponentIndex + 1, value.Y);
        AdvanceWithinValue(2);
        return this;
    }

    /// <summary>
    /// Appends a <see cref="Vector3"/> within the current value.
    /// </summary>
    /// <param name="value">The vector to store.</param>
    /// <returns>The updated element cursor.</returns>
    public DataBufferElement Add(Vector3 value)
    {
        EnsureCanWriteVector(3);

        Buffer.Set(ElementIndex, _nextValueIndex, _nextComponentIndex, value.X);
        Buffer.Set(ElementIndex, _nextValueIndex, _nextComponentIndex + 1, value.Y);
        Buffer.Set(ElementIndex, _nextValueIndex, _nextComponentIndex + 2, value.Z);
        AdvanceWithinValue(3);
        return this;
    }

    /// <summary>
    /// Appends a <see cref="Vector4"/> within the current value.
    /// </summary>
    /// <param name="value">The vector to store.</param>
    /// <returns>The updated element cursor.</returns>
    public DataBufferElement Add(Vector4 value)
    {
        EnsureCanWriteVector(4);

        Buffer.Set(ElementIndex, _nextValueIndex, _nextComponentIndex, value.X);
        Buffer.Set(ElementIndex, _nextValueIndex, _nextComponentIndex + 1, value.Y);
        Buffer.Set(ElementIndex, _nextValueIndex, _nextComponentIndex + 2, value.Z);
        Buffer.Set(ElementIndex, _nextValueIndex, _nextComponentIndex + 3, value.W);
        AdvanceWithinValue(4);
        return this;
    }

    /// <summary>
    /// Stores a scalar value at an explicit value/component location within the reserved element.
    /// This does not change the sequential cursor position.
    /// </summary>
    /// <typeparam name="TInput">The numeric type to store.</typeparam>
    /// <param name="valueIndex">The target value index.</param>
    /// <param name="componentIndex">The target component index.</param>
    /// <param name="value">The scalar value to store.</param>
    public readonly void Set<TInput>(int valueIndex, int componentIndex, TInput value) where TInput : INumber<TInput>
    {
        Buffer.Set(ElementIndex, valueIndex, componentIndex, value);
    }

    /// <summary>
    /// Stores a <see cref="Vector2"/> at an explicit value index within the reserved element.
    /// This does not change the sequential cursor position.
    /// </summary>
    /// <param name="valueIndex">The target value index.</param>
    /// <param name="value">The vector value to store.</param>
    public readonly void SetVector2(int valueIndex, Vector2 value)
    {
        Buffer.SetVector2(ElementIndex, valueIndex, value);
    }

    /// <summary>
    /// Stores a <see cref="Vector3"/> at an explicit value index within the reserved element.
    /// This does not change the sequential cursor position.
    /// </summary>
    /// <param name="valueIndex">The target value index.</param>
    /// <param name="value">The vector value to store.</param>
    public readonly void SetVector3(int valueIndex, Vector3 value)
    {
        Buffer.SetVector3(ElementIndex, valueIndex, value);
    }

    /// <summary>
    /// Stores a <see cref="Vector4"/> at an explicit value index within the reserved element.
    /// This does not change the sequential cursor position.
    /// </summary>
    /// <param name="valueIndex">The target value index.</param>
    /// <param name="value">The vector value to store.</param>
    public readonly void SetVector4(int valueIndex, Vector4 value)
    {
        Buffer.SetVector4(ElementIndex, valueIndex, value);
    }

    private readonly void EnsureCanWriteScalar()
    {
        if (IsComplete)
            throw new InvalidOperationException($"Element {ElementIndex} in {nameof(DataBuffer)} has no remaining slots.");
    }

    private readonly void EnsureCanWriteVector(int componentCount)
    {
        EnsureCanWriteScalar();

        if (_nextComponentIndex + componentCount > Buffer.ComponentCount)
        {
            throw new InvalidOperationException(
                $"Cannot append a {componentCount}-component vector at value {_nextValueIndex}, component {_nextComponentIndex} because it would cross into the next value.");
        }
    }

    private void AdvanceScalar()
    {
        _nextComponentIndex++;
        if (_nextComponentIndex >= Buffer.ComponentCount)
        {
            _nextComponentIndex = 0;
            _nextValueIndex++;
        }
    }

    private void AdvanceWithinValue(int componentCount)
    {
        _nextComponentIndex += componentCount;
        if (_nextComponentIndex >= Buffer.ComponentCount)
        {
            _nextComponentIndex = 0;
            _nextValueIndex++;
        }
    }
}
