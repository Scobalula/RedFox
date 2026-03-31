using System.Numerics;
using System.Runtime.CompilerServices;

namespace RedFox.Graphics2D.BC
{
    /// <summary>
    /// Provides color packing, unpacking, interpolation, and palette operations
    /// used by the BC1–BC5 block-compressed codec families.
    /// </summary>
    internal static class BlockColorOperations
    {
        /// <summary>
        /// Decodes a 5:6:5 RGB565 packed color into a <see cref="Vector4"/> with alpha set to 1.
        /// </summary>
        /// <param name="packed">The 16-bit packed RGB565 value.</param>
        /// <returns>An RGBA <see cref="Vector4"/> with components in [0, 1].</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static Vector4 DecodeRgb565(ushort packed)
        {
            float r = ((packed >> 11) & 0x1F) / 31.0f;
            float g = ((packed >> 5) & 0x3F) / 63.0f;
            float b = (packed & 0x1F) / 31.0f;
            return new Vector4(r, g, b, 1.0f);
        }

        /// <summary>
        /// Encodes a <see cref="Vector4"/> color into a 5:6:5 RGB565 packed value.
        /// Input components are clamped to [0, 1].
        /// </summary>
        /// <param name="color">The source RGBA color.</param>
        /// <returns>The 16-bit packed RGB565 value.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static ushort EncodeRgb565(Vector4 color)
        {
            int r = (int)(Math.Clamp(color.X, 0f, 1f) * 31f + 0.5f);
            int g = (int)(Math.Clamp(color.Y, 0f, 1f) * 63f + 0.5f);
            int b = (int)(Math.Clamp(color.Z, 0f, 1f) * 31f + 0.5f);
            return (ushort)((r << 11) | (g << 5) | b);
        }

        /// <summary>
        /// Linearly interpolates between two <see cref="Vector4"/> values.
        /// </summary>
        /// <param name="a">The start value.</param>
        /// <param name="b">The end value.</param>
        /// <param name="t">The interpolation factor in [0, 1].</param>
        /// <returns>The interpolated result.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static Vector4 Lerp(Vector4 a, Vector4 b, float t) =>
            a + (b - a) * t;

        /// <summary>
        /// Decodes a BC4/BC5-style interpolated alpha block (8 bytes) into 16 scalar values.
        /// Supports both unsigned ([0, 1]) and signed ([-1, 1]) modes.
        /// </summary>
        /// <param name="block">The 8-byte alpha block data.</param>
        /// <param name="output">The destination span receiving 16 decoded values.</param>
        /// <param name="signed">Whether to interpret endpoint bytes as signed.</param>
        public static void DecodeAlphaBlock(ReadOnlySpan<byte> block, Span<float> output, bool signed)
        {
            float alpha0, alpha1;

            if (signed)
            {
                alpha0 = (sbyte)block[0] / 127.0f;
                alpha1 = (sbyte)block[1] / 127.0f;
            }
            else
            {
                alpha0 = block[0] / 255.0f;
                alpha1 = block[1] / 255.0f;
            }

            Span<float> palette = stackalloc float[8];
            palette[0] = alpha0;
            palette[1] = alpha1;

            if (block[0] > block[1])
            {
                palette[2] = (6 * alpha0 + 1 * alpha1) / 7.0f;
                palette[3] = (5 * alpha0 + 2 * alpha1) / 7.0f;
                palette[4] = (4 * alpha0 + 3 * alpha1) / 7.0f;
                palette[5] = (3 * alpha0 + 4 * alpha1) / 7.0f;
                palette[6] = (2 * alpha0 + 5 * alpha1) / 7.0f;
                palette[7] = (1 * alpha0 + 6 * alpha1) / 7.0f;
            }
            else
            {
                palette[2] = (4 * alpha0 + 1 * alpha1) / 5.0f;
                palette[3] = (3 * alpha0 + 2 * alpha1) / 5.0f;
                palette[4] = (2 * alpha0 + 3 * alpha1) / 5.0f;
                palette[5] = (1 * alpha0 + 4 * alpha1) / 5.0f;
                palette[6] = signed ? -1.0f : 0.0f;
                palette[7] = 1.0f;
            }

            ulong indices = 0;
            for (int i = 0; i < 6; i++)
            {
                indices |= (ulong)block[2 + i] << (8 * i);
            }

            for (int i = 0; i < 16; i++)
            {
                int idx = (int)((indices >> (3 * i)) & 0x7);
                output[i] = palette[idx];
            }
        }

        /// <summary>
        /// Encodes 16 scalar values into a BC4/BC5-style interpolated alpha block (8 bytes).
        /// Supports both unsigned ([0, 1]) and signed ([-1, 1]) modes.
        /// </summary>
        /// <param name="values">The 16 source values to encode.</param>
        /// <param name="block">The destination 8-byte block.</param>
        /// <param name="signed">Whether to use signed encoding.</param>
        public static void EncodeAlphaBlock(ReadOnlySpan<float> values, Span<byte> block, bool signed)
        {
            float minVal = float.MaxValue;
            float maxVal = float.MinValue;

            for (int i = 0; i < 16; i++)
            {
                float v = signed ? Math.Clamp(values[i], -1f, 1f) : Math.Clamp(values[i], 0f, 1f);
                minVal = Math.Min(minVal, v);
                maxVal = Math.Max(maxVal, v);
            }

            if (signed)
            {
                block[0] = (byte)(sbyte)(maxVal * 127f);
                block[1] = (byte)(sbyte)(minVal * 127f);
            }
            else
            {
                block[0] = (byte)(maxVal * 255f + 0.5f);
                block[1] = (byte)(minVal * 255f + 0.5f);
            }

            float a0 = signed ? (sbyte)block[0] / 127.0f : block[0] / 255.0f;
            float a1 = signed ? (sbyte)block[1] / 127.0f : block[1] / 255.0f;

            Span<float> palette = stackalloc float[8];
            palette[0] = a0;
            palette[1] = a1;

            if (block[0] > block[1])
            {
                for (int i = 1; i <= 6; i++)
                    palette[1 + i] = ((7 - i) * a0 + i * a1) / 7.0f;
            }
            else
            {
                for (int i = 1; i <= 4; i++)
                    palette[1 + i] = ((5 - i) * a0 + i * a1) / 5.0f;
                palette[6] = signed ? -1.0f : 0.0f;
                palette[7] = 1.0f;
            }

            ulong indices = 0;
            for (int i = 0; i < 16; i++)
            {
                float v = signed ? Math.Clamp(values[i], -1f, 1f) : Math.Clamp(values[i], 0f, 1f);
                int bestIdx = 0;
                float bestDist = float.MaxValue;
                for (int j = 0; j < 8; j++)
                {
                    float dist = MathF.Abs(v - palette[j]);
                    if (dist < bestDist)
                    {
                        bestDist = dist;
                        bestIdx = j;
                    }
                }
                indices |= (ulong)bestIdx << (3 * i);
            }

            for (int i = 0; i < 6; i++)
                block[2 + i] = (byte)((indices >> (8 * i)) & 0xFF);
        }

        /// <summary>
        /// Finds the minimum and maximum RGB color endpoints from 16 pixels using bounding-box selection.
        /// </summary>
        /// <param name="pixels">The 16 source pixels.</param>
        /// <param name="minColor">The resulting minimum color endpoint.</param>
        /// <param name="maxColor">The resulting maximum color endpoint.</param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void FindMinMaxColorEndpoints(ReadOnlySpan<Vector4> pixels, out Vector4 minColor, out Vector4 maxColor)
        {
            minColor = new Vector4(float.MaxValue, float.MaxValue, float.MaxValue, 1);
            maxColor = new Vector4(float.MinValue, float.MinValue, float.MinValue, 1);

            for (int i = 0; i < 16; i++)
            {
                minColor = Vector4.Min(minColor, pixels[i]);
                maxColor = Vector4.Max(maxColor, pixels[i]);
            }
        }

        /// <summary>
        /// Computes the squared distance between two colors in RGB space (ignoring alpha).
        /// </summary>
        /// <param name="a">The first color.</param>
        /// <param name="b">The second color.</param>
        /// <returns>The squared RGB distance.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static float ColorDistanceSquared(Vector4 a, Vector4 b)
        {
            var diff = a - b;
            return diff.X * diff.X + diff.Y * diff.Y + diff.Z * diff.Z;
        }

        /// <summary>
        /// Selects the closest palette index for a pixel from a 4-color palette using RGB distance.
        /// </summary>
        /// <param name="pixel">The pixel to match.</param>
        /// <param name="palette">The 4-entry color palette.</param>
        /// <returns>The index (0–3) of the closest palette color.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static int FindClosestColorIndex(Vector4 pixel, ReadOnlySpan<Vector4> palette)
        {
            int bestIdx = 0;
            float bestDist = float.MaxValue;

            for (int j = 0; j < palette.Length; j++)
            {
                float dist = ColorDistanceSquared(pixel, palette[j]);
                if (dist < bestDist)
                {
                    bestDist = dist;
                    bestIdx = j;
                }
            }

            return bestIdx;
        }

        /// <summary>
        /// Encodes RGB565 endpoint pair ensuring c0 &gt; c1 for 4-color mode.
        /// Returns the ordered endpoints and the decoded palette colors.
        /// </summary>
        /// <param name="minColor">The minimum bounding color.</param>
        /// <param name="maxColor">The maximum bounding color.</param>
        /// <param name="c0Raw">The resulting packed endpoint 0 (higher value).</param>
        /// <param name="c1Raw">The resulting packed endpoint 1 (lower value).</param>
        /// <param name="palette">A 4-entry span to receive the interpolated palette.</param>
        public static void BuildFourColorPalette(
            Vector4 minColor,
            Vector4 maxColor,
            out ushort c0Raw,
            out ushort c1Raw,
            Span<Vector4> palette)
        {
            c0Raw = EncodeRgb565(maxColor);
            c1Raw = EncodeRgb565(minColor);

            if (c0Raw < c1Raw)
                (c0Raw, c1Raw) = (c1Raw, c0Raw);

            if (c0Raw == c1Raw && c0Raw < 0xFFFF)
                c0Raw++;

            var c0 = DecodeRgb565(c0Raw);
            var c1 = DecodeRgb565(c1Raw);

            palette[0] = c0;
            palette[1] = c1;
            palette[2] = Lerp(c0, c1, 1.0f / 3.0f);
            palette[3] = Lerp(c0, c1, 2.0f / 3.0f);
        }
    }
}
