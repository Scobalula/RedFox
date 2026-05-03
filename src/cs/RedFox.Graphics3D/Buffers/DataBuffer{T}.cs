using System;
using System.Collections.Generic;
using System.Numerics;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Text;
using RedFox.Graphics3D.Rendering;

namespace RedFox.Graphics3D.Buffers
{
    public sealed class DataBuffer<T> : DataBuffer where T : unmanaged, INumber<T>
    {
        internal T[] _items;

        internal int _elementCount;
        internal int _valueCount;
        internal int _componentCount;

        private static readonly T[] s_emptyArray = [];

        /// <inheritdoc/>
        public override int ElementCount => _elementCount;

        /// <inheritdoc/>
        public override int ValueCount => _valueCount;

        /// <inheritdoc/>
        public override int ComponentCount => _componentCount;

        /// <inheritdoc/>
        public override bool IsReadOnly => false;

        /// <summary>
        /// Gets a mutable span over the populated component storage.
        /// </summary>
        /// <returns>A span covering all initialized scalar components.</returns>
        public Span<T> AsSpan() => _items.AsSpan(0, TotalComponentCount);

        /// <inheritdoc/>
        public override bool TryGetReadOnlySpan<TResult>(out ReadOnlySpan<TResult> span)
        {
            if (typeof(TResult) == typeof(T))
            {
                span = MemoryMarshal.Cast<T, TResult>(AsReadOnlySpan());
                return true;
            }

            span = default;
            return false;
        }

        /// <inheritdoc/>
        public override bool TryGetSpan<TResult>(out Span<TResult> span)
        {
            if (typeof(TResult) == typeof(T))
            {
                span = MemoryMarshal.Cast<T, TResult>(AsSpan());
                return true;
            }

            span = default;
            return false;
        }

        /// <inheritdoc/>
        public override bool TryGetGpuBufferData(out GpuBufferData bufferData)
        {
            if (!DataBufferGpuElementTypes.TryGet<T>(out GpuBufferElementType elementType, out int componentSizeBytes))
            {
                bufferData = default;
                return false;
            }

            int valueStrideBytes = _componentCount * componentSizeBytes;
            int elementStrideBytes = _valueCount * valueStrideBytes;
            bufferData = new GpuBufferData(
                MemoryMarshal.AsBytes(AsReadOnlySpan()),
                elementType,
                _elementCount,
                _valueCount,
                _componentCount,
                elementStrideBytes,
                valueStrideBytes,
                componentSizeBytes);
            return true;
        }

        /// <summary>
        /// Gets a read-only span over the populated component storage.
        /// </summary>
        /// <returns>A read-only span covering all initialized scalar components.</returns>
        public ReadOnlySpan<T> AsReadOnlySpan() => AsSpan();

        /// <summary>
        /// Copies the populated component storage into the provided destination span.
        /// </summary>
        /// <param name="destination">The destination span.</param>
        public void CopyTo(Span<T> destination)
        {
            if (destination.Length < TotalComponentCount)
                throw new ArgumentException($"Destination span must be at least {TotalComponentCount} elements.", nameof(destination));

            AsReadOnlySpan().CopyTo(destination);
        }

        /// <summary>
        /// Copies the populated component storage into the provided array.
        /// </summary>
        /// <param name="destination">The destination array.</param>
        /// <param name="destinationIndex">The index in the destination array at which copying begins.</param>
        public void CopyTo(T[] destination, int destinationIndex = 0)
        {
            ArgumentNullException.ThrowIfNull(destination);

            if (destinationIndex < 0 || destinationIndex > destination.Length)
                throw new ArgumentOutOfRangeException(nameof(destinationIndex));

            if (destination.Length - destinationIndex < TotalComponentCount)
                throw new ArgumentException($"Destination array must have space for at least {TotalComponentCount} elements from index {destinationIndex}.", nameof(destination));

            AsReadOnlySpan().CopyTo(destination.AsSpan(destinationIndex));
        }

        /// <summary>
        /// Returns a copy of the populated component storage.
        /// </summary>
        /// <returns>An array containing the initialized component data.</returns>
        public T[] ToArray() => AsReadOnlySpan().ToArray();

        /// <summary>
        /// Initializes a new instance of the DataBuffer class.
        /// </summary>
        public DataBuffer() : this(0, 1, 1)
        {
        }

        /// <summary>
        /// Initializes a new instance of the DataBuffer class using the specified values.
        /// </summary>
        /// <param name="capacity">The initial capacity of the array of elements.</param>
        public DataBuffer(int capacity) : this(capacity, 1, 1)
        {

        }

        /// <summary>
        /// Initializes a new instance of the DataBuffer class using the specified values.
        /// </summary>
        /// <param name="capacity">The initial capacity of the array of elements.</param>
        /// <param name="valueCount">The total number of values contained in the buffer.</param>
        public DataBuffer(int capacity, int valueCount) : this(capacity, valueCount, 1)
        {
        }

        /// <summary>
        /// Initializes a new instance of the DataBuffer class using the specified values.
        /// </summary>
        /// <param name="capacity">The initial capacity of the array of elements.</param>
        /// <param name="valueCount">The total number of values contained in the buffer.</param>
        /// <param name="componentCount">The number of components per value.</param>
        public DataBuffer(int capacity, int valueCount, int componentCount)
        {
            _items = capacity > 0 ? new T[capacity * valueCount * componentCount] : s_emptyArray;
            _valueCount = valueCount;
            _componentCount = componentCount;
        }

        /// <summary>
        /// Initializes a new instance of the DataBuffer class using the specified values.
        /// </summary>
        /// <param name="elements">The elements array containing the data to be stored in the buffer.</param>
        public DataBuffer(T[] elements) : this(elements, 1, 1)
        {
        }

        /// <summary>
        /// Initializes a new instance of the DataBuffer class using the specified values.
        /// </summary>
        /// <param name="elements">The elements array containing the data to be stored in the buffer.</param>
        /// <param name="valueCount">The total number of values contained in the buffer.</param>
        public DataBuffer(T[] elements, int valueCount) : this(elements, valueCount, 1)
        {
        }

        /// <summary>
        /// Initializes a new instance of the DataBuffer class using the specified values.
        /// </summary>
        /// <param name="elements">The elements array containing the data to be stored in the buffer.</param>
        /// <param name="valueCount">The total number of values contained in the buffer.</param>
        /// <param name="componentCount">The number of components per value.</param>
        public DataBuffer(T[] elements, int valueCount, int componentCount)
        {
            _items = elements;
            _valueCount = valueCount;
            _componentCount = componentCount;
            _elementCount = elements.Length == 0 ? 0 : elements.Length / (valueCount * componentCount);
        }

        /// <summary>
        /// Initializes a new instance of the DataBuffer class using the specified values.
        /// </summary>
        /// <param name="values">The byte array containing the raw data to be stored in the buffer.</param>
        public DataBuffer(byte[] values) : this(values, 1, 1)
        {

        }

        /// <summary>
        /// Initializes a new instance of the DataBuffer class using the specified values.
        /// </summary>
        /// <param name="values">The byte array containing the raw data to be stored in the buffer.</param>
        /// <param name="valueCount">The total number of values contained in the buffer.</param>
        public DataBuffer(byte[] values, int valueCount) : this(values, valueCount, 1)
        {
        }

        /// <summary>
        /// Initializes a new instance of the DataBuffer class using the specified values.
        /// </summary>
        /// <param name="values">The byte array containing the raw data to be stored in the buffer.</param>
        /// <param name="valueCount">The total number of values contained in the buffer.</param>
        /// <param name="componentCount">The number of components per value.</param>
        public DataBuffer(byte[] values, int valueCount, int componentCount)
        {
            _items = MemoryMarshal.Cast<byte, T>(values).ToArray();
            _valueCount = valueCount;
            _componentCount = componentCount;
            _elementCount = _items.Length == 0 ? 0 : _items.Length / (valueCount * componentCount);
        }

        /// <inheritdoc/>
        public override TResult Get<TResult>(int elementIndex, int valueIndex, int componentIndex)
        {
            if (elementIndex >= _elementCount || elementIndex < 0)
                throw new ArgumentOutOfRangeException(nameof(elementIndex), $"Element index must be between 0 and {_elementCount}.");
            if (valueIndex >= _valueCount || valueIndex < 0)
                throw new ArgumentOutOfRangeException(nameof(valueIndex), $"Value index must be between 0 and {_valueCount}.");
            if (componentIndex >= _componentCount || componentIndex < 0)
                throw new ArgumentOutOfRangeException(nameof(componentIndex), $"Component index must be between 0 and {_componentCount}.");

            var value = _items[(elementIndex * _valueCount * _componentCount) + (valueIndex * _componentCount) + componentIndex];

            if (typeof(TResult) == typeof(T))
                return Unsafe.As<T, TResult>(ref value);
            else
                return TResult.CreateSaturating(value);
        }

        /// <inheritdoc/>
        public override void Set<TInput>(int elementIndex, int valueIndex, int componentIndex, TInput value)
        {
            if (elementIndex >= _elementCount || elementIndex < 0)
                throw new ArgumentOutOfRangeException(nameof(elementIndex), $"Element index must be between 0 and {_elementCount}.");
            if (valueIndex >= _valueCount || valueIndex < 0)
                throw new ArgumentOutOfRangeException(nameof(valueIndex), $"Value index must be between 0 and {_valueCount}.");
            if (componentIndex >= _componentCount || componentIndex < 0)
                throw new ArgumentOutOfRangeException(nameof(componentIndex), $"Component index must be between 0 and {_componentCount}.");

            var index = (elementIndex * _valueCount * _componentCount) + (valueIndex * _componentCount) + componentIndex;

            // Set the first value
            if (typeof(TInput) == typeof(T))
            {
                _items[index] = Unsafe.As<TInput, T>(ref value);
            }
            else
            {
                _items[index] = T.CreateSaturating(value);
            }
        }

        /// <inheritdoc/>
        protected override int ReserveElement()
        {
            int elementIndex = _elementCount;
            int requiredCapacity = (elementIndex + 1) * _valueCount * _componentCount;
            EnsureCapacity(requiredCapacity);
            _elementCount++;
            return elementIndex;
        }

        /// <inheritdoc/>
        public override void Add<TInput>(int elementIndex, int valueIndex, int componentIndex, TInput value)
        {
            if (elementIndex > _elementCount || elementIndex < 0)
                throw new ArgumentOutOfRangeException(nameof(elementIndex), $"Element index must be between 0 and {_elementCount}.");
            if (valueIndex >= _valueCount || valueIndex < 0)
                throw new ArgumentOutOfRangeException(nameof(valueIndex), $"Value index must be between 0 and {_valueCount}.");
            if (componentIndex >= _componentCount || componentIndex < 0)
                throw new ArgumentOutOfRangeException(nameof(componentIndex), $"Component index must be between 0 and {_componentCount}.");

            var index = (elementIndex * _valueCount * _componentCount) + (valueIndex * _componentCount) + componentIndex;

            int requiredCapacity = Math.Max(index + 1, (elementIndex + 1) * _valueCount * _componentCount);
            EnsureCapacity(requiredCapacity);

            // Set the first value
            if (typeof(TInput) == typeof(T))
            {
                _items[index] = Unsafe.As<TInput, T>(ref value);
            }
            else
            {
                _items[index] = T.CreateSaturating(value);
            }

            if (elementIndex == _elementCount)
            {
                _elementCount++;
            }
        }

        /// <summary>
        /// Ensures that the internal storage can accommodate at least the specified number of elements.
        /// </summary>
        /// <param name="capacity">The minimum number of elements that the internal storage must be able to hold.</param>
        public void EnsureCapacity(int capacity)
        {
            ArgumentOutOfRangeException.ThrowIfNegative(capacity);

            if (_items.Length < capacity)
            {
                var newSize = Math.Max(_items.Length * 2, capacity);
                Array.Resize(ref _items, newSize);
            }
        }

        //private void ValidateElementIndex(int elementIndex, bool allowAtEnd)
        //{
        //    var maxIndex = allowAtEnd ? _count : _count - 1;

        //    if (elementIndex < 0 || elementIndex > maxIndex)
        //        throw new ArgumentOutOfRangeException(nameof(elementIndex), $"Element index must be between 0 and {maxIndex}.");
        //}
    }
}
