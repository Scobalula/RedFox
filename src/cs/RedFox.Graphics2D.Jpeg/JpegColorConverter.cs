using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Runtime.Intrinsics;
using System.Runtime.Intrinsics.X86;

namespace RedFox.Graphics2D.Jpeg;

internal static class JpegColorConverter
{
    public static void YCbCrToRgba(
        ReadOnlySpan<byte> y, ReadOnlySpan<byte> cb, ReadOnlySpan<byte> cr,
        Span<byte> output,
        int width, int height,
        int cbWidth,
        int maxHSample, int maxVSample,
        int compHSample, int compVSample)
    {
        int hRatio = maxHSample / compHSample;
        int vRatio = maxVSample / compVSample;

        if (hRatio == 1 && vRatio == 1)
        {
            // No subsampling — fast path
            YCbCrToRgbaNoSubsampling(y, cb, cr, output, width, height);
        }
        else
        {
            YCbCrToRgbaSubsampled(y, cb, cr, output, width, height, cbWidth, hRatio, vRatio);
        }
    }

    public static void GrayscaleToRgba(ReadOnlySpan<byte> gray, Span<byte> output, int pixelCount)
    {
        // Scalar path — simple and correct for all platforms
        for (int i = 0; i < pixelCount; i++)
        {
            int offset = i * 4;
            byte val = gray[i];
            output[offset + 0] = val;
            output[offset + 1] = val;
            output[offset + 2] = val;
            output[offset + 3] = 255;
        }
    }

    private static void YCbCrToRgbaNoSubsampling(
        ReadOnlySpan<byte> y, ReadOnlySpan<byte> cb, ReadOnlySpan<byte> cr,
        Span<byte> output, int width, int height)
    {
        int pixelCount = width * height;
        int i = 0;

        if (Avx2.IsSupported)
        {
            i = YCbCrToRgbaAvx2(y, cb, cr, output, pixelCount);
        }
        else if (Sse2.IsSupported)
        {
            i = YCbCrToRgbaSse2(y, cb, cr, output, pixelCount);
        }

        // Scalar tail
        for (; i < pixelCount; i++)
        {
            YCbCrToRgbaPixel(y[i], cb[i], cr[i], output, i * 4);
        }
    }

    private static void YCbCrToRgbaSubsampled(
        ReadOnlySpan<byte> y, ReadOnlySpan<byte> cb, ReadOnlySpan<byte> cr,
        Span<byte> output, int width, int height,
        int cbWidth, int hRatio, int vRatio)
    {
        for (int row = 0; row < height; row++)
        {
            int chromaRow = row / vRatio;
            int yRowOffset = row * width;
            int chromaRowOffset = chromaRow * cbWidth;
            int outRowOffset = row * width * 4;

            for (int col = 0; col < width; col++)
            {
                int chromaCol = col / hRatio;
                int chromaIdx = chromaRowOffset + chromaCol;
                int yIdx = yRowOffset + col;

                YCbCrToRgbaPixel(y[yIdx], cb[chromaIdx], cr[chromaIdx], output, outRowOffset + col * 4);
            }
        }
    }

    private static int YCbCrToRgbaAvx2(
        ReadOnlySpan<byte> yPlane, ReadOnlySpan<byte> cbPlane, ReadOnlySpan<byte> crPlane,
        Span<byte> output, int pixelCount)
    {
        int i = 0;

        var v128 = Vector256.Create(128.0f);
        var v1_402 = Vector256.Create(1.402f);
        var v0_344 = Vector256.Create(0.344136f);
        var v0_714 = Vector256.Create(0.714136f);
        var v1_772 = Vector256.Create(1.772f);
        var vZero = Vector256.Create(0.0f);
        var v255 = Vector256.Create(255.0f);

        ref byte yRef = ref MemoryMarshal.GetReference(yPlane);
        ref byte cbRef = ref MemoryMarshal.GetReference(cbPlane);
        ref byte crRef = ref MemoryMarshal.GetReference(crPlane);
        ref byte outRef = ref MemoryMarshal.GetReference(output);

        for (; i + 8 <= pixelCount; i += 8)
        {
            // Load 8 bytes and widen to float
            var yF = WidenToFloat8(ref yRef, i);
            var cbF = Avx.Subtract(WidenToFloat8(ref cbRef, i), v128);
            var crF = Avx.Subtract(WidenToFloat8(ref crRef, i), v128);

            // R = Y + 1.402 * Cr
            var r = Avx.Add(yF, Avx.Multiply(v1_402, crF));
            // G = Y - 0.344 * Cb - 0.714 * Cr
            var g = Avx.Subtract(Avx.Subtract(yF, Avx.Multiply(v0_344, cbF)), Avx.Multiply(v0_714, crF));
            // B = Y + 1.772 * Cb
            var b = Avx.Add(yF, Avx.Multiply(v1_772, cbF));

            // Clamp to [0, 255]
            r = Avx.Min(Avx.Max(r, vZero), v255);
            g = Avx.Min(Avx.Max(g, vZero), v255);
            b = Avx.Min(Avx.Max(b, vZero), v255);

            // Convert to int and store as RGBA bytes
            var ri = Avx.ConvertToVector256Int32(r);
            var gi = Avx.ConvertToVector256Int32(g);
            var bi = Avx.ConvertToVector256Int32(b);

            // Pack to bytes and interleave
            int outOff = i * 4;
            for (int j = 0; j < 8; j++)
            {
                int idx = outOff + j * 4;
                Unsafe.Add(ref outRef, idx + 0) = (byte)ri.GetElement(j);
                Unsafe.Add(ref outRef, idx + 1) = (byte)gi.GetElement(j);
                Unsafe.Add(ref outRef, idx + 2) = (byte)bi.GetElement(j);
                Unsafe.Add(ref outRef, idx + 3) = 255;
            }
        }

        return i;
    }

    private static int YCbCrToRgbaSse2(
        ReadOnlySpan<byte> yPlane, ReadOnlySpan<byte> cbPlane, ReadOnlySpan<byte> crPlane,
        Span<byte> output, int pixelCount)
    {
        int i = 0;

        var v128 = Vector128.Create(128.0f);
        var v1_402 = Vector128.Create(1.402f);
        var v0_344 = Vector128.Create(0.344136f);
        var v0_714 = Vector128.Create(0.714136f);
        var v1_772 = Vector128.Create(1.772f);
        var vZero = Vector128.Create(0.0f);
        var v255 = Vector128.Create(255.0f);

        ref byte yRef = ref MemoryMarshal.GetReference(yPlane);
        ref byte cbRef = ref MemoryMarshal.GetReference(cbPlane);
        ref byte crRef = ref MemoryMarshal.GetReference(crPlane);
        ref byte outRef = ref MemoryMarshal.GetReference(output);

        for (; i + 4 <= pixelCount; i += 4)
        {
            var yF = WidenToFloat4(ref yRef, i);
            var cbF = Sse.Subtract(WidenToFloat4(ref cbRef, i), v128);
            var crF = Sse.Subtract(WidenToFloat4(ref crRef, i), v128);

            var r = Sse.Add(yF, Sse.Multiply(v1_402, crF));
            var g = Sse.Subtract(Sse.Subtract(yF, Sse.Multiply(v0_344, cbF)), Sse.Multiply(v0_714, crF));
            var b = Sse.Add(yF, Sse.Multiply(v1_772, cbF));

            r = Sse.Min(Sse.Max(r, vZero), v255);
            g = Sse.Min(Sse.Max(g, vZero), v255);
            b = Sse.Min(Sse.Max(b, vZero), v255);

            var ri = Sse2.ConvertToVector128Int32(r);
            var gi = Sse2.ConvertToVector128Int32(g);
            var bi = Sse2.ConvertToVector128Int32(b);

            int outOff = i * 4;
            for (int j = 0; j < 4; j++)
            {
                int idx = outOff + j * 4;
                Unsafe.Add(ref outRef, idx + 0) = (byte)ri.GetElement(j);
                Unsafe.Add(ref outRef, idx + 1) = (byte)gi.GetElement(j);
                Unsafe.Add(ref outRef, idx + 2) = (byte)bi.GetElement(j);
                Unsafe.Add(ref outRef, idx + 3) = 255;
            }
        }

        return i;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static Vector256<float> WidenToFloat8(ref byte baseRef, int offset)
    {
        // Load 8 bytes into lower 64 bits, zero-extend to 32-bit ints, then convert to float
        ref byte ptr = ref Unsafe.Add(ref baseRef, offset);

        var ints = Vector256.Create(
            ptr,
            Unsafe.Add(ref ptr, 1),
            Unsafe.Add(ref ptr, 2),
            Unsafe.Add(ref ptr, 3),
            Unsafe.Add(ref ptr, 4),
            Unsafe.Add(ref ptr, 5),
            Unsafe.Add(ref ptr, 6),
            Unsafe.Add(ref ptr, 7));

        return Avx.ConvertToVector256Single(ints);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static Vector128<float> WidenToFloat4(ref byte baseRef, int offset)
    {
        ref byte ptr = ref Unsafe.Add(ref baseRef, offset);

        var ints = Vector128.Create(
            ptr,
            Unsafe.Add(ref ptr, 1),
            Unsafe.Add(ref ptr, 2),
            Unsafe.Add(ref ptr, 3));

        return Sse2.ConvertToVector128Single(ints);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static void YCbCrToRgbaPixel(byte yVal, byte cbVal, byte crVal, Span<byte> output, int offset)
    {
        float yf = yVal;
        float cb = cbVal - 128.0f;
        float cr = crVal - 128.0f;

        int r = (int)(yf + 1.402f * cr);
        int g = (int)(yf - 0.344136f * cb - 0.714136f * cr);
        int b = (int)(yf + 1.772f * cb);

        output[offset + 0] = ClampByte(r);
        output[offset + 1] = ClampByte(g);
        output[offset + 2] = ClampByte(b);
        output[offset + 3] = 255;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static byte ClampByte(int value) => (byte)Math.Clamp(value, 0, 255);
}
