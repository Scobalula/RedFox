using System.Numerics;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace RedFox.Graphics3D.Buffers
{
    /// <summary>
    /// Provides a read-only, strided view over a byte array as a buffer of elements, values, and components of type T,
    /// without copying the underlying data.
    /// </summary>
    /// <typeparam name="T">The unmanaged numeric type of each component in the buffer. Must implement INumber<T>.</typeparam>
    /// <param name="source">The underlying byte array containing the buffer data.</param>
    /// <param name="elementCount">The number of elements in the buffer view.</param>
    /// <param name="byteOffset">The byte offset in the source array where the first element starts.</param>
    /// <param name="byteStride">The number of bytes from the start of one element to the start of the next element in the source array.</param>
    /// <param name="valueCount">The number of values per element.</param>
    /// <param name="componentCount">The number of components per value.</param>
    /// <param name="byteValueStride">The number of bytes from the start of one value within an element to the start of the next value. If null,
    /// defaults to componentCount multiplied by the size of T.</param>
    public sealed class DataBufferView<T>(byte[] source, int elementCount, int byteOffset, int byteStride, int valueCount, int componentCount, int? byteValueStride) : DataBuffer where T : unmanaged, INumber<T>
    {
        public DataBufferView(byte[] source, int elementCount, int byteOffset, int byteStride)
            : this(source, elementCount, byteOffset, byteStride, valueCount: 1, componentCount: 1, byteValueStride: null)
        {
        }

        public DataBufferView(byte[] source, int elementCount, int byteOffset, int byteStride, int valueCount)
            : this(source, elementCount, byteOffset, byteStride, valueCount, componentCount: 1, byteValueStride: null)
        {
        }

        public DataBufferView(byte[] source, int elementCount, int byteOffset, int byteStride, int valueCount, int componentCount)
            : this(source, elementCount, byteOffset, byteStride, valueCount, componentCount, byteValueStride: null)
        {
        }

        private readonly byte[] _source = source;
        private readonly int _byteOffset = byteOffset;
        private readonly int _byteStride = byteStride;
        private readonly int _byteValueStride = byteValueStride ?? (componentCount * Unsafe.SizeOf<T>());
        private readonly int _elementCount = elementCount;
        private readonly int _valueCount = valueCount;
        private readonly int _componentCount = componentCount;

        /// <inheritdoc/>
        public override int ElementCount => _elementCount;

        /// <inheritdoc/>
        public override int ValueCount => _valueCount;

        /// <inheritdoc/>
        public override int ComponentCount => _componentCount;

        /// <inheritdoc/>
        public override bool IsReadOnly => true;

        /// <inheritdoc/>
        public override bool TryGetReadOnlySpan<TResult>(out ReadOnlySpan<TResult> span)
        {
            if (typeof(TResult) == typeof(T) && IsContiguous)
            {
                ReadOnlySpan<byte> source = _source.AsSpan(_byteOffset, TotalComponentCount * Unsafe.SizeOf<T>());
                span = MemoryMarshal.Cast<byte, TResult>(source);
                return true;
            }

            span = Pack<TResult>();
            return true;
        }

        /// <summary>
        /// Gets the byte offset of the first element within the source array.
        /// </summary>
        public int ByteOffset => _byteOffset;

        /// <summary>
        /// Gets the number of bytes from the start of one element to the start
        /// of the next in the source array.
        /// </summary>
        public int ByteStride => _byteStride;

        /// <summary>
        /// Gets the number of bytes from the start of one value within an element to the next.
        /// </summary>
        public int ByteValueStride => _byteValueStride;

        private bool IsContiguous
            => _byteStride == _valueCount * _componentCount * Unsafe.SizeOf<T>()
                && _byteValueStride == _componentCount * Unsafe.SizeOf<T>();

        /// <summary>
        /// Creates a tightly-packed (non-strided) view over the entire source array.
        /// Equivalent to a <see cref="DataBuffer{T}"/> backed by a byte array,
        /// but without copying the data.
        /// </summary>
        /// <param name="source">The underlying byte array.</param>
        /// <param name="valueCount">Values per element.</param>
        /// <param name="componentCount">Components per value.</param>
        /// <returns>A new <see cref="DataBufferView{T}"/> covering all elements.</returns>
        public static DataBufferView<T> CreatePacked(byte[] source)
            => CreatePacked(source, valueCount: 1, componentCount: 1);

        public static DataBufferView<T> CreatePacked(byte[] source, int valueCount)
            => CreatePacked(source, valueCount, componentCount: 1);

        public static DataBufferView<T> CreatePacked(byte[] source, int valueCount, int componentCount)
        {
            int sizeOfT = Unsafe.SizeOf<T>();
            int elemSize = valueCount * componentCount * sizeOfT;
            int elementCount = source.Length / elemSize;

            return new DataBufferView<T>(source, elementCount, 0, elemSize, valueCount, componentCount);
        }

        /// <inheritdoc/>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public override TResult Get<TResult>(int elementIndex, int valueIndex, int componentIndex)
        {
            int bytePos = _byteOffset + elementIndex * _byteStride + valueIndex * _byteValueStride + componentIndex * Unsafe.SizeOf<T>();

            var value = MemoryMarshal.Read<T>(_source.AsSpan(bytePos));

            if (typeof(TResult) == typeof(T))
                return Unsafe.As<T, TResult>(ref value);

            return TResult.CreateSaturating(value);
        }

        /// <inheritdoc/>
        protected override int ReserveElement()
            => throw new NotSupportedException("DataBufferView is read-only.");

        /// <inheritdoc/>
        public override void Set<TInput>(int elementIndex, int valueIndex, int componentIndex, TInput value)
            => throw new NotSupportedException("DataBufferView is read-only.");

        /// <inheritdoc/>
        public override void Add<TInput>(int elementIndex, int valueIndex, int componentIndex, TInput value)
            => throw new NotSupportedException("DataBufferView is read-only.");

        private TResult[] Pack<TResult>() where TResult : unmanaged, INumber<TResult>
        {
            TResult[] packed = new TResult[TotalComponentCount];
            int destinationIndex = 0;

            for (int elementIndex = 0; elementIndex < _elementCount; elementIndex++)
            {
                int elementOffset = _byteOffset + elementIndex * _byteStride;
                for (int valueIndex = 0; valueIndex < _valueCount; valueIndex++)
                {
                    int valueOffset = elementOffset + valueIndex * _byteValueStride;
                    for (int componentIndex = 0; componentIndex < _componentCount; componentIndex++)
                    {
                        int bytePos = valueOffset + componentIndex * Unsafe.SizeOf<T>();
                        T value = MemoryMarshal.Read<T>(_source.AsSpan(bytePos));
                        packed[destinationIndex++] = typeof(TResult) == typeof(T)
                            ? Unsafe.As<T, TResult>(ref value)
                            : TResult.CreateSaturating(value);
                    }
                }
            }

            return packed;
        }
    }
}
