using System.Numerics;
using System.Runtime.InteropServices;

namespace RedFox.Graphics3D.Buffers.PackedVector
{
    /// <summary>
    /// Packs a 3-component partial-precision floating-point (11/11/10) vector into a 32-bit value.
    /// </summary>
    /// <remarks>
    /// Layout: X:6-bit mantissa + 5-bit exponent, Y:6-bit mantissa + 5-bit exponent, Z:5-bit mantissa + 5-bit exponent.
    /// </remarks>
    public struct Float3PK : IPackedVector<Float3PK>
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
        public Float3PK(uint packedValue) => _packed = packedValue;

        /// <summary>
        /// Initializes a new instance from float components.
        /// </summary>
        /// <param name="x">The X component.</param>
        /// <param name="y">The Y component.</param>
        /// <param name="z">The Z component.</param>
        public Float3PK(float x, float y, float z) => Pack(new Vector4(x, y, z, 0f));

        /// <inheritdoc/>
        public void Pack(Vector4 value)
        {
            var ix = FloatToFloat11(value.X);
            var iy = FloatToFloat11(value.Y);
            var iz = FloatToFloat10(value.Z);
            _packed = (ix & 0x7FF) | ((iy & 0x7FF) << 11) | ((iz & 0x3FF) << 22);
        }

        /// <inheritdoc/>
        public readonly Vector4 Unpack()
        {
            var ix = (int)(_packed & 0x7FF);
            var iy = (int)((_packed >> 11) & 0x7FF);
            var iz = (int)((_packed >> 22) & 0x3FF);
            return new Vector4(Float11ToFloat(ix), Float11ToFloat(iy), Float10ToFloat(iz), 0f);
        }

        private static uint FloatToFloat11(float value)
        {
            uint i = BitConverter.SingleToUInt32Bits(value);
            uint sign = i & 0x80000000;
            uint abs = i & 0x7FFFFFFF;

            if ((abs & 0x7F800000) == 0x7F800000)
            {
                if ((abs & 0x7FFFFF) != 0) return 0x7FF;
                return sign != 0 ? 0u : 0x7C0u;
            }
            if (sign != 0 || abs < 0x35800000) return 0;
            if (abs > 0x477E0000U) return 0x7BF;

            if (abs < 0x38800000U)
            {
                uint shift = 113U - (abs >> 23);
                abs = (0x800000U | (abs & 0x7FFFFFU)) >> (int)shift;
            }
            else
            {
                abs += 0xC8000000U;
            }

            return ((abs + 0xFFFFU + ((abs >> 17) & 1U)) >> 17) & 0x7FF;
        }

        private static uint FloatToFloat10(float value)
        {
            uint i = BitConverter.SingleToUInt32Bits(value);
            uint sign = i & 0x80000000;
            uint abs = i & 0x7FFFFFFF;

            if ((abs & 0x7F800000) == 0x7F800000)
            {
                if (abs != 0x7F800000) return 0x3FF;
                return sign != 0 ? 0u : 0x3E0u;
            }
            if (sign != 0) return 0;
            if (abs > 0x477C0000U) return 0x3DF;

            if (abs < 0x38800000U)
            {
                uint shift = 113U - (abs >> 23);
                abs = (0x800000U | (abs & 0x7FFFFFU)) >> (int)shift;
            }
            else
            {
                abs += 0xC8000000U;
            }

            return ((abs + 0x1FFFFU + ((abs >> 18) & 1U)) >> 18) & 0x3FF;
        }

        private static float Float11ToFloat(int value)
        {
            if (value == 0) return 0f;

            uint exponent = (uint)(value >> 6);
            uint mantissa = (uint)(value & 0x3F);

            if (exponent == 0x1F)
                return BitConverter.UInt32BitsToSingle(0x7F800000 | (mantissa << 17));

            if (exponent != 0)
            {
            }
            else if (mantissa != 0)
            {
                exponent = 1;
                while ((mantissa & 0x40) == 0) { exponent--; mantissa <<= 1; }
                mantissa &= 0x3F;
            }
            else
            {
                exponent = unchecked((uint)(-112));
            }

            return BitConverter.UInt32BitsToSingle(((exponent + 112) << 23) | (mantissa << 17));
        }

        private static float Float10ToFloat(int value)
        {
            if (value == 0) return 0f;

            uint exponent = (uint)(value >> 5);
            uint mantissa = (uint)(value & 0x1F);

            if (exponent == 0x1F)
                return BitConverter.UInt32BitsToSingle(0x7F800000 | (mantissa << 17));

            if (exponent != 0)
            {
            }
            else if (mantissa != 0)
            {
                exponent = 1;
                while ((mantissa & 0x20) == 0) { exponent--; mantissa <<= 1; }
                mantissa &= 0x1F;
            }
            else
            {
                exponent = unchecked((uint)(-112));
            }

            return BitConverter.UInt32BitsToSingle(((exponent + 112) << 23) | (mantissa << 18));
        }
    }
}
