using System.Numerics;

namespace RedFox.Graphics3D.Buffers.PackedVector
{
    /// <summary>
    /// Packs a 2-component signed normalized byte vector into a single <see cref="ushort"/>.
    /// </summary>
    public struct ByteN2 : IPackedVector<ByteN2>
    {
        private ushort _packed;

        /// <inheritdoc/>
        public static int ComponentCount => 2;

        /// <summary>
        /// Gets or sets the raw packed value.
        /// </summary>
        public ushort PackedValue { readonly get => _packed; set => _packed = value; }

        /// <summary>
        /// Initializes a new instance from a raw packed value.
        /// </summary>
        /// <param name="packedValue">The raw packed value.</param>
        public ByteN2(ushort packedValue) => _packed = packedValue;

        /// <summary>
        /// Initializes a new instance from float components.
        /// </summary>
        /// <param name="x">The X component.</param>
        /// <param name="y">The Y component.</param>
        public ByteN2(float x, float y) => Pack(new Vector4(x, y, 0f, 0f));

        /// <inheritdoc/>
        public void Pack(Vector4 value)
        {
            var x = (sbyte)Math.Clamp(MathF.Truncate(Math.Clamp(value.X, -1f, 1f) * 127f), -128f, 127f);
            var y = (sbyte)Math.Clamp(MathF.Truncate(Math.Clamp(value.Y, -1f, 1f) * 127f), -128f, 127f);
            _packed = (ushort)((byte)x | ((uint)(byte)y << 8));
        }

        /// <inheritdoc/>
        public readonly Vector4 Unpack()
        {
            var x = (sbyte)(_packed & 0xFF);
            var y = (sbyte)(_packed >> 8);
            return new Vector4(
                x == -128 ? -1f : x / 127f,
                y == -128 ? -1f : y / 127f,
                0f, 0f);
        }
    }
}
