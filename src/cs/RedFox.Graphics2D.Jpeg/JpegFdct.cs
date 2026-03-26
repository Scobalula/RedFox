using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Runtime.Intrinsics;
using System.Runtime.Intrinsics.X86;

namespace RedFox.Graphics2D.Jpeg;

/// <summary>
/// Forward Discrete Cosine Transform for 8×8 blocks using the AAN algorithm.
/// Supports scalar, SSE2, and AVX2 fast paths.
/// </summary>
public static class JpegFdct
{
    // AAN FDCT fixed-point constants (same butterfly structure as IDCT, reversed flow).
    // These match the constants used in libjpeg's jfdctint.c (scaled by 2^13).
    private const int Fix_0_298631336 = 2446;
    private const int Fix_0_390180644 = 3196;
    private const int Fix_0_541196100 = 4433;
    private const int Fix_0_765366865 = 6270;
    private const int Fix_0_899976223 = 7373;
    private const int Fix_1_175875602 = 9633;
    private const int Fix_1_501321110 = 12299;
    private const int Fix_1_847759065 = 15137;
    private const int Fix_1_961570560 = 16069;
    private const int Fix_2_053119869 = 16819;
    private const int Fix_2_562915447 = 20995;
    private const int Fix_3_072711026 = 25172;

    /// <summary>Performs a forward DCT on an 8×8 block of spatial-domain samples in-place.</summary>
    /// <param name="block">A 64-element span of integer samples; on return, contains DCT coefficients.</param>
    public static void Transform(Span<int> block)
    {
        if (Avx2.IsSupported)
        {
            TransformAvx2(block);
            return;
        }

        TransformScalar(block);
    }

    private static void TransformScalar(Span<int> block)
    {
        // Pass 1: Process rows
        for (int row = 0; row < 8; row++)
        {
            FdctRow(block, row);
        }

        // Pass 2: Process columns
        for (int col = 0; col < 8; col++)
        {
            FdctColumn(block, col);
        }
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static void FdctRow(Span<int> block, int row)
    {
        int offset = row * 8;

        int s0 = block[offset + 0];
        int s1 = block[offset + 1];
        int s2 = block[offset + 2];
        int s3 = block[offset + 3];
        int s4 = block[offset + 4];
        int s5 = block[offset + 5];
        int s6 = block[offset + 6];
        int s7 = block[offset + 7];

        // Stage 1: butterfly
        int tmp0 = s0 + s7;
        int tmp7 = s0 - s7;
        int tmp1 = s1 + s6;
        int tmp6 = s1 - s6;
        int tmp2 = s2 + s5;
        int tmp5 = s2 - s5;
        int tmp3 = s3 + s4;
        int tmp4 = s3 - s4;

        // Even part
        int tmp10 = tmp0 + tmp3;
        int tmp13 = tmp0 - tmp3;
        int tmp11 = tmp1 + tmp2;
        int tmp12 = tmp1 - tmp2;

        // Apply scaling (shift left by 13 for fixed-point precision in row pass)
        block[offset + 0] = (tmp10 + tmp11) << 2;
        block[offset + 4] = (tmp10 - tmp11) << 2;

        int zRot = (tmp12 + tmp13) * Fix_0_541196100;
        block[offset + 2] = (zRot + tmp13 * Fix_0_765366865 + 1024) >> 11;
        block[offset + 6] = (zRot - tmp12 * Fix_1_847759065 + 1024) >> 11;

        // Odd part (per libjpeg jfdctint.c — same butterfly constants as IDCT)
        int z1 = tmp4 + tmp7;
        int z2 = tmp5 + tmp6;
        int z3 = tmp4 + tmp6;
        int z4 = tmp5 + tmp7;
        int z5 = (z3 + z4) * Fix_1_175875602;

        tmp4 *= Fix_0_298631336;
        tmp5 *= Fix_2_053119869;
        tmp6 *= Fix_3_072711026;
        tmp7 *= Fix_1_501321110;
        z1 *= -Fix_0_899976223;
        z2 *= -Fix_2_562915447;
        z3 = z3 * -Fix_1_961570560 + z5;
        z4 = z4 * -Fix_0_390180644 + z5;

        block[offset + 7] = (tmp4 + z1 + z3 + 1024) >> 11;
        block[offset + 5] = (tmp5 + z2 + z4 + 1024) >> 11;
        block[offset + 3] = (tmp6 + z2 + z3 + 1024) >> 11;
        block[offset + 1] = (tmp7 + z1 + z4 + 1024) >> 11;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static void FdctColumn(Span<int> block, int col)
    {
        int s0 = block[col + 0 * 8];
        int s1 = block[col + 1 * 8];
        int s2 = block[col + 2 * 8];
        int s3 = block[col + 3 * 8];
        int s4 = block[col + 4 * 8];
        int s5 = block[col + 5 * 8];
        int s6 = block[col + 6 * 8];
        int s7 = block[col + 7 * 8];

        int tmp0 = s0 + s7;
        int tmp7 = s0 - s7;
        int tmp1 = s1 + s6;
        int tmp6 = s1 - s6;
        int tmp2 = s2 + s5;
        int tmp5 = s2 - s5;
        int tmp3 = s3 + s4;
        int tmp4 = s3 - s4;

        int tmp10 = tmp0 + tmp3;
        int tmp13 = tmp0 - tmp3;
        int tmp11 = tmp1 + tmp2;
        int tmp12 = tmp1 - tmp2;

        // Column pass final descale: >> (PASS1_BITS + 3) = >> 5 for even, >> (CONST_BITS + PASS1_BITS + 3) = >> 18 for rotated
        block[col + 0 * 8] = (tmp10 + tmp11 + (1 << 4)) >> 5;
        block[col + 4 * 8] = (tmp10 - tmp11 + (1 << 4)) >> 5;

        int zRot = (tmp12 + tmp13) * Fix_0_541196100;
        block[col + 2 * 8] = (zRot + tmp13 * Fix_0_765366865 + (1 << 17)) >> 18;
        block[col + 6 * 8] = (zRot - tmp12 * Fix_1_847759065 + (1 << 17)) >> 18;

        int z1 = tmp4 + tmp7;
        int z2 = tmp5 + tmp6;
        int z3 = tmp4 + tmp6;
        int z4 = tmp5 + tmp7;
        int z5 = (z3 + z4) * Fix_1_175875602;

        tmp4 *= Fix_0_298631336;
        tmp5 *= Fix_2_053119869;
        tmp6 *= Fix_3_072711026;
        tmp7 *= Fix_1_501321110;
        z1 *= -Fix_0_899976223;
        z2 *= -Fix_2_562915447;
        z3 = z3 * -Fix_1_961570560 + z5;
        z4 = z4 * -Fix_0_390180644 + z5;

        block[col + 7 * 8] = (tmp4 + z1 + z3 + (1 << 17)) >> 18;
        block[col + 5 * 8] = (tmp5 + z2 + z4 + (1 << 17)) >> 18;
        block[col + 3 * 8] = (tmp6 + z2 + z3 + (1 << 17)) >> 18;
        block[col + 1 * 8] = (tmp7 + z1 + z4 + (1 << 17)) >> 18;
    }

    private static void TransformAvx2(Span<int> block)
    {
        ref int blockRef = ref MemoryMarshal.GetReference(block);

        // Load all 8 rows into vectors
        var r0 = Vector256.LoadUnsafe(ref blockRef, 0);
        var r1 = Vector256.LoadUnsafe(ref blockRef, 8);
        var r2 = Vector256.LoadUnsafe(ref blockRef, 16);
        var r3 = Vector256.LoadUnsafe(ref blockRef, 24);
        var r4 = Vector256.LoadUnsafe(ref blockRef, 32);
        var r5 = Vector256.LoadUnsafe(ref blockRef, 40);
        var r6 = Vector256.LoadUnsafe(ref blockRef, 48);
        var r7 = Vector256.LoadUnsafe(ref blockRef, 56);

        Transpose8x8Avx2(ref r0, ref r1, ref r2, ref r3, ref r4, ref r5, ref r6, ref r7);
        FdctPassAvx2Row(ref r0, ref r1, ref r2, ref r3, ref r4, ref r5, ref r6, ref r7);
        Transpose8x8Avx2(ref r0, ref r1, ref r2, ref r3, ref r4, ref r5, ref r6, ref r7);
        FdctPassAvx2Column(ref r0, ref r1, ref r2, ref r3, ref r4, ref r5, ref r6, ref r7);

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
    private static void FdctPassAvx2Row(
        ref Vector256<int> s0, ref Vector256<int> s1, ref Vector256<int> s2, ref Vector256<int> s3,
        ref Vector256<int> s4, ref Vector256<int> s5, ref Vector256<int> s6, ref Vector256<int> s7)
    {
        FdctButterflyAvx2(ref s0, ref s1, ref s2, ref s3, ref s4, ref s5, ref s6, ref s7, isRowPass: true);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static void FdctPassAvx2Column(
        ref Vector256<int> s0, ref Vector256<int> s1, ref Vector256<int> s2, ref Vector256<int> s3,
        ref Vector256<int> s4, ref Vector256<int> s5, ref Vector256<int> s6, ref Vector256<int> s7)
    {
        FdctButterflyAvx2(ref s0, ref s1, ref s2, ref s3, ref s4, ref s5, ref s6, ref s7, isRowPass: false);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static void FdctButterflyAvx2(
        ref Vector256<int> s0, ref Vector256<int> s1, ref Vector256<int> s2, ref Vector256<int> s3,
        ref Vector256<int> s4, ref Vector256<int> s5, ref Vector256<int> s6, ref Vector256<int> s7,
        bool isRowPass)
    {
        // Stage 1: butterfly sums/differences
        var tmp0 = Avx2.Add(s0, s7);
        var tmp7 = Avx2.Subtract(s0, s7);
        var tmp1 = Avx2.Add(s1, s6);
        var tmp6 = Avx2.Subtract(s1, s6);
        var tmp2 = Avx2.Add(s2, s5);
        var tmp5 = Avx2.Subtract(s2, s5);
        var tmp3 = Avx2.Add(s3, s4);
        var tmp4 = Avx2.Subtract(s3, s4);

        // Even part
        var tmp10 = Avx2.Add(tmp0, tmp3);
        var tmp13 = Avx2.Subtract(tmp0, tmp3);
        var tmp11 = Avx2.Add(tmp1, tmp2);
        var tmp12 = Avx2.Subtract(tmp1, tmp2);

        var c4433 = Vector256.Create(Fix_0_541196100);
        var c6270 = Vector256.Create(Fix_0_765366865);
        var c15137 = Vector256.Create(Fix_1_847759065);
        var c12299 = Vector256.Create(Fix_1_501321110);
        var c9633 = Vector256.Create(Fix_1_175875602);

        if (isRowPass)
        {
            // Row pass: << 2 for even, >> 11 for rotated
            s0 = Avx2.ShiftLeftLogical(Avx2.Add(tmp10, tmp11), 2);
            s4 = Avx2.ShiftLeftLogical(Avx2.Subtract(tmp10, tmp11), 2);

            var zRot = Avx2.MultiplyLow(Avx2.Add(tmp12, tmp13), c4433);
            var bias = Vector256.Create(1024);
            s2 = Avx2.ShiftRightArithmetic(Avx2.Add(Avx2.Add(zRot, Avx2.MultiplyLow(tmp13, c6270)), bias), 11);
            s6 = Avx2.ShiftRightArithmetic(Avx2.Add(Avx2.Subtract(zRot, Avx2.MultiplyLow(tmp12, c15137)), bias), 11);
        }
        else
        {
            // Column pass: >> (PASS1_BITS+3)=5 for even, >> (CONST_BITS+PASS1_BITS+3)=18 for rotated
            var bias5 = Vector256.Create(1 << 4);
            s0 = Avx2.ShiftRightArithmetic(Avx2.Add(Avx2.Add(tmp10, tmp11), bias5), 5);
            s4 = Avx2.ShiftRightArithmetic(Avx2.Add(Avx2.Subtract(tmp10, tmp11), bias5), 5);

            var zRot = Avx2.MultiplyLow(Avx2.Add(tmp12, tmp13), c4433);
            var bias18 = Vector256.Create(1 << 17);
            s2 = Avx2.ShiftRightArithmetic(Avx2.Add(Avx2.Add(zRot, Avx2.MultiplyLow(tmp13, c6270)), bias18), 18);
            s6 = Avx2.ShiftRightArithmetic(Avx2.Add(Avx2.Subtract(zRot, Avx2.MultiplyLow(tmp12, c15137)), bias18), 18);
        }

        // Odd part (libjpeg jfdctint.c butterfly — same constants as IDCT)
        var c2446 = Vector256.Create(Fix_0_298631336);
        var c16819 = Vector256.Create(Fix_2_053119869);
        var c25172 = Vector256.Create(Fix_3_072711026);
        var cn16069 = Vector256.Create(-Fix_1_961570560);
        var cn3196 = Vector256.Create(-Fix_0_390180644);

        var z1 = Avx2.Add(tmp4, tmp7);
        var z2 = Avx2.Add(tmp5, tmp6);
        var z3 = Avx2.Add(tmp4, tmp6);
        var z4 = Avx2.Add(tmp5, tmp7);
        var z5 = Avx2.MultiplyLow(Avx2.Add(z3, z4), c9633);

        tmp4 = Avx2.MultiplyLow(tmp4, c2446);
        tmp5 = Avx2.MultiplyLow(tmp5, c16819);
        tmp6 = Avx2.MultiplyLow(tmp6, c25172);
        tmp7 = Avx2.MultiplyLow(tmp7, c12299);
        z1 = Avx2.MultiplyLow(z1, Vector256.Create(-Fix_0_899976223));
        z2 = Avx2.MultiplyLow(z2, Vector256.Create(-Fix_2_562915447));
        z3 = Avx2.Add(Avx2.MultiplyLow(z3, cn16069), z5);
        z4 = Avx2.Add(Avx2.MultiplyLow(z4, cn3196), z5);

        if (isRowPass)
        {
            var bias = Vector256.Create(1024);
            s7 = Avx2.ShiftRightArithmetic(Avx2.Add(Avx2.Add(Avx2.Add(tmp4, z1), z3), bias), 11);
            s5 = Avx2.ShiftRightArithmetic(Avx2.Add(Avx2.Add(Avx2.Add(tmp5, z2), z4), bias), 11);
            s3 = Avx2.ShiftRightArithmetic(Avx2.Add(Avx2.Add(Avx2.Add(tmp6, z2), z3), bias), 11);
            s1 = Avx2.ShiftRightArithmetic(Avx2.Add(Avx2.Add(Avx2.Add(tmp7, z1), z4), bias), 11);
        }
        else
        {
            var bias18 = Vector256.Create(1 << 17);
            s7 = Avx2.ShiftRightArithmetic(Avx2.Add(Avx2.Add(Avx2.Add(tmp4, z1), z3), bias18), 18);
            s5 = Avx2.ShiftRightArithmetic(Avx2.Add(Avx2.Add(Avx2.Add(tmp5, z2), z4), bias18), 18);
            s3 = Avx2.ShiftRightArithmetic(Avx2.Add(Avx2.Add(Avx2.Add(tmp6, z2), z3), bias18), 18);
            s1 = Avx2.ShiftRightArithmetic(Avx2.Add(Avx2.Add(Avx2.Add(tmp7, z1), z4), bias18), 18);
        }
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static void Transpose8x8Avx2(
        ref Vector256<int> r0, ref Vector256<int> r1, ref Vector256<int> r2, ref Vector256<int> r3,
        ref Vector256<int> r4, ref Vector256<int> r5, ref Vector256<int> r6, ref Vector256<int> r7)
    {
        var a0 = Avx2.UnpackLow(r0, r1);
        var a1 = Avx2.UnpackHigh(r0, r1);
        var a2 = Avx2.UnpackLow(r2, r3);
        var a3 = Avx2.UnpackHigh(r2, r3);
        var a4 = Avx2.UnpackLow(r4, r5);
        var a5 = Avx2.UnpackHigh(r4, r5);
        var a6 = Avx2.UnpackLow(r6, r7);
        var a7 = Avx2.UnpackHigh(r6, r7);

        var b0 = Avx2.UnpackLow(a0.AsInt64(), a2.AsInt64()).AsInt32();
        var b1 = Avx2.UnpackHigh(a0.AsInt64(), a2.AsInt64()).AsInt32();
        var b2 = Avx2.UnpackLow(a1.AsInt64(), a3.AsInt64()).AsInt32();
        var b3 = Avx2.UnpackHigh(a1.AsInt64(), a3.AsInt64()).AsInt32();
        var b4 = Avx2.UnpackLow(a4.AsInt64(), a6.AsInt64()).AsInt32();
        var b5 = Avx2.UnpackHigh(a4.AsInt64(), a6.AsInt64()).AsInt32();
        var b6 = Avx2.UnpackLow(a5.AsInt64(), a7.AsInt64()).AsInt32();
        var b7 = Avx2.UnpackHigh(a5.AsInt64(), a7.AsInt64()).AsInt32();

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
