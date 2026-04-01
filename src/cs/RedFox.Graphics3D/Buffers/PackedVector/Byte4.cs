using System.Numerics;

namespace RedFox.Graphics3D.Buffers.PackedVector
{
    /// <summary>
    /// Packs a 4-component signed byte vector into a single <see cref="uint"/>.
    /// </summary>
    public struct Byte4 : IPackedVector<Byte4>
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
        public Byte4(uint packedValue) => _packed = packedValue;

        /// <summary>
        /// Initializes a new instance from float components.
        /// </summary>
        /// <param name="x">The X component.</param>
        /// <param name="y">The Y component.</param>
        /// <param name="z">The Z component.</param>
        /// <param name="w">The W component.</param>
        public Byte4(float x, float y, float z, float w) => Pack(new Vector4(x, y, z, w));

        /// <inheritdoc/>
        public void Pack(Vector4 value)
        {
            var x = (sbyte)Math.Clamp(MathF.Round(value.X), -128f, 127f);
            var y = (sbyte)Math.Clamp(MathF.Round(value.Y), -128f, 127f);
            var z = (sbyte)Math.Clamp(MathF.Round(value.Z), -128f, 127f);
            var w = (sbyte)Math.Clamp(MathF.Round(value.W), -128f, 127f);
            _packed = (uint)(byte)x | ((uint)(byte)y << 8) | ((uint)(byte)z << 16) | ((uint)(byte)w << 24);
        }

        /// <inheritdoc/>
        public readonly Vector4 Unpack()
        {
            var x = (sbyte)(_packed & 0xFF);
            var y = (sbyte)((_packed >> 8) & 0xFF);
            var z = (sbyte)((_packed >> 16) & 0xFF);
            var w = (sbyte)((_packed >> 24) & 0xFF);
            return new Vector4(x, y, z, w);
        }
    }
}
