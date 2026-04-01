using System.Numerics;

namespace RedFox.Graphics3D.Buffers.PackedVector
{
    /// <summary>
    /// Packs a 2-component unsigned 16-bit integer vector into a single <see cref="uint"/>.
    /// </summary>
    public struct UShort2 : IPackedVector<UShort2>
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
        public UShort2(uint packedValue) => _packed = packedValue;

        /// <summary>
        /// Initializes a new instance from float components.
        /// </summary>
        /// <param name="x">The X component.</param>
        /// <param name="y">The Y component.</param>
        public UShort2(float x, float y) => Pack(new Vector4(x, y, 0f, 0f));

        /// <inheritdoc/>
        public void Pack(Vector4 value)
        {
            var x = (ushort)Math.Clamp(MathF.Round(value.X), 0, 65535);
            var y = (ushort)Math.Clamp(MathF.Round(value.Y), 0, 65535);
            _packed = x | ((uint)y << 16);
        }

        /// <inheritdoc/>
        public readonly Vector4 Unpack()
        {
            var x = (ushort)(_packed & 0xFFFF);
            var y = (ushort)(_packed >> 16);
            return new Vector4(x, y, 0f, 0f);
        }
    }
}
