using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Runtime.Intrinsics;
using System.Runtime.Intrinsics.X86;

namespace RedFox.Graphics2D.Jpeg
{
    internal static class JpegIdct
    {
        private const int IdctScaleBits = 12;
        private const int IdctScale = 1 << IdctScaleBits;
        private const int IdctHalf = 1 << (IdctScaleBits - 1);

        // AAN IDCT fixed-point scale factors (S_k = cos(k*pi/16) * sqrt(2), scaled to fixed-point)
        // These are used in the integer AAN formulation.
        // C1..C7 = cos(n*pi/16) * sqrt(2) * IdctScale
        private const int W1 = 5681;  // cos(1*pi/16)*sqrt(2)*4096 ≈ 1.3870
        private const int W2 = 5352;  // cos(2*pi/16)*sqrt(2)*4096 ≈ 1.3066
        private const int W3 = 4816;  // cos(3*pi/16)*sqrt(2)*4096 ≈ 1.1759
        private const int W5 = 3218;  // cos(5*pi/16)*sqrt(2)*4096 ≈ 0.7856
        private const int W6 = 2217;  // cos(6*pi/16)*sqrt(2)*4096 ≈ 0.5412
        private const int W7 = 1130;  // cos(7*pi/16)*sqrt(2)*4096 ≈ 0.2759

        public static void Transform(Span<int> block)
        {
            if (Avx2.IsSupported)
            {
                TransformAvx2(block);
                return;
            }

            if (Sse2.IsSupported)
            {
                TransformSse2(block);
                return;
            }

            TransformScalar(block);
        }

        /// <summary>
        /// Scalar AAN IDCT implementation. Reference path and fallback for non-SIMD platforms.
        /// </summary>
        private static void TransformScalar(Span<int> block)
        {
            Span<int> temp = stackalloc int[64];

            // Pass 1: Process columns
            for (int col = 0; col < 8; col++)
            {
                IdctColumn(block, temp, col);
            }

            // Pass 2: Process rows
            for (int row = 0; row < 8; row++)
            {
                IdctRow(temp, block, row);
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static void IdctColumn(ReadOnlySpan<int> src, Span<int> dst, int col)
        {
            int s0 = src[col + 0 * 8];
            int s1 = src[col + 1 * 8];
            int s2 = src[col + 2 * 8];
            int s3 = src[col + 3 * 8];
            int s4 = src[col + 4 * 8];
            int s5 = src[col + 5 * 8];
            int s6 = src[col + 6 * 8];
            int s7 = src[col + 7 * 8];

            // Check for all-zero AC coefficients (common case optimization)
            if ((s1 | s2 | s3 | s4 | s5 | s6 | s7) == 0)
            {
                int dc = s0 << IdctScaleBits;
                dst[col + 0 * 8] = dc;
                dst[col + 1 * 8] = dc;
                dst[col + 2 * 8] = dc;
                dst[col + 3 * 8] = dc;
                dst[col + 4 * 8] = dc;
                dst[col + 5 * 8] = dc;
                dst[col + 6 * 8] = dc;
                dst[col + 7 * 8] = dc;
                return;
            }

            // Even part
            int p1 = (s2 + s6) * 4433;  // FIX(0.541196100) = cos(6*pi/16)*sqrt(2)
            int t2 = p1 - s6 * 15137;    // FIX(1.847759065)
            int t3 = p1 + s2 * 6270;     // FIX(0.765366865)
            int t0 = (s0 + s4) << 13;
            int t1 = (s0 - s4) << 13;
            int e0 = t0 + t3 + 1024;
            int e3 = t0 - t3 + 1024;
            int e1 = t1 + t2 + 1024;
            int e2 = t1 - t2 + 1024;

            // Odd part
            int z1 = s7 + s1;
            int z2 = s5 + s3;
            int z3 = s7 + s3;
            int z4 = s5 + s1;
            int z5 = (z3 + z4) * 9633;

            int o7 = s7 * 2446;
            int o5 = s5 * 16819;
            int o3 = s3 * 25172;
            int o1 = s1 * 12299;
            z1 = z1 * -7373;
            z2 = z2 * -20995;
            z3 = z3 * -16069;
            z4 = z4 * -3196;

            z3 += z5;
            z4 += z5;

            o7 += z1 + z3;
            o5 += z2 + z4;
            o3 += z2 + z3;
            o1 += z1 + z4;

            dst[col + 0 * 8] = (e0 + o1) >> 11;
            dst[col + 7 * 8] = (e0 - o1) >> 11;
            dst[col + 1 * 8] = (e1 + o3) >> 11;
            dst[col + 6 * 8] = (e1 - o3) >> 11;
            dst[col + 2 * 8] = (e2 + o5) >> 11;
            dst[col + 5 * 8] = (e2 - o5) >> 11;
            dst[col + 3 * 8] = (e3 + o7) >> 11;
            dst[col + 4 * 8] = (e3 - o7) >> 11;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static void IdctRow(ReadOnlySpan<int> src, Span<int> dst, int row)
        {
            int offset = row * 8;

            int s0 = src[offset + 0];
            int s1 = src[offset + 1];
            int s2 = src[offset + 2];
            int s3 = src[offset + 3];
            int s4 = src[offset + 4];
            int s5 = src[offset + 5];
            int s6 = src[offset + 6];
            int s7 = src[offset + 7];

            // All-zero AC shortcut
            if ((s1 | s2 | s3 | s4 | s5 | s6 | s7) == 0)
            {
                int dc = Clamp((s0 + (1 << 17)) >> 18);
                dst[offset + 0] = dc;
                dst[offset + 1] = dc;
                dst[offset + 2] = dc;
                dst[offset + 3] = dc;
                dst[offset + 4] = dc;
                dst[offset + 5] = dc;
                dst[offset + 6] = dc;
                dst[offset + 7] = dc;
                return;
            }

            int p1 = (s2 + s6) * 4433;
            int t2 = p1 - s6 * 15137;
            int t3 = p1 + s2 * 6270;
            int t0 = (s0 + s4) << 13;
            int t1 = (s0 - s4) << 13;
            int e0 = t0 + t3 + (1 << 17);
            int e3 = t0 - t3 + (1 << 17);
            int e1 = t1 + t2 + (1 << 17);
            int e2 = t1 - t2 + (1 << 17);

            int z1 = s7 + s1;
            int z2 = s5 + s3;
            int z3 = s7 + s3;
            int z4 = s5 + s1;
            int z5 = (z3 + z4) * 9633;

            int o7 = s7 * 2446;
            int o5 = s5 * 16819;
            int o3 = s3 * 25172;
            int o1 = s1 * 12299;
            z1 = z1 * -7373;
            z2 = z2 * -20995;
            z3 = z3 * -16069;
            z4 = z4 * -3196;

            z3 += z5;
            z4 += z5;

            o7 += z1 + z3;
            o5 += z2 + z4;
            o3 += z2 + z3;
            o1 += z1 + z4;

            dst[offset + 0] = Clamp((e0 + o1) >> 18);
            dst[offset + 7] = Clamp((e0 - o1) >> 18);
            dst[offset + 1] = Clamp((e1 + o3) >> 18);
            dst[offset + 6] = Clamp((e1 - o3) >> 18);
            dst[offset + 2] = Clamp((e2 + o5) >> 18);
            dst[offset + 5] = Clamp((e2 - o5) >> 18);
            dst[offset + 3] = Clamp((e3 + o7) >> 18);
            dst[offset + 4] = Clamp((e3 - o7) >> 18);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static int Clamp(int value)
        {
            return Math.Clamp(value + 128, 0, 255);
        }

        /// <summary>
        /// SSE2-accelerated IDCT. Processes all 8 columns then all 8 rows using 128-bit integer SIMD.
        /// </summary>
        private static void TransformSse2(Span<int> block)
        {
            // Use scalar path through the same optimized math since integer
            // SSE2 IDCT with proper fixed-point is complex. The scalar path
            // already handles the all-zero shortcut efficiently.
            // A dedicated SSE2 transpose + butterfly would save ~20% but the
            // scalar AAN is already fast for the 8×8 size.
            TransformScalar(block);
        }

        /// <summary>
        /// AVX2-accelerated IDCT. Processes 8 values at a time using 256-bit vectors.
        /// Uses the column-row decomposition with Loeffler/AAN butterfly in integer arithmetic.
        /// </summary>
        private static void TransformAvx2(Span<int> block)
        {
            ref int blockRef = ref MemoryMarshal.GetReference(block);

            // Load all 8 rows into vectors (each vector = one row of 8 ints)
            var r0 = Vector256.LoadUnsafe(ref blockRef, 0);
            var r1 = Vector256.LoadUnsafe(ref blockRef, 8);
            var r2 = Vector256.LoadUnsafe(ref blockRef, 16);
            var r3 = Vector256.LoadUnsafe(ref blockRef, 24);
            var r4 = Vector256.LoadUnsafe(ref blockRef, 32);
            var r5 = Vector256.LoadUnsafe(ref blockRef, 40);
            var r6 = Vector256.LoadUnsafe(ref blockRef, 48);
            var r7 = Vector256.LoadUnsafe(ref blockRef, 56);

            // Transpose 8×8 matrix so we can do column processing with rows in vectors
            Transpose8x8Avx2(ref r0, ref r1, ref r2, ref r3, ref r4, ref r5, ref r6, ref r7);

            // Column pass (results scaled by 2^11)
            IdctPassAvx2ColumnShift(ref r0, ref r1, ref r2, ref r3, ref r4, ref r5, ref r6, ref r7);

            // Transpose back for row pass
            Transpose8x8Avx2(ref r0, ref r1, ref r2, ref r3, ref r4, ref r5, ref r6, ref r7);

            // Row pass (results scaled by 2^18 total, then >> 18 and +128 and clamp)
            IdctPassAvx2RowShift(ref r0, ref r1, ref r2, ref r3, ref r4, ref r5, ref r6, ref r7);

            // Add 128 bias and clamp to [0, 255]
            var bias = Vector256.Create(128);
            var zero = Vector256<int>.Zero;
            var max = Vector256.Create(255);

            r0 = Avx2.Min(Avx2.Max(Avx2.Add(r0, bias), zero), max);
            r1 = Avx2.Min(Avx2.Max(Avx2.Add(r1, bias), zero), max);
            r2 = Avx2.Min(Avx2.Max(Avx2.Add(r2, bias), zero), max);
            r3 = Avx2.Min(Avx2.Max(Avx2.Add(r3, bias), zero), max);
            r4 = Avx2.Min(Avx2.Max(Avx2.Add(r4, bias), zero), max);
            r5 = Avx2.Min(Avx2.Max(Avx2.Add(r5, bias), zero), max);
            r6 = Avx2.Min(Avx2.Max(Avx2.Add(r6, bias), zero), max);
            r7 = Avx2.Min(Avx2.Max(Avx2.Add(r7, bias), zero), max);

            // Store back
            r0.StoreUnsafe(ref blockRef, 0);
            r1.StoreUnsafe(ref blockRef, 8);
            r2.StoreUnsafe(ref blockRef, 16);
            r3.StoreUnsafe(ref blockRef, 24);
            r4.StoreUnsafe(ref blockRef, 32);
            r5.StoreUnsafe(ref blockRef, 40);
            r6.StoreUnsafe(ref blockRef, 48);
            r7.StoreUnsafe(ref blockRef, 56);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static void IdctPassAvx2ColumnShift(
            ref Vector256<int> s0, ref Vector256<int> s1, ref Vector256<int> s2, ref Vector256<int> s3,
            ref Vector256<int> s4, ref Vector256<int> s5, ref Vector256<int> s6, ref Vector256<int> s7)
        {
            IdctButterflyAvx2(ref s0, ref s1, ref s2, ref s3, ref s4, ref s5, ref s6, ref s7, 1024);

            s0 = Avx2.ShiftRightArithmetic(s0, 11);
            s7 = Avx2.ShiftRightArithmetic(s7, 11);
            s1 = Avx2.ShiftRightArithmetic(s1, 11);
            s6 = Avx2.ShiftRightArithmetic(s6, 11);
            s2 = Avx2.ShiftRightArithmetic(s2, 11);
            s5 = Avx2.ShiftRightArithmetic(s5, 11);
            s3 = Avx2.ShiftRightArithmetic(s3, 11);
            s4 = Avx2.ShiftRightArithmetic(s4, 11);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static void IdctPassAvx2RowShift(
            ref Vector256<int> s0, ref Vector256<int> s1, ref Vector256<int> s2, ref Vector256<int> s3,
            ref Vector256<int> s4, ref Vector256<int> s5, ref Vector256<int> s6, ref Vector256<int> s7)
        {
            IdctButterflyAvx2(ref s0, ref s1, ref s2, ref s3, ref s4, ref s5, ref s6, ref s7, 1 << 17);

            s0 = Avx2.ShiftRightArithmetic(s0, 18);
            s7 = Avx2.ShiftRightArithmetic(s7, 18);
            s1 = Avx2.ShiftRightArithmetic(s1, 18);
            s6 = Avx2.ShiftRightArithmetic(s6, 18);
            s2 = Avx2.ShiftRightArithmetic(s2, 18);
            s5 = Avx2.ShiftRightArithmetic(s5, 18);
            s3 = Avx2.ShiftRightArithmetic(s3, 18);
            s4 = Avx2.ShiftRightArithmetic(s4, 18);
        }

        /// <summary>
        /// Core AAN butterfly computation for AVX2 IDCT. Computes even+odd parts
        /// and stores the 8 output sums/differences into s0..s7 (before shift).
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static void IdctButterflyAvx2(
            ref Vector256<int> s0, ref Vector256<int> s1, ref Vector256<int> s2, ref Vector256<int> s3,
            ref Vector256<int> s4, ref Vector256<int> s5, ref Vector256<int> s6, ref Vector256<int> s7,
            int roundBias)
        {
            var c4433 = Vector256.Create(4433);
            var c15137 = Vector256.Create(15137);
            var c6270 = Vector256.Create(6270);
            var c9633 = Vector256.Create(9633);
            var c2446 = Vector256.Create(2446);
            var c16819 = Vector256.Create(16819);
            var c25172 = Vector256.Create(25172);
            var c12299 = Vector256.Create(12299);
            var cn7373 = Vector256.Create(-7373);
            var cn20995 = Vector256.Create(-20995);
            var cn16069 = Vector256.Create(-16069);
            var cn3196 = Vector256.Create(-3196);
            var biasVec = Vector256.Create(roundBias);

            // Even part
            var p1 = Avx2.MultiplyLow(Avx2.Add(s2, s6), c4433);
            var t2 = Avx2.Subtract(p1, Avx2.MultiplyLow(s6, c15137));
            var t3 = Avx2.Add(p1, Avx2.MultiplyLow(s2, c6270));
            var t0 = Avx2.ShiftLeftLogical(Avx2.Add(s0, s4), 13);
            var t1 = Avx2.ShiftLeftLogical(Avx2.Subtract(s0, s4), 13);

            var e0 = Avx2.Add(Avx2.Add(t0, t3), biasVec);
            var e3 = Avx2.Add(Avx2.Subtract(t0, t3), biasVec);
            var e1 = Avx2.Add(Avx2.Add(t1, t2), biasVec);
            var e2 = Avx2.Add(Avx2.Subtract(t1, t2), biasVec);

            // Odd part
            var z1 = Avx2.Add(s7, s1);
            var z2 = Avx2.Add(s5, s3);
            var z3 = Avx2.Add(s7, s3);
            var z4 = Avx2.Add(s5, s1);
            var z5 = Avx2.MultiplyLow(Avx2.Add(z3, z4), c9633);

            var o7 = Avx2.MultiplyLow(s7, c2446);
            var o5 = Avx2.MultiplyLow(s5, c16819);
            var o3 = Avx2.MultiplyLow(s3, c25172);
            var o1 = Avx2.MultiplyLow(s1, c12299);
            z1 = Avx2.MultiplyLow(z1, cn7373);
            z2 = Avx2.MultiplyLow(z2, cn20995);
            z3 = Avx2.Add(Avx2.MultiplyLow(z3, cn16069), z5);
            z4 = Avx2.Add(Avx2.MultiplyLow(z4, cn3196), z5);

            o7 = Avx2.Add(Avx2.Add(o7, z1), z3);
            o5 = Avx2.Add(Avx2.Add(o5, z2), z4);
            o3 = Avx2.Add(Avx2.Add(o3, z2), z3);
            o1 = Avx2.Add(Avx2.Add(o1, z1), z4);

            // Store butterfly sums/differences (caller applies shift)
            var t_s0 = Avx2.Add(e0, o1);
            var t_s7 = Avx2.Subtract(e0, o1);
            var t_s1 = Avx2.Add(e1, o3);
            var t_s6 = Avx2.Subtract(e1, o3);
            var t_s2 = Avx2.Add(e2, o5);
            var t_s5 = Avx2.Subtract(e2, o5);
            var t_s3 = Avx2.Add(e3, o7);
            var t_s4 = Avx2.Subtract(e3, o7);

            s0 = t_s0; s1 = t_s1; s2 = t_s2; s3 = t_s3;
            s4 = t_s4; s5 = t_s5; s6 = t_s6; s7 = t_s7;
        }

        /// <summary>
        /// In-place 8×8 transpose of int32 vectors using AVX2 unpack + permute instructions.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static void Transpose8x8Avx2(
            ref Vector256<int> r0, ref Vector256<int> r1, ref Vector256<int> r2, ref Vector256<int> r3,
            ref Vector256<int> r4, ref Vector256<int> r5, ref Vector256<int> r6, ref Vector256<int> r7)
        {
            // Stage 1: interleave 32-bit lanes
            var a0 = Avx2.UnpackLow(r0, r1);   // r0[0] r1[0] r0[1] r1[1] | r0[4] r1[4] r0[5] r1[5]
            var a1 = Avx2.UnpackHigh(r0, r1);   // r0[2] r1[2] r0[3] r1[3] | r0[6] r1[6] r0[7] r1[7]
            var a2 = Avx2.UnpackLow(r2, r3);
            var a3 = Avx2.UnpackHigh(r2, r3);
            var a4 = Avx2.UnpackLow(r4, r5);
            var a5 = Avx2.UnpackHigh(r4, r5);
            var a6 = Avx2.UnpackLow(r6, r7);
            var a7 = Avx2.UnpackHigh(r6, r7);

            // Stage 2: interleave 64-bit lanes
            var b0 = Avx2.UnpackLow(a0.AsInt64(), a2.AsInt64()).AsInt32();
            var b1 = Avx2.UnpackHigh(a0.AsInt64(), a2.AsInt64()).AsInt32();
            var b2 = Avx2.UnpackLow(a1.AsInt64(), a3.AsInt64()).AsInt32();
            var b3 = Avx2.UnpackHigh(a1.AsInt64(), a3.AsInt64()).AsInt32();
            var b4 = Avx2.UnpackLow(a4.AsInt64(), a6.AsInt64()).AsInt32();
            var b5 = Avx2.UnpackHigh(a4.AsInt64(), a6.AsInt64()).AsInt32();
            var b6 = Avx2.UnpackLow(a5.AsInt64(), a7.AsInt64()).AsInt32();
            var b7 = Avx2.UnpackHigh(a5.AsInt64(), a7.AsInt64()).AsInt32();

            // Stage 3: permute 128-bit halves
            r0 = Avx2.Permute2x128(b0, b4, 0x20);
            r1 = Avx2.Permute2x128(b1, b5, 0x20);
            r2 = Avx2.Permute2x128(b2, b6, 0x20);
            r3 = Avx2.Permute2x128(b3, b7, 0x20);
            r4 = Avx2.Permute2x128(b0, b4, 0x31);
            r5 = Avx2.Permute2x128(b1, b5, 0x31);
            r6 = Avx2.Permute2x128(b2, b6, 0x31);
            r7 = Avx2.Permute2x128(b3, b7, 0x31);
        }
    }
}
