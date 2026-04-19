using System.Numerics;

namespace RedFox.Graphics3D.Buffers;

/// <summary>
/// Provides an abstract base class for managing a structured collection of data elements, each composed of multiple values and components.
/// </summary>
public abstract class DataBuffer
{
    /// <summary>
    /// Gets the total number of elements in the buffer.
    /// </summary>
    public abstract int ElementCount { get; }

    /// <summary>
    /// Gets the fixed number of values associated with each element.
    /// This is the per-element stride/max slot count, not a per-element active value count.
    /// </summary>
    public abstract int ValueCount { get; }

    /// <summary>
    /// Gets the number of components that make up each value.
    /// </summary>
    public abstract int ComponentCount { get; }

    /// <summary>
    /// Gets a value indicating whether the buffer is read-only.
    /// </summary>
    public abstract bool IsReadOnly { get; }

    /// <summary>
    /// Gets the total number of scalar components stored in the buffer.
    /// </summary>
    public int TotalComponentCount => ElementCount * ValueCount * ComponentCount;

    /// <summary>
    /// Gets the value of a specific component from the buffer at the given element, value, and component indices.
    /// </summary>
    /// <typeparam name="T">The numeric type to retrieve, which must implement <see cref="INumber{T}"/>.</typeparam>
    /// <param name="elementIndex">The zero-based index of the element.</param>
    /// <param name="valueIndex">The zero-based index of the value within the element.</param>
    /// <param name="componentIndex">The zero-based index of the component within the value.</param>
    /// <returns>The component value of type <typeparamref name="T"/> at the specified indices.</returns>
    public abstract T Get<T>(int elementIndex, int valueIndex, int componentIndex) where T : INumber<T>;

    /// <summary>
    /// Copies the component values for the specified element and value indices into the provided destination span.
    /// </summary>
    /// <typeparam name="T">The numeric type to retrieve, which must implement <see cref="INumber{T}"/>.</typeparam>
    /// <param name="elementIndex">The zero-based index of the element.</param>
    /// <param name="valueIndex">The zero-based index of the value within the element.</param>
    /// <param name="destination">The span to receive the component values.</param>
    public void Get<T>(int elementIndex, int valueIndex, Span<T> destination) where T : INumber<T>
    {
        for (int i = 0; i < destination.Length; i++)
        {
            destination[i] = Get<T>(elementIndex, valueIndex, i);
        }
    }

    /// <summary>
    /// Sets the value of a specific component in the buffer at the given element, value, and component indices.
    /// </summary>
    /// <typeparam name="TInput">The numeric type to store, which must implement <see cref="INumber{TInput}"/>.</typeparam>
    /// <param name="elementIndex">The zero-based index of the element.</param>
    /// <param name="valueIndex">The zero-based index of the value within the element.</param>
    /// <param name="componentIndex">The zero-based index of the component within the value.</param>
    /// <param name="value">The value to set.</param>
    public abstract void Set<TInput>(int elementIndex, int valueIndex, int componentIndex, TInput value) where TInput : INumber<TInput>;

    /// <summary>
    /// Reserves storage for one new element and returns its element index.
    /// </summary>
    /// <returns>The zero-based index of the newly reserved element.</returns>
    protected abstract int ReserveElement();

    /// <summary>
    /// Adds a value to a specific component in the buffer at the given element, value, and component indices. If <paramref name="elementIndex"/> equals the number of elements, a new element is added.
    /// </summary>
    /// <typeparam name="TInput">The numeric type to store, which must implement <see cref="INumber{TInput}"/>.</typeparam>
    /// <param name="elementIndex">The zero-based index of the element. If equal to <see cref="ElementCount"/>, a new element is added.</param>
    /// <param name="valueIndex">The zero-based index of the value within the element.</param>
    /// <param name="componentIndex">The zero-based index of the component within the value.</param>
    /// <param name="value">The value to add.</param>
    public abstract void Add<TInput>(int elementIndex, int valueIndex, int componentIndex, TInput value) where TInput : INumber<TInput>;

    /// <summary>
    /// Reserves a new element and returns a cursor for sequential writes within it.
    /// </summary>
    /// <returns>A cursor positioned at the first value/component of the new element.</returns>
    public DataBufferElement Add()
    {
        int elementIndex = ReserveElement();
        return new DataBufferElement(this, elementIndex);
    }

    /// <summary>
    /// Appends a new element and writes a single scalar value to value 0/component 0.
    /// Remaining slots in the element stay default-initialized.
    /// </summary>
    /// <typeparam name="TInput">The numeric type to store.</typeparam>
    /// <param name="value">The scalar value to write.</param>
    /// <returns>The updated cursor for the appended element.</returns>
    public DataBufferElement Add<TInput>(TInput value) where TInput : INumber<TInput>
    {
        EnsureCanAppendToNewElement(1);
        var element = Add();
        return element.Add(value);
    }

    /// <summary>
    /// Appends a new element and writes a <see cref="Vector2"/> to value 0 starting at component 0.
    /// </summary>
    /// <param name="value">The vector value to write.</param>
    /// <returns>The updated cursor for the appended element.</returns>
    public DataBufferElement Add(Vector2 value)
    {
        EnsureCanAppendToNewElement(2);
        var element = Add();
        return element.Add(value);
    }

    /// <summary>
    /// Appends a new element and writes a <see cref="Vector3"/> to value 0 starting at component 0.
    /// </summary>
    /// <param name="value">The vector value to write.</param>
    /// <returns>The updated cursor for the appended element.</returns>
    public DataBufferElement Add(Vector3 value)
    {
        EnsureCanAppendToNewElement(3);
        var element = Add();
        return element.Add(value);
    }

    /// <summary>
    /// Appends a new element and writes a <see cref="Vector4"/> to value 0 starting at component 0.
    /// </summary>
    /// <param name="value">The vector value to write.</param>
    /// <returns>The updated cursor for the appended element.</returns>
    public DataBufferElement Add(Vector4 value)
    {
        EnsureCanAppendToNewElement(4);
        var element = Add();
        return element.Add(value);
    }

    /// <summary>
    /// Creates a writable copy of the specified source buffer using the requested component type.
    /// </summary>
    /// <typeparam name="T">The unmanaged numeric type to store in the writable clone.</typeparam>
    /// <param name="source">The source buffer to clone.</param>
    /// <returns>A writable clone containing the source data converted to <typeparamref name="T"/>.</returns>
    public static DataBuffer<T> CloneToWritable<T>(DataBuffer source) where T : unmanaged, INumber<T>
    {
        ArgumentNullException.ThrowIfNull(source);

        DataBuffer<T> clone = new(source.ElementCount, source.ValueCount, source.ComponentCount);

        for (int elementIndex = 0; elementIndex < source.ElementCount; elementIndex++)
        {
            for (int valueIndex = 0; valueIndex < source.ValueCount; valueIndex++)
            {
                for (int componentIndex = 0; componentIndex < source.ComponentCount; componentIndex++)
                {
                    clone.Add(elementIndex, valueIndex, componentIndex, source.Get<T>(elementIndex, valueIndex, componentIndex));
                }
            }
        }

        return clone;
    }

    /// <summary>
    /// Retrieves a <see cref="Vector2"/> from the specified element and value indices.
    /// If the buffer has fewer than 2 components, the missing components are set to zero.
    /// </summary>
    /// <param name="elementIndex">The zero-based index of the element.</param>
    /// <param name="valueIndex">The zero-based index of the value within the element.</param>
    /// <returns>A <see cref="Vector2"/> containing the first two components of the value.</returns>
    public virtual Vector2 GetVector2(int elementIndex, int valueIndex)
    {
        Span<float> buffer = stackalloc float[2];
        int count = Math.Min(2, ComponentCount);
        for (int i = 0; i < count; i++)
            buffer[i] = Get<float>(elementIndex, valueIndex, i);
        return new Vector2(buffer);
    }

    /// <summary>
    /// Stores a <see cref="Vector2"/> into the specified element/value starting at component 0.
    /// </summary>
    /// <param name="elementIndex">The zero-based index of the element.</param>
    /// <param name="valueIndex">The zero-based index of the value within the element.</param>
    /// <param name="value">The vector value to store.</param>
    public virtual void SetVector2(int elementIndex, int valueIndex, Vector2 value)
    {
        Set(elementIndex, valueIndex, 0, value.X);
        Set(elementIndex, valueIndex, 1, value.Y);
    }

    /// <summary>
    /// Retrieves a <see cref="Vector3"/> from the specified element and value indices.
    /// If the buffer has fewer than 3 components, the missing components are set to zero.
    /// </summary>
    /// <param name="elementIndex">The zero-based index of the element.</param>
    /// <param name="valueIndex">The zero-based index of the value within the element.</param>
    /// <returns>A <see cref="Vector3"/> containing the first three components of the value.</returns>
    public virtual Vector3 GetVector3(int elementIndex, int valueIndex)
    {
        Span<float> buffer = stackalloc float[3];
        int count = Math.Min(3, ComponentCount);
        for (int i = 0; i < count; i++)
            buffer[i] = Get<float>(elementIndex, valueIndex, i);
        return new Vector3(buffer);
    }

    /// <summary>
    /// Stores a <see cref="Vector3"/> into the specified element/value starting at component 0.
    /// </summary>
    /// <param name="elementIndex">The zero-based index of the element.</param>
    /// <param name="valueIndex">The zero-based index of the value within the element.</param>
    /// <param name="value">The vector value to store.</param>
    public virtual void SetVector3(int elementIndex, int valueIndex, Vector3 value)
    {
        Set(elementIndex, valueIndex, 0, value.X);
        Set(elementIndex, valueIndex, 1, value.Y);
        Set(elementIndex, valueIndex, 2, value.Z);
    }

    /// <summary>
    /// Retrieves a <see cref="Vector4"/> from the specified element and value indices.
    /// If the buffer has fewer than 4 components, the missing components are set to zero.
    /// </summary>
    /// <param name="elementIndex">The zero-based index of the element.</param>
    /// <param name="valueIndex">The zero-based index of the value within the element.</param>
    /// <returns>A <see cref="Vector4"/> containing the first four components of the value.</returns>
    public virtual Vector4 GetVector4(int elementIndex, int valueIndex)
    {
        Span<float> buffer = stackalloc float[4];
        int count = Math.Min(4, ComponentCount);
        for (int i = 0; i < count; i++)
            buffer[i] = Get<float>(elementIndex, valueIndex, i);
        return new Vector4(buffer);
    }

    /// <summary>
    /// Stores a <see cref="Vector4"/> into the specified element/value starting at component 0.
    /// </summary>
    /// <param name="elementIndex">The zero-based index of the element.</param>
    /// <param name="valueIndex">The zero-based index of the value within the element.</param>
    /// <param name="value">The vector value to store.</param>
    public virtual void SetVector4(int elementIndex, int valueIndex, Vector4 value)
    {
        Set(elementIndex, valueIndex, 0, value.X);
        Set(elementIndex, valueIndex, 1, value.Y);
        Set(elementIndex, valueIndex, 2, value.Z);
        Set(elementIndex, valueIndex, 3, value.W);
    }

    /// <summary>
    /// Retrieves a <see cref="Vector4"/> from the specified element and value indices, using a default value for missing components.
    /// If the buffer has fewer than 4 components, the missing components are set to <paramref name="defaultValue"/>.
    /// </summary>
    /// <param name="elementIndex">The zero-based index of the element.</param>
    /// <param name="valueIndex">The zero-based index of the value within the element.</param>
    /// <param name="defaultValue">The value to use for any vector components that are not present or cannot be retrieved.</param>
    /// <returns>A <see cref="Vector4"/> containing the retrieved values, with missing components set to <paramref name="defaultValue"/>.</returns>
    public virtual Vector4 GetVector4(int elementIndex, int valueIndex, float defaultValue)
    {
        Span<float> buffer =
        [
            defaultValue,
            defaultValue,
            defaultValue,
            defaultValue
        ];
        int count = Math.Min(4, ComponentCount);
        for (int i = 0; i < count; i++)
            buffer[i] = Get<float>(elementIndex, valueIndex, i);
        return new Vector4(buffer);
    }

    private void EnsureCanAppendToNewElement(int componentCount)
    {
        if (ValueCount <= 0 || ComponentCount < componentCount)
        {
            throw new InvalidOperationException($"Buffer layout must provide at least one value with {componentCount} components to append that shape.");
        }
    }
}
