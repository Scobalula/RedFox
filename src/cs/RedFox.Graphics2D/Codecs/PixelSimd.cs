using System.Numerics;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Runtime.Intrinsics;
using System.Runtime.Intrinsics.X86;

namespace RedFox.Graphics2D.Codecs
{
    /// <summary>
    /// SIMD-accelerated helpers for pixel format conversions.
    /// Provides AVX2 → SSE2 → scalar tiered fallback paths.
    /// </summary>
    internal static class PixelSimd
    {
        /// <summary>
        /// Swizzles byte channels 0↔2 within each 4-byte pixel (BGRA↔RGBA).
        /// The operation is symmetric: applying it twice is identity.
        /// </summary>
        internal static void SwizzleRedBlue(ReadOnlySpan<byte> source, Span<byte> destination, int pixelCount)
        {
            int byteCount = pixelCount * 4;
            int i = 0;

            if (Avx2.IsSupported)
            {
                var mask = Vector256.Create(
                    (byte)2, 1, 0, 3, 6, 5, 4, 7, 10, 9, 8, 11, 14, 13, 12, 15,
                    2, 1, 0, 3, 6, 5, 4, 7, 10, 9, 8, 11, 14, 13, 12, 15);

                ref byte srcRef = ref MemoryMarshal.GetReference(source);
                ref byte dstRef = ref MemoryMarshal.GetReference(destination);

                for (; i + 32 <= byteCount; i += 32)
                {
                    var v = Vector256.LoadUnsafe(ref srcRef, (nuint)i);
                    Avx2.Shuffle(v, mask).StoreUnsafe(ref dstRef, (nuint)i);
                }
            }

            if (Ssse3.IsSupported)
            {
                var mask = Vector128.Create(
                    (byte)2, 1, 0, 3, 6, 5, 4, 7, 10, 9, 8, 11, 14, 13, 12, 15);

                ref byte srcRef = ref MemoryMarshal.GetReference(source);
                ref byte dstRef = ref MemoryMarshal.GetReference(destination);

                for (; i + 16 <= byteCount; i += 16)
                {
                    var v = Vector128.LoadUnsafe(ref srcRef, (nuint)i);
                    Ssse3.Shuffle(v, mask).StoreUnsafe(ref dstRef, (nuint)i);
                }
            }
            else if (Sse2.IsSupported)
            {
                ref byte srcRef = ref MemoryMarshal.GetReference(source);
                ref byte dstRef = ref MemoryMarshal.GetReference(destination);

                var maskR = Vector128.Create(0x00FF0000);
                var maskG = Vector128.Create(0x0000FF00);
                var maskB = Vector128.Create(0x000000FF);
                var maskA = Vector128.Create(unchecked((int)0xFF000000));

                for (; i + 16 <= byteCount; i += 16)
                {
                    var v = Vector128.LoadUnsafe(ref Unsafe.As<byte, int>(ref Unsafe.Add(ref srcRef, i)));
                    var r = Sse2.And(v, maskR);
                    var g = Sse2.And(v, maskG);
                    var b = Sse2.And(v, maskB);
                    var a = Sse2.And(v, maskA);

                    var result = Sse2.Or(
                        Sse2.Or(Sse2.ShiftRightLogical(r.AsUInt32(), 16).AsInt32(), g),
                        Sse2.Or(Sse2.ShiftLeftLogical(b.AsUInt32(), 16).AsInt32(), a));

                    result.StoreUnsafe(ref Unsafe.As<byte, int>(ref Unsafe.Add(ref dstRef, i)));
                }
            }

            // Scalar tail
            for (; i < byteCount; i += 4)
            {
                destination[i + 0] = source[i + 2];
                destination[i + 1] = source[i + 1];
                destination[i + 2] = source[i + 0];
                destination[i + 3] = source[i + 3];
            }
        }

        /// <summary>
        /// Decodes contiguous R8G8B8A8 bytes into <see cref="Vector4"/> (normalized 0–1).
        /// </summary>
        internal static void DecodeRgba8(ReadOnlySpan<byte> source, Span<Vector4> destination, int pixelCount)
        {
            int i = 0;

            if (Sse2.IsSupported)
            {
                var inv255 = Vector128.Create(1.0f / 255.0f);
                ref byte srcRef = ref MemoryMarshal.GetReference(source);
                ref Vector4 dstRef = ref MemoryMarshal.GetReference(destination);

                for (; i + 4 <= pixelCount; i += 4)
                {
                    var bytes16 = Vector128.LoadUnsafe(ref srcRef, (nuint)(i * 4));
                    Unpack4PixelsToVector4(bytes16, inv255, ref dstRef, i);
                }
            }

            for (; i < pixelCount; i++)
            {
                int o = i * 4;
                destination[i] = new Vector4(
                    source[o] * (1.0f / 255.0f),
                    source[o + 1] * (1.0f / 255.0f),
                    source[o + 2] * (1.0f / 255.0f),
                    source[o + 3] * (1.0f / 255.0f));
            }
        }

        /// <summary>
        /// Decodes contiguous B8G8R8A8 bytes into <see cref="Vector4"/> (RGBA order, normalized 0–1).
        /// </summary>
        internal static void DecodeBgra8(ReadOnlySpan<byte> source, Span<Vector4> destination, int pixelCount)
        {
            int i = 0;

            if (Sse2.IsSupported)
            {
                var inv255 = Vector128.Create(1.0f / 255.0f);
                ref byte srcRef = ref MemoryMarshal.GetReference(source);
                ref Vector4 dstRef = ref MemoryMarshal.GetReference(destination);

                for (; i + 4 <= pixelCount; i += 4)
                {
                    var bytes16 = Vector128.LoadUnsafe(ref srcRef, (nuint)(i * 4));
                    Unpack4PixelsToVector4(bytes16, inv255, ref dstRef, i);

                    // Swizzle float lanes BGRA → RGBA (swap X↔Z) using shufps
                    const byte kSwapRB = 0xC6; // _MM_SHUFFLE(3,0,1,2)
                    ref float fBase = ref Unsafe.As<Vector4, float>(ref Unsafe.Add(ref dstRef, i));
                    var f0 = Vector128.LoadUnsafe(ref fBase);
                    var f1 = Vector128.LoadUnsafe(ref fBase, 4);
                    var f2 = Vector128.LoadUnsafe(ref fBase, 8);
                    var f3 = Vector128.LoadUnsafe(ref fBase, 12);
                    Sse.Shuffle(f0, f0, kSwapRB).StoreUnsafe(ref fBase);
                    Sse.Shuffle(f1, f1, kSwapRB).StoreUnsafe(ref fBase, 4);
                    Sse.Shuffle(f2, f2, kSwapRB).StoreUnsafe(ref fBase, 8);
                    Sse.Shuffle(f3, f3, kSwapRB).StoreUnsafe(ref fBase, 12);
                }
            }

            for (; i < pixelCount; i++)
            {
                int o = i * 4;
                destination[i] = new Vector4(
                    source[o + 2] * (1.0f / 255.0f),
                    source[o + 1] * (1.0f / 255.0f),
                    source[o] * (1.0f / 255.0f),
                    source[o + 3] * (1.0f / 255.0f));
            }
        }

        /// <summary>
        /// Encodes <see cref="Vector4"/> pixels (RGBA, 0–1) to R8G8B8A8 bytes.
        /// </summary>
        internal static void EncodeToRgba8(ReadOnlySpan<Vector4> source, Span<byte> destination, int pixelCount)
        {
            int i = 0;

            if (Sse2.IsSupported)
            {
                ref Vector4 srcRef = ref MemoryMarshal.GetReference(source);
                ref byte dstRef = ref MemoryMarshal.GetReference(destination);

                for (; i + 4 <= pixelCount; i += 4)
                {
                    var bytes = Pack4PixelsFromVector4(ref srcRef, i);
                    bytes.StoreUnsafe(ref dstRef, (nuint)(i * 4));
                }
            }

            for (; i < pixelCount; i++)
            {
                int o = i * 4;
                var p = source[i];
                destination[o + 0] = (byte)(Math.Clamp(p.X, 0f, 1f) * 255f + 0.5f);
                destination[o + 1] = (byte)(Math.Clamp(p.Y, 0f, 1f) * 255f + 0.5f);
                destination[o + 2] = (byte)(Math.Clamp(p.Z, 0f, 1f) * 255f + 0.5f);
                destination[o + 3] = (byte)(Math.Clamp(p.W, 0f, 1f) * 255f + 0.5f);
            }
        }

        /// <summary>
        /// Encodes <see cref="Vector4"/> pixels (RGBA, 0–1) to B8G8R8A8 bytes.
        /// </summary>
        internal static void EncodeToBgra8(ReadOnlySpan<Vector4> source, Span<byte> destination, int pixelCount)
        {
            int i = 0;

            if (Sse2.IsSupported)
            {
                ref Vector4 srcRef = ref MemoryMarshal.GetReference(source);
                ref byte dstRef = ref MemoryMarshal.GetReference(destination);
                const byte kSwapRB = 0xC6;

                for (; i + 4 <= pixelCount; i += 4)
                {
                    ref float fBase = ref Unsafe.As<Vector4, float>(ref Unsafe.Add(ref srcRef, i));
                    var f0 = Sse.Shuffle(Vector128.LoadUnsafe(ref fBase), Vector128.LoadUnsafe(ref fBase), kSwapRB);
                    var f1 = Sse.Shuffle(Vector128.LoadUnsafe(ref fBase, 4), Vector128.LoadUnsafe(ref fBase, 4), kSwapRB);
                    var f2 = Sse.Shuffle(Vector128.LoadUnsafe(ref fBase, 8), Vector128.LoadUnsafe(ref fBase, 8), kSwapRB);
                    var f3 = Sse.Shuffle(Vector128.LoadUnsafe(ref fBase, 12), Vector128.LoadUnsafe(ref fBase, 12), kSwapRB);

                    var bytes = ClampScaleAndPack(f0, f1, f2, f3);
                    bytes.StoreUnsafe(ref dstRef, (nuint)(i * 4));
                }
            }

            for (; i < pixelCount; i++)
            {
                int o = i * 4;
                var p = source[i];
                destination[o + 0] = (byte)(Math.Clamp(p.Z, 0f, 1f) * 255f + 0.5f);
                destination[o + 1] = (byte)(Math.Clamp(p.Y, 0f, 1f) * 255f + 0.5f);
                destination[o + 2] = (byte)(Math.Clamp(p.X, 0f, 1f) * 255f + 0.5f);
                destination[o + 3] = (byte)(Math.Clamp(p.W, 0f, 1f) * 255f + 0.5f);
            }
        }

        #region Private SIMD Helpers

        /// <summary>
        /// Unpacks 16 bytes (4 RGBA pixels) into 4 <see cref="Vector4"/> values via SSE2 widen chain.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static void Unpack4PixelsToVector4(
            Vector128<byte> bytes16,
            Vector128<float> inv255,
            ref Vector4 dstRef,
            int dstIndex)
        {
            // byte → ushort (zero extend)
            var lo16 = Sse2.UnpackLow(bytes16, Vector128<byte>.Zero).AsUInt16();
            var hi16 = Sse2.UnpackHigh(bytes16, Vector128<byte>.Zero).AsUInt16();

            // ushort → uint → float, multiply by 1/255
            var px0 = Sse.Multiply(Sse2.ConvertToVector128Single(Sse2.UnpackLow(lo16, Vector128<ushort>.Zero).AsInt32()), inv255);
            var px1 = Sse.Multiply(Sse2.ConvertToVector128Single(Sse2.UnpackHigh(lo16, Vector128<ushort>.Zero).AsInt32()), inv255);
            var px2 = Sse.Multiply(Sse2.ConvertToVector128Single(Sse2.UnpackLow(hi16, Vector128<ushort>.Zero).AsInt32()), inv255);
            var px3 = Sse.Multiply(Sse2.ConvertToVector128Single(Sse2.UnpackHigh(hi16, Vector128<ushort>.Zero).AsInt32()), inv255);

            // Store 4 Vector4s (each is 4 floats = 1 Vector128<float>)
            ref float f = ref Unsafe.As<Vector4, float>(ref Unsafe.Add(ref dstRef, dstIndex));
            px0.StoreUnsafe(ref f);
            px1.StoreUnsafe(ref f, 4);
            px2.StoreUnsafe(ref f, 8);
            px3.StoreUnsafe(ref f, 12);
        }

        /// <summary>
        /// Packs 4 <see cref="Vector4"/> pixels into 16 bytes (RGBA order) via SSE2 clamp+scale+pack chain.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static Vector128<byte> Pack4PixelsFromVector4(ref Vector4 srcRef, int srcIndex)
        {
            ref float fBase = ref Unsafe.As<Vector4, float>(ref Unsafe.Add(ref srcRef, srcIndex));
            var f0 = Vector128.LoadUnsafe(ref fBase);
            var f1 = Vector128.LoadUnsafe(ref fBase, 4);
            var f2 = Vector128.LoadUnsafe(ref fBase, 8);
            var f3 = Vector128.LoadUnsafe(ref fBase, 12);
            return ClampScaleAndPack(f0, f1, f2, f3);
        }

        /// <summary>
        /// Clamps 4 float vectors to [0,1], scales to [0,255], rounds, and packs to 16 bytes.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static Vector128<byte> ClampScaleAndPack(
            Vector128<float> f0, Vector128<float> f1,
            Vector128<float> f2, Vector128<float> f3)
        {
            var scale = Vector128.Create(255f);
            var half = Vector128.Create(0.5f);
            var zero = Vector128<float>.Zero;
            var one = Vector128.Create(1f);

            f0 = Sse.Add(Sse.Multiply(Sse.Max(Sse.Min(f0, one), zero), scale), half);
            f1 = Sse.Add(Sse.Multiply(Sse.Max(Sse.Min(f1, one), zero), scale), half);
            f2 = Sse.Add(Sse.Multiply(Sse.Max(Sse.Min(f2, one), zero), scale), half);
            f3 = Sse.Add(Sse.Multiply(Sse.Max(Sse.Min(f3, one), zero), scale), half);

            var i0 = Sse2.ConvertToVector128Int32WithTruncation(f0);
            var i1 = Sse2.ConvertToVector128Int32WithTruncation(f1);
            var i2 = Sse2.ConvertToVector128Int32WithTruncation(f2);
            var i3 = Sse2.ConvertToVector128Int32WithTruncation(f3);

            // int32 → int16 (signed saturate) → byte (unsigned saturate)
            var s01 = Sse2.PackSignedSaturate(i0, i1);
            var s23 = Sse2.PackSignedSaturate(i2, i3);
            return Sse2.PackUnsignedSaturate(s01, s23);
        }

        #endregion
    }
}
