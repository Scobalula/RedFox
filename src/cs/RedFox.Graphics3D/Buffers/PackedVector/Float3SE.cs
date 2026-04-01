using System.Numerics;
using System.Runtime.InteropServices;

namespace RedFox.Graphics3D.Buffers.PackedVector
{
    /// <summary>
    /// Packs a 3-component shared-exponent floating-point (9/9/9) vector into a 32-bit value.
    /// </summary>
    /// <remarks>
    /// Layout: X:9 Y:9 Z:9 E:5.
    /// </remarks>
    public struct Float3SE : IPackedVector<Float3SE>
    {
        private uint _packed;

        /// <inheritdoc/>
        public static int ComponentCount => 3;

        /// <summary>
        /// Gets or sets the raw packed value.
        /// </summary>
        public uint PackedValue { readonly get => _packed; set => _packed = value; }

        /// <summary>
        /// Initializes a new instance from a raw packed value.
        /// </summary>
        /// <param name="packedValue">The raw packed value.</param>
        public Float3SE(uint packedValue) => _packed = packedValue;

        /// <summary>
        /// Initializes a new instance from float components.
        /// </summary>
        /// <param name="x">The X component.</param>
        /// <param name="y">The Y component.</param>
        /// <param name="z">The Z component.</param>
        public Float3SE(float x, float y, float z) => Pack(new Vector4(x, y, z, 0f));

        /// <inheritdoc/>
        public void Pack(Vector4 value)
        {
            const float maxf9 = 0x1FF << 7;
            const float minf9 = 1f / (1 << 16);

            float x = Math.Clamp(value.X, 0f, maxf9);
            float y = Math.Clamp(value.Y, 0f, maxf9);
            float z = Math.Clamp(value.Z, 0f, maxf9);

            float maxColor = Math.Max(Math.Max(x, y), z);
            maxColor = Math.Max(maxColor, minf9);

            uint fi = BitConverter.SingleToUInt32Bits(maxColor);
            fi += 0x00004000;
            uint exp = fi >> 23;
            uint e = exp - 0x6F;

            uint invScaleBits = (uint)(0x83000000 - (exp << 23));
            float scaleR = BitConverter.UInt32BitsToSingle(invScaleBits);

            uint xm = (uint)Math.Clamp(MathF.Round(x * scaleR), 0, 0x1FF);
            uint ym = (uint)Math.Clamp(MathF.Round(y * scaleR), 0, 0x1FF);
            uint zm = (uint)Math.Clamp(MathF.Round(z * scaleR), 0, 0x1FF);

            _packed = (xm & 0x1FF) | ((ym & 0x1FF) << 9) | ((zm & 0x1FF) << 18) | ((e & 0x1F) << 27);
        }

        /// <inheritdoc/>
        public readonly Vector4 Unpack()
        {
            uint xm = _packed & 0x1FF;
            uint ym = (_packed >> 9) & 0x1FF;
            uint zm = (_packed >> 18) & 0x1FF;
            uint e = (_packed >> 27) & 0x1F;

            uint scaleBits = 0x33800000 + (e << 23);
            float scale = BitConverter.UInt32BitsToSingle(scaleBits);

            return new Vector4(scale * xm, scale * ym, scale * zm, 0f);
        }
    }
}
