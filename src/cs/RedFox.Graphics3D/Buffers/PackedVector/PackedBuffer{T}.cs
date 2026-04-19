using System.Numerics;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace RedFox.Graphics3D.Buffers.PackedVector
{
    /// <summary>
    /// Provides a typed buffer that stores packed vector elements with optional scale and offset transforms.
    /// </summary>
    /// <typeparam name="TPacked">The packed vector type stored in the buffer.</typeparam>
    public sealed class PackedBuffer<TPacked> : DataBuffer where TPacked : unmanaged, IPackedVector<TPacked>
    {
        internal TPacked[] _items;
        internal int _elementCount;
        internal int _valueCount;
        private readonly Vector4 _scale;
        private readonly Vector4 _offset;
        private readonly float[] _scaleComponents;
        private readonly float[] _offsetComponents;

        private static readonly TPacked[] s_emptyArray = [];

        /// <inheritdoc/>
        public override int ElementCount => _elementCount;

        /// <inheritdoc/>
        public override int ValueCount => _valueCount;

        /// <inheritdoc/>
        public override int ComponentCount => TPacked.ComponentCount;

        /// <inheritdoc/>
        public override bool IsReadOnly => false;

        /// <summary>
        /// Gets a mutable span over the populated packed element storage.
        /// </summary>
        /// <returns>A span covering all initialized packed elements.</returns>
        public Span<TPacked> AsSpan() => _items.AsSpan(0, _elementCount * _valueCount);

        /// <summary>
        /// Gets a read-only span over the populated packed element storage.
        /// </summary>
        /// <returns>A read-only span covering all initialized packed elements.</returns>
        public ReadOnlySpan<TPacked> AsReadOnlySpan() => AsSpan();

        /// <summary>
        /// Copies the populated packed element storage into the provided destination span.
        /// </summary>
        /// <param name="destination">The destination span.</param>
        public void CopyTo(Span<TPacked> destination)
        {
            var count = _elementCount * _valueCount;
            if (destination.Length < count)
                throw new ArgumentException($"Destination span must be at least {count} elements.", nameof(destination));
            AsReadOnlySpan().CopyTo(destination);
        }

        /// <summary>
        /// Returns a copy of the populated packed element storage.
        /// </summary>
        /// <returns>An array containing the initialized packed element data.</returns>
        public TPacked[] ToArray() => AsReadOnlySpan().ToArray();

        /// <summary>
        /// Initializes a new instance of the PackedBuffer class.
        /// </summary>
        public PackedBuffer() : this(0, 1, Vector4.One, Vector4.Zero) { }

        /// <summary>
        /// Initializes a new instance of the PackedBuffer class using the specified capacity.
        /// </summary>
        /// <param name="capacity">The initial capacity of the array of elements.</param>
        public PackedBuffer(int capacity) : this(capacity, 1, Vector4.One, Vector4.Zero) { }

        /// <summary>
        /// Initializes a new instance of the PackedBuffer class using the specified values.
        /// </summary>
        /// <param name="capacity">The initial capacity of the array of elements.</param>
        /// <param name="valueCount">The total number of values contained in the buffer.</param>
        public PackedBuffer(int capacity, int valueCount) : this(capacity, valueCount, Vector4.One, Vector4.Zero) { }

        /// <summary>
        /// Initializes a new instance of the PackedBuffer class using the specified values.
        /// </summary>
        /// <param name="capacity">The initial capacity of the array of elements.</param>
        /// <param name="valueCount">The total number of values contained in the buffer.</param>
        /// <param name="scale">The scale applied to unpacked component values.</param>
        /// <param name="offset">The offset applied to unpacked component values.</param>
        public PackedBuffer(int capacity, int valueCount, Vector4 scale, Vector4 offset)
        {
            _items = capacity > 0 ? new TPacked[capacity * valueCount] : s_emptyArray;
            _valueCount = valueCount;
            _scale = scale;
            _offset = offset;
            _scaleComponents = [scale.X, scale.Y, scale.Z, scale.W];
            _offsetComponents = [offset.X, offset.Y, offset.Z, offset.W];
        }

        /// <summary>
        /// Initializes a new instance of the PackedBuffer class using the specified elements.
        /// </summary>
        /// <param name="elements">The packed elements array containing the data to be stored in the buffer.</param>
        public PackedBuffer(TPacked[] elements) : this(elements, 1, Vector4.One, Vector4.Zero) { }

        /// <summary>
        /// Initializes a new instance of the PackedBuffer class using the specified elements.
        /// </summary>
        /// <param name="elements">The packed elements array containing the data to be stored in the buffer.</param>
        /// <param name="valueCount">The total number of values contained in the buffer.</param>
        public PackedBuffer(TPacked[] elements, int valueCount) : this(elements, valueCount, Vector4.One, Vector4.Zero) { }

        /// <summary>
        /// Initializes a new instance of the PackedBuffer class using the specified elements.
        /// </summary>
        /// <param name="elements">The packed elements array containing the data to be stored in the buffer.</param>
        /// <param name="valueCount">The total number of values contained in the buffer.</param>
        /// <param name="scale">The scale applied to unpacked component values.</param>
        /// <param name="offset">The offset applied to unpacked component values.</param>
        public PackedBuffer(TPacked[] elements, int valueCount, Vector4 scale, Vector4 offset)
        {
            _items = elements;
            _valueCount = valueCount;
            _elementCount = elements.Length == 0 ? 0 : elements.Length / valueCount;
            _scale = scale;
            _offset = offset;
            _scaleComponents = [scale.X, scale.Y, scale.Z, scale.W];
            _offsetComponents = [offset.X, offset.Y, offset.Z, offset.W];
        }

        /// <summary>
        /// Initializes a new instance of the PackedBuffer class using the specified byte data.
        /// </summary>
        /// <param name="values">The byte array containing the raw data to be stored in the buffer.</param>
        public PackedBuffer(byte[] values) : this(values, 1, Vector4.One, Vector4.Zero) { }

        /// <summary>
        /// Initializes a new instance of the PackedBuffer class using the specified byte data.
        /// </summary>
        /// <param name="values">The byte array containing the raw data to be stored in the buffer.</param>
        /// <param name="valueCount">The total number of values contained in the buffer.</param>
        public PackedBuffer(byte[] values, int valueCount) : this(values, valueCount, Vector4.One, Vector4.Zero) { }

        /// <summary>
        /// Initializes a new instance of the PackedBuffer class using the specified byte data.
        /// </summary>
        /// <param name="values">The byte array containing the raw data to be stored in the buffer.</param>
        /// <param name="valueCount">The total number of values contained in the buffer.</param>
        /// <param name="scale">The scale applied to unpacked component values.</param>
        /// <param name="offset">The offset applied to unpacked component values.</param>
        public PackedBuffer(byte[] values, int valueCount, Vector4 scale, Vector4 offset)
        {
            _items = MemoryMarshal.Cast<byte, TPacked>(values).ToArray();
            _valueCount = valueCount;
            _elementCount = _items.Length == 0 ? 0 : _items.Length / valueCount;
            _scale = scale;
            _offset = offset;
            _scaleComponents = [scale.X, scale.Y, scale.Z, scale.W];
            _offsetComponents = [offset.X, offset.Y, offset.Z, offset.W];
        }

        private Vector4 UnpackAndTransform(ref TPacked packed)
        {
            var v = packed.Unpack();
            return v * _scale + _offset;
        }

        private float GetScaledComponent(ref TPacked packed, int componentIndex)
        {
            var v = UnpackAndTransform(ref packed);
            return componentIndex switch
            {
                0 => v.X,
                1 => v.Y,
                2 => v.Z,
                3 => v.W,
                _ => 0f
            };
        }

        /// <inheritdoc/>
        public override TResult Get<TResult>(int elementIndex, int valueIndex, int componentIndex)
        {
            if (elementIndex < 0 || elementIndex >= _elementCount)
                throw new ArgumentOutOfRangeException(nameof(elementIndex), $"Element index must be between 0 and {_elementCount}.");
            if (valueIndex < 0 || valueIndex >= _valueCount)
                throw new ArgumentOutOfRangeException(nameof(valueIndex), $"Value index must be between 0 and {_valueCount}.");
            if (componentIndex < 0 || componentIndex >= TPacked.ComponentCount)
                throw new ArgumentOutOfRangeException(nameof(componentIndex), $"Component index must be between 0 and {TPacked.ComponentCount}.");

            var idx = elementIndex * _valueCount + valueIndex;
            var value = GetScaledComponent(ref _items[idx], componentIndex);

            if (typeof(TResult) == typeof(float))
                return Unsafe.As<float, TResult>(ref value);

            return TResult.CreateSaturating(value);
        }

        /// <inheritdoc/>
        public override void Set<TInput>(int elementIndex, int valueIndex, int componentIndex, TInput value)
        {
            if (elementIndex < 0 || elementIndex >= _elementCount)
                throw new ArgumentOutOfRangeException(nameof(elementIndex), $"Element index must be between 0 and {_elementCount}.");
            if (valueIndex < 0 || valueIndex >= _valueCount)
                throw new ArgumentOutOfRangeException(nameof(valueIndex), $"Value index must be between 0 and {_valueCount}.");
            if (componentIndex < 0 || componentIndex >= TPacked.ComponentCount)
                throw new ArgumentOutOfRangeException(nameof(componentIndex), $"Component index must be between 0 and {TPacked.ComponentCount}.");

            var idx = elementIndex * _valueCount + valueIndex;
            var floatValue = typeof(TInput) == typeof(float)
                ? Unsafe.As<TInput, float>(ref value)
                : float.CreateSaturating(value);

            SetComponentInternal(idx, componentIndex, floatValue);
        }

        /// <inheritdoc/>
        protected override int ReserveElement()
        {
            int elementIndex = _elementCount;
            int requiredCapacity = (elementIndex + 1) * _valueCount;
            EnsureCapacity(requiredCapacity);
            _elementCount++;
            return elementIndex;
        }

        /// <inheritdoc/>
        public override void Add<TInput>(int elementIndex, int valueIndex, int componentIndex, TInput value)
        {
            if (elementIndex < 0 || elementIndex > _elementCount)
                throw new ArgumentOutOfRangeException(nameof(elementIndex), $"Element index must be between 0 and {_elementCount}.");
            if (valueIndex < 0 || valueIndex >= _valueCount)
                throw new ArgumentOutOfRangeException(nameof(valueIndex), $"Value index must be between 0 and {_valueCount}.");
            if (componentIndex < 0 || componentIndex >= TPacked.ComponentCount)
                throw new ArgumentOutOfRangeException(nameof(componentIndex), $"Component index must be between 0 and {TPacked.ComponentCount}.");

            var idx = elementIndex * _valueCount + valueIndex;
            EnsureCapacity(Math.Max(idx + 1, (elementIndex + 1) * _valueCount));

            var floatValue = typeof(TInput) == typeof(float)
                ? Unsafe.As<TInput, float>(ref value)
                : float.CreateSaturating(value);

            SetComponentInternal(idx, componentIndex, floatValue);

            if (elementIndex == _elementCount)
                _elementCount++;
        }

        private void SetComponentInternal(int index, int componentIndex, float floatValue)
        {
            ref var packed = ref _items[index];

            var targetValue = _scaleComponents[componentIndex] != 0f
                ? (floatValue - _offsetComponents[componentIndex]) / _scaleComponents[componentIndex]
                : floatValue;

            var v = packed.Unpack();

            switch (componentIndex)
            {
                case 0: v = new Vector4(targetValue, v.Y, v.Z, v.W); break;
                case 1: v = new Vector4(v.X, targetValue, v.Z, v.W); break;
                case 2: v = new Vector4(v.X, v.Y, targetValue, v.W); break;
                case 3: v = new Vector4(v.X, v.Y, v.Z, targetValue); break;
            }

            packed.Pack(v);
            _items[index] = packed;
        }

        /// <summary>
        /// Ensures that the internal storage can accommodate at least the specified number of packed elements.
        /// </summary>
        /// <param name="capacity">The minimum number of packed elements that the internal storage must be able to hold.</param>
        public void EnsureCapacity(int capacity)
        {
            ArgumentOutOfRangeException.ThrowIfNegative(capacity);
            if (_items.Length < capacity)
            {
                var newSize = Math.Max(_items.Length * 2, capacity);
                Array.Resize(ref _items, newSize);
            }
        }
    }
}
