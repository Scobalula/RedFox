using System.Numerics;

namespace RedFox.Graphics3D.Buffers.PackedVector
{
    /// <summary>
    /// Packs a 4-component half-precision floating-point vector into a single <see cref="ulong"/>.
    /// </summary>
    public struct Half4 : IPackedVector<Half4>
    {
        private ulong _packed;

        /// <inheritdoc/>
        public static int ComponentCount => 4;

        /// <summary>
        /// Gets or sets the raw packed value.
        /// </summary>
        public ulong PackedValue { readonly get => _packed; set => _packed = value; }

        /// <summary>
        /// Initializes a new instance from a raw packed value.
        /// </summary>
        /// <param name="packedValue">The raw packed value.</param>
        public Half4(ulong packedValue) => _packed = packedValue;

        /// <summary>
        /// Initializes a new instance from float components.
        /// </summary>
        /// <param name="x">The X component.</param>
        /// <param name="y">The Y component.</param>
        /// <param name="z">The Z component.</param>
        /// <param name="w">The W component.</param>
        public Half4(float x, float y, float z, float w) => Pack(new Vector4(x, y, z, w));

        /// <inheritdoc/>
        public void Pack(Vector4 value)
        {
            _packed = (ulong)BitConverter.HalfToUInt16Bits((Half)value.X)
                    | ((ulong)BitConverter.HalfToUInt16Bits((Half)value.Y) << 16)
                    | ((ulong)BitConverter.HalfToUInt16Bits((Half)value.Z) << 32)
                    | ((ulong)BitConverter.HalfToUInt16Bits((Half)value.W) << 48);
        }

        /// <inheritdoc/>
        public readonly Vector4 Unpack() => new(
            (float)BitConverter.UInt16BitsToHalf((ushort)(_packed & 0xFFFF)),
            (float)BitConverter.UInt16BitsToHalf((ushort)((_packed >> 16) & 0xFFFF)),
            (float)BitConverter.UInt16BitsToHalf((ushort)((_packed >> 32) & 0xFFFF)),
            (float)BitConverter.UInt16BitsToHalf((ushort)((_packed >> 48) & 0xFFFF)));
    }
}
