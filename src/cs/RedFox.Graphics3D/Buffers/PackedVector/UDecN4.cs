using System.Numerics;

namespace RedFox.Graphics3D.Buffers.PackedVector
{
    /// <summary>
    /// Packs a 4-component 10-10-10-2 unsigned normalized vector into a 32-bit value.
    /// </summary>
    /// <remarks>
    /// Layout: X:10 Y:10 Z:10 W:2.
    /// </remarks>
    public struct UDecN4 : IPackedVector<UDecN4>
    {
        private uint _packed;

        /// <inheritdoc/>
        public static int ComponentCount => 4;

        /// <summary>
        /// Gets or sets the raw packed value.
        /// </summary>
        public uint PackedValue { readonly get => _packed; set => _packed = value; }

        /// <summary>
        /// Initializes a new instance from a raw packed value.
        /// </summary>
        /// <param name="packedValue">The raw packed value.</param>
        public UDecN4(uint packedValue) => _packed = packedValue;

        /// <summary>
        /// Initializes a new instance from float components.
        /// </summary>
        /// <param name="x">The X component.</param>
        /// <param name="y">The Y component.</param>
        /// <param name="z">The Z component.</param>
        /// <param name="w">The W component.</param>
        public UDecN4(float x, float y, float z, float w) => Pack(new Vector4(x, y, z, w));

        /// <inheritdoc/>
        public void Pack(Vector4 value)
        {
            var x = (int)Math.Clamp(MathF.Round(Math.Clamp(value.X, 0f, 1f) * 1023f), 0, 1023);
            var y = (int)Math.Clamp(MathF.Round(Math.Clamp(value.Y, 0f, 1f) * 1023f), 0, 1023);
            var z = (int)Math.Clamp(MathF.Round(Math.Clamp(value.Z, 0f, 1f) * 1023f), 0, 1023);
            var w = (int)Math.Clamp(MathF.Round(Math.Clamp(value.W, 0f, 1f) * 3f), 0, 3);
            _packed = (uint)((x & 0x3FF) | ((y & 0x3FF) << 10) | ((z & 0x3FF) << 20) | (w << 30));
        }

        /// <inheritdoc/>
        public readonly Vector4 Unpack()
        {
            var x = (float)(_packed & 0x3FF);
            var y = (float)((_packed >> 10) & 0x3FF);
            var z = (float)((_packed >> 20) & 0x3FF);
            var w = (float)(_packed >> 30);
            return new Vector4(x / 1023f, y / 1023f, z / 1023f, w / 3f);
        }
    }
}
