using System.Numerics;

namespace RedFox.Graphics3D.Buffers.PackedVector
{
    /// <summary>
    /// Packs a 4-component unsigned 4-bit integer vector into a 16-bit value.
    /// </summary>
    /// <remarks>
    /// Layout: X:4 Y:4 Z:4 W:4.
    /// </remarks>
    public struct UNibble4 : IPackedVector<UNibble4>
    {
        private ushort _packed;

        /// <inheritdoc/>
        public static int ComponentCount => 4;

        /// <summary>
        /// Gets or sets the raw packed value.
        /// </summary>
        public ushort PackedValue { readonly get => _packed; set => _packed = value; }

        /// <summary>
        /// Initializes a new instance from a raw packed value.
        /// </summary>
        /// <param name="packedValue">The raw packed value.</param>
        public UNibble4(ushort packedValue) => _packed = packedValue;

        /// <summary>
        /// Initializes a new instance from float components.
        /// </summary>
        /// <param name="x">The X component.</param>
        /// <param name="y">The Y component.</param>
        /// <param name="z">The Z component.</param>
        /// <param name="w">The W component.</param>
        public UNibble4(float x, float y, float z, float w) => Pack(new Vector4(x, y, z, w));

        /// <inheritdoc/>
        public void Pack(Vector4 value)
        {
            var x = (int)Math.Clamp(MathF.Round(value.X), 0, 15);
            var y = (int)Math.Clamp(MathF.Round(value.Y), 0, 15);
            var z = (int)Math.Clamp(MathF.Round(value.Z), 0, 15);
            var w = (int)Math.Clamp(MathF.Round(value.W), 0, 15);
            _packed = (ushort)((w << 12) | (z << 8) | (y << 4) | x);
        }

        /// <inheritdoc/>
        public readonly Vector4 Unpack()
        {
            var x = _packed & 0xF;
            var y = (_packed >> 4) & 0xF;
            var z = (_packed >> 8) & 0xF;
            var w = (_packed >> 12) & 0xF;
            return new Vector4(x, y, z, w);
        }
    }
}
