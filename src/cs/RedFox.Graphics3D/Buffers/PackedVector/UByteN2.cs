using System.Numerics;

namespace RedFox.Graphics3D.Buffers.PackedVector
{
    /// <summary>
    /// Packs a 2-component unsigned normalized byte vector into a single <see cref="ushort"/>.
    /// </summary>
    public struct UByteN2 : IPackedVector<UByteN2>
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
        public UByteN2(ushort packedValue) => _packed = packedValue;

        /// <summary>
        /// Initializes a new instance from float components.
        /// </summary>
        /// <param name="x">The X component.</param>
        /// <param name="y">The Y component.</param>
        public UByteN2(float x, float y) => Pack(new Vector4(x, y, 0f, 0f));

        /// <inheritdoc/>
        public void Pack(Vector4 value)
        {
            var x = (byte)Math.Clamp(MathF.Truncate(Math.Clamp(value.X, 0f, 1f) * 255f + 0.5f), 0, 255);
            var y = (byte)Math.Clamp(MathF.Truncate(Math.Clamp(value.Y, 0f, 1f) * 255f + 0.5f), 0, 255);
            _packed = (ushort)(x | ((uint)y << 8));
        }

        /// <inheritdoc/>
        public readonly Vector4 Unpack()
        {
            var x = (byte)(_packed & 0xFF);
            var y = (byte)(_packed >> 8);
            return new Vector4(x / 255f, y / 255f, 0f, 0f);
        }
    }
}
