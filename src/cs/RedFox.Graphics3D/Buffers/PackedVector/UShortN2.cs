using System.Numerics;

namespace RedFox.Graphics3D.Buffers.PackedVector
{
    /// <summary>
    /// Packs a 2-component unsigned normalized 16-bit integer vector into a single <see cref="uint"/>.
    /// </summary>
    public struct UShortN2 : IPackedVector<UShortN2>
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
        public UShortN2(uint packedValue) => _packed = packedValue;

        /// <summary>
        /// Initializes a new instance from float components.
        /// </summary>
        /// <param name="x">The X component.</param>
        /// <param name="y">The Y component.</param>
        public UShortN2(float x, float y) => Pack(new Vector4(x, y, 0f, 0f));

        /// <inheritdoc/>
        public void Pack(Vector4 value)
        {
            var x = (ushort)Math.Clamp(MathF.Truncate(Math.Clamp(value.X, 0f, 1f) * 65535f + 0.5f), 0, 65535);
            var y = (ushort)Math.Clamp(MathF.Truncate(Math.Clamp(value.Y, 0f, 1f) * 65535f + 0.5f), 0, 65535);
            _packed = x | ((uint)y << 16);
        }

        /// <inheritdoc/>
        public readonly Vector4 Unpack()
        {
            var x = (ushort)(_packed & 0xFFFF);
            var y = (ushort)(_packed >> 16);
            return new Vector4(x / 65535f, y / 65535f, 0f, 0f);
        }
    }
}
