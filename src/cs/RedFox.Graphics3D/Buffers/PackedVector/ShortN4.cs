using System.Numerics;

namespace RedFox.Graphics3D.Buffers.PackedVector
{
    /// <summary>
    /// Packs a 4-component signed normalized 16-bit integer vector into a single <see cref="ulong"/>.
    /// </summary>
    public struct ShortN4 : IPackedVector<ShortN4>
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
        public ShortN4(ulong packedValue) => _packed = packedValue;

        /// <summary>
        /// Initializes a new instance from float components.
        /// </summary>
        /// <param name="x">The X component.</param>
        /// <param name="y">The Y component.</param>
        /// <param name="z">The Z component.</param>
        /// <param name="w">The W component.</param>
        public ShortN4(float x, float y, float z, float w) => Pack(new Vector4(x, y, z, w));

        /// <inheritdoc/>
        public void Pack(Vector4 value)
        {
            var x = (short)Math.Clamp(MathF.Round(Math.Clamp(value.X, -1f, 1f) * 32767f), -32768f, 32767f);
            var y = (short)Math.Clamp(MathF.Round(Math.Clamp(value.Y, -1f, 1f) * 32767f), -32768f, 32767f);
            var z = (short)Math.Clamp(MathF.Round(Math.Clamp(value.Z, -1f, 1f) * 32767f), -32768f, 32767f);
            var w = (short)Math.Clamp(MathF.Round(Math.Clamp(value.W, -1f, 1f) * 32767f), -32768f, 32767f);
            _packed = (ulong)(ushort)x | ((ulong)(ushort)y << 16) | ((ulong)(ushort)z << 32) | ((ulong)(ushort)w << 48);
        }

        /// <inheritdoc/>
        public readonly Vector4 Unpack()
        {
            var x = (short)(_packed & 0xFFFF);
            var y = (short)((_packed >> 16) & 0xFFFF);
            var z = (short)((_packed >> 32) & 0xFFFF);
            var w = (short)((_packed >> 48) & 0xFFFF);
            return new Vector4(
                x == -32768 ? -1f : x / 32767f,
                y == -32768 ? -1f : y / 32767f,
                z == -32768 ? -1f : z / 32767f,
                w == -32768 ? -1f : w / 32767f);
        }
    }
}
