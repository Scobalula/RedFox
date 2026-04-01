using System.Numerics;

namespace RedFox.Graphics3D.Buffers.PackedVector
{
    /// <summary>
    /// Packs a 4-component 10-10-10-2 signed/unsigned normalized vector into a 32-bit value.
    /// </summary>
    /// <remarks>
    /// Layout: X:10 (signed normalized) Y:10 (signed normalized) Z:10 (signed normalized) W:2 (unsigned normalized).
    /// </remarks>
    public struct XDecN4 : IPackedVector<XDecN4>
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
        public XDecN4(uint packedValue) => _packed = packedValue;

        /// <summary>
        /// Initializes a new instance from float components.
        /// </summary>
        /// <param name="x">The X component.</param>
        /// <param name="y">The Y component.</param>
        /// <param name="z">The Z component.</param>
        /// <param name="w">The W component.</param>
        public XDecN4(float x, float y, float z, float w) => Pack(new Vector4(x, y, z, w));

        /// <inheritdoc/>
        public void Pack(Vector4 value)
        {
            var x = (int)Math.Clamp(MathF.Round(Math.Clamp(value.X, -1f, 1f) * 511f), -511f, 511f);
            var y = (int)Math.Clamp(MathF.Round(Math.Clamp(value.Y, -1f, 1f) * 511f), -511f, 511f);
            var z = (int)Math.Clamp(MathF.Round(Math.Clamp(value.Z, -1f, 1f) * 511f), -511f, 511f);
            var w = (int)Math.Clamp(MathF.Round(Math.Clamp(value.W, 0f, 1f) * 3f), 0f, 3f);
            _packed = (uint)((x & 0x3FF) | ((y & 0x3FF) << 10) | ((z & 0x3FF) << 20) | (w << 30));
        }

        /// <inheritdoc/>
        public readonly Vector4 Unpack()
        {
            uint rawX = _packed & 0x3FF;
            uint rawY = (_packed >> 10) & 0x3FF;
            uint rawZ = (_packed >> 20) & 0x3FF;
            var rawW = (float)(_packed >> 30);

            float x = rawX == 0x200 ? -1f : SignExtend10(rawX) / 511f;
            float y = rawY == 0x200 ? -1f : SignExtend10(rawY) / 511f;
            float z = rawZ == 0x200 ? -1f : SignExtend10(rawZ) / 511f;

            return new Vector4(x, y, z, rawW / 3f);
        }

        private static float SignExtend10(uint value)
        {
            return (value & 0x200) != 0 ? (int)(value | 0xFFFFFC00) : (int)value;
        }
    }
}
