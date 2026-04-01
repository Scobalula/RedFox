using System.Numerics;

namespace RedFox.Graphics3D.Buffers.PackedVector
{
    /// <summary>
    /// Packs a 2-component signed normalized 16-bit integer vector into a single <see cref="uint"/>.
    /// </summary>
    public struct ShortN2 : IPackedVector<ShortN2>
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
        public ShortN2(uint packedValue) => _packed = packedValue;

        /// <summary>
        /// Initializes a new instance from float components.
        /// </summary>
        /// <param name="x">The X component.</param>
        /// <param name="y">The Y component.</param>
        public ShortN2(float x, float y) => Pack(new Vector4(x, y, 0f, 0f));

        /// <inheritdoc/>
        public void Pack(Vector4 value)
        {
            var x = (short)Math.Clamp(MathF.Round(Math.Clamp(value.X, -1f, 1f) * 32767f), -32768f, 32767f);
            var y = (short)Math.Clamp(MathF.Round(Math.Clamp(value.Y, -1f, 1f) * 32767f), -32768f, 32767f);
            _packed = (uint)((ushort)x | ((uint)(ushort)y << 16));
        }

        /// <inheritdoc/>
        public readonly Vector4 Unpack()
        {
            var x = (short)(_packed & 0xFFFF);
            var y = (short)(_packed >> 16);
            return new Vector4(
                x == -32768 ? -1f : x / 32767f,
                y == -32768 ? -1f : y / 32767f,
                0f, 0f);
        }
    }
}
