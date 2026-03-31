using System.Numerics;
using System.Runtime.InteropServices;

namespace RedFox.Graphics2D.Codecs
{
    /// <summary>
    /// Codec for <see cref="ImageFormat.R11G11B10Float"/>.
    /// Uses IEEE 754 half-precision float decoding for R and G channels,
    /// and a 10-bit float approximation for the B channel.
    /// </summary>
    public sealed class R11G11B10FloatCodec : IPixelCodec
    {
        /// <inheritdoc/>
        public ImageFormat Format => ImageFormat.R11G11B10Float;

        /// <inheritdoc/>
        public int BytesPerPixel => 4;

        /// <inheritdoc/>
        public void Decode(ReadOnlySpan<byte> source, Span<Vector4> destination, int width, int height)
        {
            var uints = MemoryMarshal.Cast<byte, uint>(source);
            int pixelCount = width * height;

            for (int i = 0; i < pixelCount; i++)
            {
                uint packed = uints[i];
                destination[i] = new Vector4(
                    DecodeFloat11((packed >> 0) & 0x7FF),
                    DecodeFloat11((packed >> 11) & 0x7FF),
                    DecodeFloat10((packed >> 22) & 0x3FF),
                    1f);
            }
        }

        /// <inheritdoc/>
        public void Encode(ReadOnlySpan<Vector4> source, Span<byte> destination, int width, int height)
        {
            var uints = MemoryMarshal.Cast<byte, uint>(destination);
            int pixelCount = width * height;

            for (int i = 0; i < pixelCount; i++)
            {
                var p = source[i];
                uint r = EncodeFloat11(p.X);
                uint g = EncodeFloat11(p.Y);
                uint b = EncodeFloat10(p.Z);
                uints[i] = (r & 0x7FF) | ((g & 0x7FF) << 11) | ((b & 0x3FF) << 22);
            }
        }

        /// <inheritdoc/>
        public Vector4 ReadPixel(ReadOnlySpan<byte> source, int pixelIndex)
        {
            var uints = MemoryMarshal.Cast<byte, uint>(source);
            uint packed = uints[pixelIndex];
            return new Vector4(
                DecodeFloat11((packed >> 0) & 0x7FF),
                DecodeFloat11((packed >> 11) & 0x7FF),
                DecodeFloat10((packed >> 22) & 0x3FF),
                1f);
        }

        /// <inheritdoc/>
        public void WritePixel(Vector4 pixel, Span<byte> destination, int pixelIndex)
        {
            var uints = MemoryMarshal.Cast<byte, uint>(destination);
            uint r = EncodeFloat11(pixel.X);
            uint g = EncodeFloat11(pixel.Y);
            uint b = EncodeFloat10(pixel.Z);
            uints[pixelIndex] = (r & 0x7FF) | ((g & 0x7FF) << 11) | ((b & 0x3FF) << 22);
        }

        /// <inheritdoc/>
        public void ConvertFrom(ReadOnlySpan<byte> source, IPixelCodec sourceCodec, Span<byte> destination, int width, int height)
        {
            if (sourceCodec is R11G11B10FloatCodec)
            {
                int byteCount = width * height * 4;
                source[..byteCount].CopyTo(destination);
                return;
            }

            sourceCodec.DecodeTo(source, this, destination, width, height);
        }

        private static float DecodeFloat11(uint value)
        {
            uint exponent = (value >> 6) & 0x1F;
            uint mantissa = value & 0x3F;

            if (exponent == 0)
            {
                if (mantissa == 0) return 0f;
                return (float)(mantissa * Math.Pow(2, -10) * Math.Pow(2, -14));
            }
            else if (exponent == 31)
            {
                return mantissa == 0 ? float.PositiveInfinity : float.NaN;
            }

            return (float)((1.0 + mantissa / 64.0) * Math.Pow(2, exponent - 15));
        }

        private static float DecodeFloat10(uint value)
        {
            uint exponent = (value >> 5) & 0x1F;
            uint mantissa = value & 0x1F;

            if (exponent == 0)
            {
                if (mantissa == 0) return 0f;
                return (float)(mantissa * Math.Pow(2, -9) * Math.Pow(2, -14));
            }
            else if (exponent == 31)
            {
                return mantissa == 0 ? float.PositiveInfinity : float.NaN;
            }

            return (float)((1.0 + mantissa / 32.0) * Math.Pow(2, exponent - 15));
        }

        private static uint EncodeFloat11(float value)
        {
            if (value <= 0f) return 0;
            if (float.IsPositiveInfinity(value)) return 0x7C0;
            if (float.IsNaN(value)) return 0x7FF;

            int exponent = 0;
            float mantissa = value;
            while (mantissa >= 2f && exponent < 30)
            {
                mantissa *= 0.5f;
                exponent++;
            }
            while (mantissa < 1f && exponent > -15)
            {
                mantissa *= 2f;
                exponent--;
            }

            uint encodedExp = (uint)(exponent + 15);
            uint encodedMant = (uint)((mantissa - 1f) * 64f);
            return (encodedExp << 6) | (encodedMant & 0x3F);
        }

        private static uint EncodeFloat10(float value)
        {
            if (value <= 0f) return 0;
            if (float.IsPositiveInfinity(value)) return 0x7C0;
            if (float.IsNaN(value)) return 0x7FF;

            int exponent = 0;
            float mantissa = value;
            while (mantissa >= 2f && exponent < 30)
            {
                mantissa *= 0.5f;
                exponent++;
            }
            while (mantissa < 1f && exponent > -15)
            {
                mantissa *= 2f;
                exponent--;
            }

            uint encodedExp = (uint)(exponent + 15);
            uint encodedMant = (uint)((mantissa - 1f) * 32f);
            return (encodedExp << 5) | (encodedMant & 0x1F);
        }
    }
}
