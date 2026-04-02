using System.Numerics;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Runtime.Intrinsics;
using System.Runtime.Intrinsics.X86;

namespace RedFox.Graphics2D.BC
{
    /// <summary>
    /// SIMD helpers for BC block encode/decode hot paths.
    /// Uses SSE/SSE2 when available and falls back to scalar code otherwise.
    /// </summary>
    public static class BcSimd
    {
        /// <summary>
        /// Quantizes RGBA pixels to four byte-range channel arrays.
        /// </summary>
        /// <param name="pixels">The source pixels to quantize.</param>
        /// <param name="r">The destination red-channel values in the range 0-255.</param>
        /// <param name="g">The destination green-channel values in the range 0-255.</param>
        /// <param name="b">The destination blue-channel values in the range 0-255.</param>
        /// <param name="a">The destination alpha-channel values in the range 0-255.</param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void QuantizeToRgba8Channels(ReadOnlySpan<Vector4> pixels, Span<int> r, Span<int> g, Span<int> b, Span<int> a)
        {
            int i = 0;

            if (Sse.IsSupported && Sse2.IsSupported)
            {
                ref Vector4 srcRef = ref MemoryMarshal.GetReference(pixels);
                ref int rRef = ref MemoryMarshal.GetReference(r);
                ref int gRef = ref MemoryMarshal.GetReference(g);
                ref int bRef = ref MemoryMarshal.GetReference(b);
                ref int aRef = ref MemoryMarshal.GetReference(a);

                for (; i + 4 <= pixels.Length; i += 4)
                {
                    ref float fBase = ref Unsafe.As<Vector4, float>(ref Unsafe.Add(ref srcRef, i));
                    Vector128<float> p0 = Vector128.LoadUnsafe(ref fBase);
                    Vector128<float> p1 = Vector128.LoadUnsafe(ref fBase, 4);
                    Vector128<float> p2 = Vector128.LoadUnsafe(ref fBase, 8);
                    Vector128<float> p3 = Vector128.LoadUnsafe(ref fBase, 12);

                    Transpose4x4(p0, p1, p2, p3, out Vector128<float> vr, out Vector128<float> vg, out Vector128<float> vb, out Vector128<float> va);

                    QuantizeToByteRange(vr).StoreUnsafe(ref rRef, (nuint)i);
                    QuantizeToByteRange(vg).StoreUnsafe(ref gRef, (nuint)i);
                    QuantizeToByteRange(vb).StoreUnsafe(ref bRef, (nuint)i);
                    QuantizeToByteRange(va).StoreUnsafe(ref aRef, (nuint)i);
                }
            }

            for (; i < pixels.Length; i++)
            {
                r[i] = Math.Clamp((int)(pixels[i].X * 255f + 0.5f), 0, 255);
                g[i] = Math.Clamp((int)(pixels[i].Y * 255f + 0.5f), 0, 255);
                b[i] = Math.Clamp((int)(pixels[i].Z * 255f + 0.5f), 0, 255);
                a[i] = Math.Clamp((int)(pixels[i].W * 255f + 0.5f), 0, 255);
            }
        }

        /// <summary>
        /// Stores RGBA channel arrays as normalized <see cref="Vector4"/> pixels.
        /// </summary>
        /// <param name="r">The source red-channel values in the range 0-255.</param>
        /// <param name="g">The source green-channel values in the range 0-255.</param>
        /// <param name="b">The source blue-channel values in the range 0-255.</param>
        /// <param name="a">The source alpha-channel values in the range 0-255.</param>
        /// <param name="pixels">The destination normalized pixels.</param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void StoreNormalizedRgba8(ReadOnlySpan<int> r, ReadOnlySpan<int> g, ReadOnlySpan<int> b, ReadOnlySpan<int> a, Span<Vector4> pixels)
        {
            int i = 0;

            if (Sse.IsSupported && Sse2.IsSupported)
            {
                ref int rRef = ref MemoryMarshal.GetReference(r);
                ref int gRef = ref MemoryMarshal.GetReference(g);
                ref int bRef = ref MemoryMarshal.GetReference(b);
                ref int aRef = ref MemoryMarshal.GetReference(a);
                ref Vector4 dstRef = ref MemoryMarshal.GetReference(pixels);
                Vector128<float> inv255 = Vector128.Create(1.0f / 255.0f);

                for (; i + 4 <= pixels.Length; i += 4)
                {
                    Vector128<float> vr = Sse.Multiply(Sse2.ConvertToVector128Single(Vector128.LoadUnsafe(ref rRef, (nuint)i)), inv255);
                    Vector128<float> vg = Sse.Multiply(Sse2.ConvertToVector128Single(Vector128.LoadUnsafe(ref gRef, (nuint)i)), inv255);
                    Vector128<float> vb = Sse.Multiply(Sse2.ConvertToVector128Single(Vector128.LoadUnsafe(ref bRef, (nuint)i)), inv255);
                    Vector128<float> va = Sse.Multiply(Sse2.ConvertToVector128Single(Vector128.LoadUnsafe(ref aRef, (nuint)i)), inv255);

                    Transpose4x4(vr, vg, vb, va, out Vector128<float> p0, out Vector128<float> p1, out Vector128<float> p2, out Vector128<float> p3);

                    ref float fBase = ref Unsafe.As<Vector4, float>(ref Unsafe.Add(ref dstRef, i));
                    p0.StoreUnsafe(ref fBase);
                    p1.StoreUnsafe(ref fBase, 4);
                    p2.StoreUnsafe(ref fBase, 8);
                    p3.StoreUnsafe(ref fBase, 12);
                }
            }

            for (; i < pixels.Length; i++)
                pixels[i] = new Vector4(r[i] * (1.0f / 255.0f), g[i] * (1.0f / 255.0f), b[i] * (1.0f / 255.0f), a[i] * (1.0f / 255.0f));
        }

        /// <summary>
        /// Finds the best interpolation index per pixel for a 4-channel block.
        /// </summary>
        /// <param name="rPix">The source red-channel pixel values.</param>
        /// <param name="gPix">The source green-channel pixel values.</param>
        /// <param name="bPix">The source blue-channel pixel values.</param>
        /// <param name="aPix">The source alpha-channel pixel values.</param>
        /// <param name="rCandidates">The candidate red-channel interpolated values.</param>
        /// <param name="gCandidates">The candidate green-channel interpolated values.</param>
        /// <param name="bCandidates">The candidate blue-channel interpolated values.</param>
        /// <param name="aCandidates">The candidate alpha-channel interpolated values.</param>
        /// <param name="indices">The destination span receiving the best candidate index for each pixel.</param>
        /// <returns>The summed squared error across all pixels.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static float FindBestIndices4Channel(ReadOnlySpan<int> rPix, ReadOnlySpan<int> gPix, ReadOnlySpan<int> bPix, ReadOnlySpan<int> aPix, ReadOnlySpan<float> rCandidates, ReadOnlySpan<float> gCandidates, ReadOnlySpan<float> bCandidates, ReadOnlySpan<float> aCandidates, Span<int> indices)
        {
            if (!(Sse.IsSupported && Sse2.IsSupported))
                return FindBestIndices4ChannelScalar(rPix, gPix, bPix, aPix, rCandidates, gCandidates, bCandidates, aCandidates, indices);

            float totalError = 0f;
            Span<float> errorBuffer = stackalloc float[4];
            Span<int> indexBuffer = stackalloc int[4];
            ref int rRef = ref MemoryMarshal.GetReference(rPix);
            ref int gRef = ref MemoryMarshal.GetReference(gPix);
            ref int bRef = ref MemoryMarshal.GetReference(bPix);
            ref int aRef = ref MemoryMarshal.GetReference(aPix);
            ref float errRef = ref MemoryMarshal.GetReference(errorBuffer);
            ref int idxRef = ref MemoryMarshal.GetReference(indexBuffer);
            Vector128<float> inf = Vector128.Create(float.MaxValue);

            for (int i = 0; i < rPix.Length; i += 4)
            {
                Vector128<float> vr = Sse2.ConvertToVector128Single(Vector128.LoadUnsafe(ref rRef, (nuint)i));
                Vector128<float> vg = Sse2.ConvertToVector128Single(Vector128.LoadUnsafe(ref gRef, (nuint)i));
                Vector128<float> vb = Sse2.ConvertToVector128Single(Vector128.LoadUnsafe(ref bRef, (nuint)i));
                Vector128<float> va = Sse2.ConvertToVector128Single(Vector128.LoadUnsafe(ref aRef, (nuint)i));

                Vector128<float> bestErr = inf;
                Vector128<int> bestIdx = Vector128<int>.Zero;

                for (int j = 0; j < rCandidates.Length; j++)
                {
                    Vector128<float> err = ComputeSquaredError(vr, vg, vb, va, Vector128.Create(rCandidates[j]), Vector128.Create(gCandidates[j]), Vector128.Create(bCandidates[j]), Vector128.Create(aCandidates[j]));

                    Vector128<float> mask = Sse.CompareLessThan(err, bestErr);
                    bestErr = Select(mask, err, bestErr);
                    bestIdx = Select(mask.AsInt32(), Vector128.Create(j), bestIdx);
                }

                bestErr.StoreUnsafe(ref errRef);
                bestIdx.StoreUnsafe(ref idxRef);

                totalError += errorBuffer[0] + errorBuffer[1] + errorBuffer[2] + errorBuffer[3];
                indices[i + 0] = indexBuffer[0];
                indices[i + 1] = indexBuffer[1];
                indices[i + 2] = indexBuffer[2];
                indices[i + 3] = indexBuffer[3];
            }

            return totalError;
        }

        /// <summary>
        /// Finds the best interpolation index per pixel for a 3-channel float block.
        /// </summary>
        /// <param name="rPix">The source red-channel pixel values.</param>
        /// <param name="gPix">The source green-channel pixel values.</param>
        /// <param name="bPix">The source blue-channel pixel values.</param>
        /// <param name="rCandidates">The candidate red-channel interpolated values.</param>
        /// <param name="gCandidates">The candidate green-channel interpolated values.</param>
        /// <param name="bCandidates">The candidate blue-channel interpolated values.</param>
        /// <param name="indices">The destination span receiving the best candidate index for each pixel.</param>
        /// <returns>The summed squared error across all pixels.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static float FindBestIndices3Channel(ReadOnlySpan<float> rPix, ReadOnlySpan<float> gPix, ReadOnlySpan<float> bPix, ReadOnlySpan<float> rCandidates, ReadOnlySpan<float> gCandidates, ReadOnlySpan<float> bCandidates, Span<int> indices)
        {
            if (!(Sse.IsSupported && Sse2.IsSupported))
                return FindBestIndices3ChannelScalar(rPix, gPix, bPix, rCandidates, gCandidates, bCandidates, indices);

            float totalError = 0f;
            Span<float> errorBuffer = stackalloc float[4];
            Span<int> indexBuffer = stackalloc int[4];
            ref float rRef = ref MemoryMarshal.GetReference(rPix);
            ref float gRef = ref MemoryMarshal.GetReference(gPix);
            ref float bRef = ref MemoryMarshal.GetReference(bPix);
            ref float errRef = ref MemoryMarshal.GetReference(errorBuffer);
            ref int idxRef = ref MemoryMarshal.GetReference(indexBuffer);
            Vector128<float> inf = Vector128.Create(float.MaxValue);

            for (int i = 0; i < rPix.Length; i += 4)
            {
                Vector128<float> vr = Vector128.LoadUnsafe(ref rRef, (nuint)i);
                Vector128<float> vg = Vector128.LoadUnsafe(ref gRef, (nuint)i);
                Vector128<float> vb = Vector128.LoadUnsafe(ref bRef, (nuint)i);

                Vector128<float> bestErr = inf;
                Vector128<int> bestIdx = Vector128<int>.Zero;

                for (int j = 0; j < rCandidates.Length; j++)
                {
                    Vector128<float> err = ComputeSquaredError(vr, vg, vb, Vector128<float>.Zero, Vector128.Create(rCandidates[j]), Vector128.Create(gCandidates[j]), Vector128.Create(bCandidates[j]), Vector128<float>.Zero);

                    Vector128<float> mask = Sse.CompareLessThan(err, bestErr);
                    bestErr = Select(mask, err, bestErr);
                    bestIdx = Select(mask.AsInt32(), Vector128.Create(j), bestIdx);
                }

                bestErr.StoreUnsafe(ref errRef);
                bestIdx.StoreUnsafe(ref idxRef);

                totalError += errorBuffer[0] + errorBuffer[1] + errorBuffer[2] + errorBuffer[3];
                indices[i + 0] = indexBuffer[0];
                indices[i + 1] = indexBuffer[1];
                indices[i + 2] = indexBuffer[2];
                indices[i + 3] = indexBuffer[3];
            }

            return totalError;
        }

        /// <summary>
        /// Finds the best interpolation index per pixel for a 2-subset RGB block.
        /// </summary>
        /// <param name="subsetIndices">The subset index assigned to each source pixel.</param>
        /// <param name="rPix">The source red-channel pixel values.</param>
        /// <param name="gPix">The source green-channel pixel values.</param>
        /// <param name="bPix">The source blue-channel pixel values.</param>
        /// <param name="rCandidates0">The subset-0 red-channel candidate values.</param>
        /// <param name="gCandidates0">The subset-0 green-channel candidate values.</param>
        /// <param name="bCandidates0">The subset-0 blue-channel candidate values.</param>
        /// <param name="rCandidates1">The subset-1 red-channel candidate values.</param>
        /// <param name="gCandidates1">The subset-1 green-channel candidate values.</param>
        /// <param name="bCandidates1">The subset-1 blue-channel candidate values.</param>
        /// <param name="indices">The destination span receiving the best candidate index for each pixel.</param>
        /// <returns>The summed squared error across all pixels.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static float FindBestIndicesPartitioned3Channel(ReadOnlySpan<int> subsetIndices, ReadOnlySpan<int> rPix, ReadOnlySpan<int> gPix, ReadOnlySpan<int> bPix, ReadOnlySpan<float> rCandidates0, ReadOnlySpan<float> gCandidates0, ReadOnlySpan<float> bCandidates0, ReadOnlySpan<float> rCandidates1, ReadOnlySpan<float> gCandidates1, ReadOnlySpan<float> bCandidates1, Span<int> indices)
        {
            if (!(Sse.IsSupported && Sse2.IsSupported))
                return FindBestIndicesPartitioned3ChannelScalar(subsetIndices, rPix, gPix, bPix, rCandidates0, gCandidates0, bCandidates0, rCandidates1, gCandidates1, bCandidates1, indices);

            float totalError = 0f;
            Span<float> errorBuffer = stackalloc float[4];
            Span<int> indexBuffer = stackalloc int[4];
            ref int subsetRef = ref MemoryMarshal.GetReference(subsetIndices);
            ref int rRef = ref MemoryMarshal.GetReference(rPix);
            ref int gRef = ref MemoryMarshal.GetReference(gPix);
            ref int bRef = ref MemoryMarshal.GetReference(bPix);
            ref float errRef = ref MemoryMarshal.GetReference(errorBuffer);
            ref int idxRef = ref MemoryMarshal.GetReference(indexBuffer);
            Vector128<int> subsetOne = Vector128.Create(1);
            Vector128<float> inf = Vector128.Create(float.MaxValue);

            for (int i = 0; i < rPix.Length; i += 4)
            {
                Vector128<int> subset = Vector128.LoadUnsafe(ref subsetRef, (nuint)i);
                Vector128<int> subsetMask = Sse2.CompareEqual(subset, subsetOne);

                Vector128<float> vr = Sse2.ConvertToVector128Single(Vector128.LoadUnsafe(ref rRef, (nuint)i));
                Vector128<float> vg = Sse2.ConvertToVector128Single(Vector128.LoadUnsafe(ref gRef, (nuint)i));
                Vector128<float> vb = Sse2.ConvertToVector128Single(Vector128.LoadUnsafe(ref bRef, (nuint)i));

                Vector128<float> bestErr = inf;
                Vector128<int> bestIdx = Vector128<int>.Zero;

                for (int j = 0; j < rCandidates0.Length; j++)
                {
                    Vector128<float> cr = Select(subsetMask.AsSingle(), Vector128.Create(rCandidates1[j]), Vector128.Create(rCandidates0[j]));
                    Vector128<float> cg = Select(subsetMask.AsSingle(), Vector128.Create(gCandidates1[j]), Vector128.Create(gCandidates0[j]));
                    Vector128<float> cb = Select(subsetMask.AsSingle(), Vector128.Create(bCandidates1[j]), Vector128.Create(bCandidates0[j]));

                    Vector128<float> err = ComputeSquaredError(vr, vg, vb, Vector128<float>.Zero, cr, cg, cb, Vector128<float>.Zero);

                    Vector128<float> mask = Sse.CompareLessThan(err, bestErr);
                    bestErr = Select(mask, err, bestErr);
                    bestIdx = Select(mask.AsInt32(), Vector128.Create(j), bestIdx);
                }

                bestErr.StoreUnsafe(ref errRef);
                bestIdx.StoreUnsafe(ref idxRef);

                totalError += errorBuffer[0] + errorBuffer[1] + errorBuffer[2] + errorBuffer[3];
                indices[i + 0] = indexBuffer[0];
                indices[i + 1] = indexBuffer[1];
                indices[i + 2] = indexBuffer[2];
                indices[i + 3] = indexBuffer[3];
            }

            return totalError;
        }

        /// <summary>
        /// Stores RGB float channels as <see cref="Vector4"/> pixels with alpha fixed to 1.
        /// </summary>
        /// <param name="r">The source red-channel values.</param>
        /// <param name="g">The source green-channel values.</param>
        /// <param name="b">The source blue-channel values.</param>
        /// <param name="pixels">The destination pixel span.</param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void StoreRgbFloatWithAlphaOne(ReadOnlySpan<float> r, ReadOnlySpan<float> g, ReadOnlySpan<float> b, Span<Vector4> pixels)
        {
            int i = 0;

            if (Sse.IsSupported)
            {
                ref float rRef = ref MemoryMarshal.GetReference(r);
                ref float gRef = ref MemoryMarshal.GetReference(g);
                ref float bRef = ref MemoryMarshal.GetReference(b);
                ref Vector4 dstRef = ref MemoryMarshal.GetReference(pixels);
                Vector128<float> alpha = Vector128.Create(1f);

                for (; i + 4 <= pixels.Length; i += 4)
                {
                    Vector128<float> vr = Vector128.LoadUnsafe(ref rRef, (nuint)i);
                    Vector128<float> vg = Vector128.LoadUnsafe(ref gRef, (nuint)i);
                    Vector128<float> vb = Vector128.LoadUnsafe(ref bRef, (nuint)i);

                    Transpose4x4(vr, vg, vb, alpha, out Vector128<float> p0, out Vector128<float> p1, out Vector128<float> p2, out Vector128<float> p3);

                    ref float fBase = ref Unsafe.As<Vector4, float>(ref Unsafe.Add(ref dstRef, i));
                    p0.StoreUnsafe(ref fBase);
                    p1.StoreUnsafe(ref fBase, 4);
                    p2.StoreUnsafe(ref fBase, 8);
                    p3.StoreUnsafe(ref fBase, 12);
                }
            }

            for (; i < pixels.Length; i++)
                pixels[i] = new Vector4(r[i], g[i], b[i], 1f);
        }

        private static float FindBestIndices4ChannelScalar(ReadOnlySpan<int> rPix, ReadOnlySpan<int> gPix, ReadOnlySpan<int> bPix, ReadOnlySpan<int> aPix, ReadOnlySpan<float> rCandidates, ReadOnlySpan<float> gCandidates, ReadOnlySpan<float> bCandidates, ReadOnlySpan<float> aCandidates, Span<int> indices)
        {
            float totalError = 0f;

            for (int i = 0; i < rPix.Length; i++)
            {
                float bestErr = float.MaxValue;
                int bestIdx = 0;

                for (int j = 0; j < rCandidates.Length; j++)
                {
                    float dr = rCandidates[j] - rPix[i];
                    float dg = gCandidates[j] - gPix[i];
                    float db = bCandidates[j] - bPix[i];
                    float da = aCandidates[j] - aPix[i];
                    float err = (dr * dr) + (dg * dg) + (db * db) + (da * da);

                    if (err < bestErr)
                    {
                        bestErr = err;
                        bestIdx = j;
                    }
                }

                totalError += bestErr;
                indices[i] = bestIdx;
            }

            return totalError;
        }

        private static float FindBestIndices3ChannelScalar(ReadOnlySpan<float> rPix, ReadOnlySpan<float> gPix, ReadOnlySpan<float> bPix, ReadOnlySpan<float> rCandidates, ReadOnlySpan<float> gCandidates, ReadOnlySpan<float> bCandidates, Span<int> indices)
        {
            float totalError = 0f;

            for (int i = 0; i < rPix.Length; i++)
            {
                float bestErr = float.MaxValue;
                int bestIdx = 0;

                for (int j = 0; j < rCandidates.Length; j++)
                {
                    float dr = rCandidates[j] - rPix[i];
                    float dg = gCandidates[j] - gPix[i];
                    float db = bCandidates[j] - bPix[i];
                    float err = (dr * dr) + (dg * dg) + (db * db);

                    if (err < bestErr)
                    {
                        bestErr = err;
                        bestIdx = j;
                    }
                }

                totalError += bestErr;
                indices[i] = bestIdx;
            }

            return totalError;
        }

        private static float FindBestIndicesPartitioned3ChannelScalar(ReadOnlySpan<int> subsetIndices, ReadOnlySpan<int> rPix, ReadOnlySpan<int> gPix, ReadOnlySpan<int> bPix, ReadOnlySpan<float> rCandidates0, ReadOnlySpan<float> gCandidates0, ReadOnlySpan<float> bCandidates0, ReadOnlySpan<float> rCandidates1, ReadOnlySpan<float> gCandidates1, ReadOnlySpan<float> bCandidates1, Span<int> indices)
        {
            float totalError = 0f;

            for (int i = 0; i < rPix.Length; i++)
            {
                bool useSubset1 = subsetIndices[i] != 0;
                ReadOnlySpan<float> rCandidates = useSubset1 ? rCandidates1 : rCandidates0;
                ReadOnlySpan<float> gCandidates = useSubset1 ? gCandidates1 : gCandidates0;
                ReadOnlySpan<float> bCandidates = useSubset1 ? bCandidates1 : bCandidates0;

                float bestErr = float.MaxValue;
                int bestIdx = 0;

                for (int j = 0; j < rCandidates.Length; j++)
                {
                    float dr = rCandidates[j] - rPix[i];
                    float dg = gCandidates[j] - gPix[i];
                    float db = bCandidates[j] - bPix[i];
                    float err = (dr * dr) + (dg * dg) + (db * db);

                    if (err < bestErr)
                    {
                        bestErr = err;
                        bestIdx = j;
                    }
                }

                totalError += bestErr;
                indices[i] = bestIdx;
            }

            return totalError;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static Vector128<float> ComputeSquaredError(Vector128<float> vr, Vector128<float> vg, Vector128<float> vb, Vector128<float> va, Vector128<float> cr, Vector128<float> cg, Vector128<float> cb, Vector128<float> ca)
        {
            Vector128<float> dr = Sse.Subtract(cr, vr);
            Vector128<float> dg = Sse.Subtract(cg, vg);
            Vector128<float> db = Sse.Subtract(cb, vb);
            Vector128<float> da = Sse.Subtract(ca, va);

            Vector128<float> err = Sse.Add(Sse.Multiply(dr, dr), Sse.Multiply(dg, dg));
            err = Sse.Add(err, Sse.Multiply(db, db));
            err = Sse.Add(err, Sse.Multiply(da, da));
            return err;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static Vector128<int> QuantizeToByteRange(Vector128<float> value)
        {
            Vector128<float> zero = Vector128<float>.Zero;
            Vector128<float> one = Vector128.Create(1f);
            Vector128<float> scale = Vector128.Create(255f);
            Vector128<float> half = Vector128.Create(0.5f);
            Vector128<float> clamped = Sse.Max(Sse.Min(value, one), zero);
            return Sse2.ConvertToVector128Int32WithTruncation(Sse.Add(Sse.Multiply(clamped, scale), half));
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static Vector128<float> Select(Vector128<float> mask, Vector128<float> whenTrue, Vector128<float> whenFalse) =>
            Sse.Or(Sse.And(mask, whenTrue), Sse.AndNot(mask, whenFalse));

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static Vector128<int> Select(Vector128<int> mask, Vector128<int> whenTrue, Vector128<int> whenFalse) =>
            Sse2.Or(Sse2.And(mask, whenTrue), Sse2.AndNot(mask, whenFalse));

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static void Transpose4x4(Vector128<float> r0, Vector128<float> r1, Vector128<float> r2, Vector128<float> r3, out Vector128<float> c0, out Vector128<float> c1, out Vector128<float> c2, out Vector128<float> c3)
        {
            Vector128<float> t0 = Sse.UnpackLow(r0, r1);
            Vector128<float> t1 = Sse.UnpackHigh(r0, r1);
            Vector128<float> t2 = Sse.UnpackLow(r2, r3);
            Vector128<float> t3 = Sse.UnpackHigh(r2, r3);

            c0 = Sse.Shuffle(t0, t2, 0x44);
            c1 = Sse.Shuffle(t0, t2, 0xEE);
            c2 = Sse.Shuffle(t1, t3, 0x44);
            c3 = Sse.Shuffle(t1, t3, 0xEE);
        }
    }
}
