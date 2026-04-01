using System.Numerics;

namespace RedFox.Graphics3D.Buffers.PackedVector
{
    /// <summary>
    /// Packs a 2-component signed byte vector into a single <see cref="ushort"/>.
    /// </summary>
    public struct Byte2 : IPackedVector<Byte2>
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
        public Byte2(ushort packedValue) => _packed = packedValue;

        /// <summary>
        /// Initializes a new instance from float components.
        /// </summary>
        /// <param name="x">The X component.</param>
        /// <param name="y">The Y component.</param>
        public Byte2(float x, float y) => Pack(new Vector4(x, y, 0f, 0f));

        /// <inheritdoc/>
        public void Pack(Vector4 value)
        {
            var x = (sbyte)Math.Clamp(MathF.Round(value.X), -128f, 127f);
            var y = (sbyte)Math.Clamp(MathF.Round(value.Y), -128f, 127f);
            _packed = (ushort)((byte)x | ((uint)(byte)y << 8));
        }

        /// <inheritdoc/>
        public readonly Vector4 Unpack()
        {
            var x = (sbyte)(_packed & 0xFF);
            var y = (sbyte)(_packed >> 8);
            return new Vector4(x, y, 0f, 0f);
        }
    }
}
