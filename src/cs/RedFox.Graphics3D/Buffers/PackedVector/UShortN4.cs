using System.Numerics;

namespace RedFox.Graphics3D.Buffers.PackedVector
{
    /// <summary>
    /// Packs a 4-component unsigned normalized 16-bit integer vector into a single <see cref="ulong"/>.
    /// </summary>
    public struct UShortN4 : IPackedVector<UShortN4>
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
        public UShortN4(ulong packedValue) => _packed = packedValue;

        /// <summary>
        /// Initializes a new instance from float components.
        /// </summary>
        /// <param name="x">The X component.</param>
        /// <param name="y">The Y component.</param>
        /// <param name="z">The Z component.</param>
        /// <param name="w">The W component.</param>
        public UShortN4(float x, float y, float z, float w) => Pack(new Vector4(x, y, z, w));

        /// <inheritdoc/>
        public void Pack(Vector4 value)
        {
            var x = (ushort)Math.Clamp(MathF.Truncate(Math.Clamp(value.X, 0f, 1f) * 65535f + 0.5f), 0, 65535);
            var y = (ushort)Math.Clamp(MathF.Truncate(Math.Clamp(value.Y, 0f, 1f) * 65535f + 0.5f), 0, 65535);
            var z = (ushort)Math.Clamp(MathF.Truncate(Math.Clamp(value.Z, 0f, 1f) * 65535f + 0.5f), 0, 65535);
            var w = (ushort)Math.Clamp(MathF.Truncate(Math.Clamp(value.W, 0f, 1f) * 65535f + 0.5f), 0, 65535);
            _packed = (ulong)x | ((ulong)y << 16) | ((ulong)z << 32) | ((ulong)w << 48);
        }

        /// <inheritdoc/>
        public readonly Vector4 Unpack()
        {
            var x = (ushort)(_packed & 0xFFFF);
            var y = (ushort)((_packed >> 16) & 0xFFFF);
            var z = (ushort)((_packed >> 32) & 0xFFFF);
            var w = (ushort)((_packed >> 48) & 0xFFFF);
            return new Vector4(x / 65535f, y / 65535f, z / 65535f, w / 65535f);
        }
    }
}
