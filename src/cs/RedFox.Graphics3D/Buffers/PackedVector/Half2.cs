using System.Numerics;

namespace RedFox.Graphics3D.Buffers.PackedVector
{
    /// <summary>
    /// Packs a 2-component half-precision floating-point vector into a single <see cref="uint"/>.
    /// </summary>
    public struct Half2 : IPackedVector<Half2>
    {
        private uint _packed;

        /// <inheritdoc/>
        public static int ComponentCount => 2;

        /// <summary>
        /// Gets or sets the raw packed value.
        /// </summary>
        public uint PackedValue { readonly get => _packed; set => _packed = value; }

        /// <summary>
        /// Initializes a new instance from a raw packed value.
        /// </summary>
        /// <param name="packedValue">The raw packed value.</param>
        public Half2(uint packedValue) => _packed = packedValue;

        /// <summary>
        /// Initializes a new instance from float components.
        /// </summary>
        /// <param name="x">The X component.</param>
        /// <param name="y">The Y component.</param>
        public Half2(float x, float y) => Pack(new Vector4(x, y, 0f, 0f));

        /// <inheritdoc/>
        public void Pack(Vector4 value)
        {
            _packed = (uint)BitConverter.HalfToUInt16Bits((Half)value.X)
                    | ((uint)BitConverter.HalfToUInt16Bits((Half)value.Y) << 16);
        }

        /// <inheritdoc/>
        public readonly Vector4 Unpack() => new(
            (float)BitConverter.UInt16BitsToHalf((ushort)(_packed & 0xFFFF)),
            (float)BitConverter.UInt16BitsToHalf((ushort)(_packed >> 16)),
            0f, 0f);
    }
}
