using System.Buffers.Binary;
using System.Numerics;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Runtime.Intrinsics;
using System.Runtime.Intrinsics.X86;
using RedFox.Graphics2D.Codecs;

namespace RedFox.Graphics2D.Jpeg;

internal sealed class JpegEncoder
{
    private static ReadOnlySpan<byte> BaseLuminanceQuant =>
    [
        16,  11,  10,  16,  24,  40,  51,  61,
        12,  12,  14,  19,  26,  58,  60,  55,
        14,  13,  16,  24,  40,  57,  69,  56,
        14,  17,  22,  29,  51,  87,  80,  62,
        18,  22,  37,  56,  68, 109, 103,  77,
        24,  35,  55,  64,  81, 104, 113,  92,
        49,  64,  78,  87, 103, 121, 120, 101,
        72,  92,  95,  98, 112, 100, 103,  99,
    ];

    private static ReadOnlySpan<byte> BaseChrominanceQuant =>
    [
        17,  18,  24,  47,  99,  99,  99,  99,
        18,  21,  26,  66,  99,  99,  99,  99,
        24,  26,  56,  99,  99,  99,  99,  99,
        47,  66,  99,  99,  99,  99,  99,  99,
        99,  99,  99,  99,  99,  99,  99,  99,
        99,  99,  99,  99,  99,  99,  99,  99,
        99,  99,  99,  99,  99,  99,  99,  99,
        99,  99,  99,  99,  99,  99,  99,  99,
    ];

    private readonly Stream _stream;
    private readonly JpegEncoderOptions _options;
    private readonly int[] _luminanceQuant = new int[64];
    private readonly int[] _chrominanceQuant = new int[64];

    public JpegEncoder(Stream stream, JpegEncoderOptions options)
    {
        _stream = stream;
        _options = options;
        BuildQuantizationTables(options.Quality);
    }

    public void Encode(Image image)
    {
        int width = image.Width;
        int height = image.Height;
        var rgba = ExtractRgba8(image.GetSlice(), image.Format);
        ReadOnlySpan<byte> pixels = rgba;

        // Determine sampling factors
        GetSamplingFactors(
            _options.Subsampling,
            out int yH, out int yV,
            out int cbH, out int cbV,
            out int crH, out int crV);

        int maxH = yH;
        int maxV = yV;

        // Convert RGB to YCbCr planes
        int yWidth = width;
        int yHeight = height;
        int cbWidth = (width * cbH + maxH - 1) / maxH;
        int cbHeight = (height * cbV + maxV - 1) / maxV;

        var yPlane = new byte[yWidth * yHeight];
        var cbPlane = new byte[cbWidth * cbHeight];
        var crPlane = new byte[cbWidth * cbHeight];

        ConvertRgbaToYCbCr(pixels, yPlane, cbPlane, crPlane, width, height, maxH, maxV, cbH, cbV);

        // Write JFIF headers
        WriteSOI();
        WriteAPP0();
        WriteDQT(0, _luminanceQuant);
        WriteDQT(1, _chrominanceQuant);
        WriteSOF0(width, height, yH, yV, cbH, cbV, crH, crV);
        WriteDHT(0, 0, JpegHuffmanEncoder.LuminanceDc);
        WriteDHT(0, 1, JpegHuffmanEncoder.LuminanceAc);
        WriteDHT(1, 0, JpegHuffmanEncoder.ChrominanceDc);
        WriteDHT(1, 1, JpegHuffmanEncoder.ChrominanceAc);
        WriteSOS();

        // Encode scan data
        EncodeScanData(
            yPlane, cbPlane, crPlane,
            width, height,
            yH, yV, cbH, cbV,
            cbWidth, cbHeight);

        WriteEOI();
    }

    private static byte[] ExtractRgba8(in ImageSlice slice, ImageFormat format)
    {
        int width = slice.Width;
        int height = slice.Height;
        int rowBytes = width * 4;
        var rgba = new byte[rowBytes * height];
        var src = slice.PixelSpan;

        if (format is ImageFormat.R8G8B8A8Unorm or ImageFormat.R8G8B8A8UnormSrgb)
        {
            for (int y = 0; y < height; y++)
                src.Slice(y * slice.RowPitch, rowBytes).CopyTo(rgba.AsSpan(y * rowBytes, rowBytes));
            return rgba;
        }

        if (format is ImageFormat.B8G8R8A8Unorm or ImageFormat.B8G8R8A8UnormSrgb or ImageFormat.B8G8R8X8Unorm or ImageFormat.B8G8R8X8UnormSrgb)
        {
            bool forceOpaque = format is ImageFormat.B8G8R8X8Unorm or ImageFormat.B8G8R8X8UnormSrgb;

            for (int y = 0; y < height; y++)
            {
                int srcRow = y * slice.RowPitch;
                int dstRow = y * rowBytes;

                for (int x = 0; x < width; x++)
                {
                    int s = srcRow + x * 4;
                    int d = dstRow + x * 4;
                    rgba[d + 0] = src[s + 2];
                    rgba[d + 1] = src[s + 1];
                    rgba[d + 2] = src[s + 0];
                    rgba[d + 3] = forceOpaque ? (byte)255 : src[s + 3];
                }
            }

            return rgba;
        }

        if (!PixelCodecRegistry.TryGetCodec(format, out var codec) || codec is null)
            throw new NotSupportedException($"JPEG writing is not supported for format {format}.");

        const int stripHeight = 4;
        var pixels = new Vector4[width * stripHeight];

        for (int stripY = 0; stripY < height; stripY += stripHeight)
        {
            int rows = Math.Min(stripHeight, height - stripY);
            codec.DecodeRows(src, pixels, stripY, rows, width, height);

            for (int row = 0; row < rows; row++)
            {
                int pixelBase = row * width;
                int dstRow = (stripY + row) * rowBytes;

                for (int x = 0; x < width; x++)
                {
                    Vector4 pixel = pixels[pixelBase + x];
                    int d = dstRow + x * 4;
                    rgba[d + 0] = (byte)(Math.Clamp(pixel.X, 0f, 1f) * 255f + 0.5f);
                    rgba[d + 1] = (byte)(Math.Clamp(pixel.Y, 0f, 1f) * 255f + 0.5f);
                    rgba[d + 2] = (byte)(Math.Clamp(pixel.Z, 0f, 1f) * 255f + 0.5f);
                    rgba[d + 3] = (byte)(Math.Clamp(pixel.W, 0f, 1f) * 255f + 0.5f);
                }
            }
        }

        return rgba;
    }
    private void BuildQuantizationTables(int quality)
    {
        quality = Math.Clamp(quality, 1, 100);

        int scaleFactor = quality < 50
            ? 5000 / quality
            : 200 - quality * 2;

        ScaleQuantTable(BaseLuminanceQuant, _luminanceQuant, scaleFactor);
        ScaleQuantTable(BaseChrominanceQuant, _chrominanceQuant, scaleFactor);
    }

    private static void ScaleQuantTable(ReadOnlySpan<byte> baseTable, Span<int> output, int scaleFactor)
    {
        for (int i = 0; i < 64; i++)
        {
            int value = (baseTable[i] * scaleFactor + 50) / 100;
            output[i] = Math.Clamp(value, 1, 255);
        }
    }

    private static void ConvertRgbaToYCbCr(
        ReadOnlySpan<byte> rgba,
        Span<byte> yPlane, Span<byte> cbPlane, Span<byte> crPlane,
        int width, int height,
        int maxH, int maxV, int cbH, int cbV)
    {
        int hRatio = maxH / cbH;
        int vRatio = maxV / cbV;
        int cbWidth = (width * cbH + maxH - 1) / maxH;

        if (hRatio == 1 && vRatio == 1)
        {
            ConvertRgbaToYCbCrDirect(rgba, yPlane, cbPlane, crPlane, width * height);
        }
        else
        {
            ConvertRgbaToYCbCrSubsampled(rgba, yPlane, cbPlane, crPlane, width, height, cbWidth, hRatio, vRatio);
        }
    }

    private static void ConvertRgbaToYCbCrDirect(
        ReadOnlySpan<byte> rgba,
        Span<byte> yPlane, Span<byte> cbPlane, Span<byte> crPlane,
        int pixelCount)
    {
        int i = 0;

        if (Avx2.IsSupported)
        {
            i = ConvertRgbaToYCbCrAvx2(rgba, yPlane, cbPlane, crPlane, pixelCount);
        }
        else if (Sse2.IsSupported)
        {
            i = ConvertRgbaToYCbCrSse2(rgba, yPlane, cbPlane, crPlane, pixelCount);
        }

        // Scalar tail
        for (; i < pixelCount; i++)
        {
            int offset = i * 4;
            RgbToYCbCrPixel(rgba[offset], rgba[offset + 1], rgba[offset + 2],
                out yPlane[i], out cbPlane[i], out crPlane[i]);
        }
    }

    private static void ConvertRgbaToYCbCrSubsampled(
        ReadOnlySpan<byte> rgba,
        Span<byte> yPlane, Span<byte> cbPlane, Span<byte> crPlane,
        int width, int height, int cbWidth,
        int hRatio, int vRatio)
    {
        // First pass: compute full-resolution Y
        for (int row = 0; row < height; row++)
        {
            int rowBase = row * width * 4;
            int yBase = row * width;

            for (int col = 0; col < width; col++)
            {
                int off = rowBase + col * 4;
                byte r = rgba[off];
                byte g = rgba[off + 1];
                byte b = rgba[off + 2];
                yPlane[yBase + col] = ClampByte((int)(0.299f * r + 0.587f * g + 0.114f * b));
            }
        }

        // Second pass: downsample chroma by averaging
        int chromaHeight = (height + vRatio - 1) / vRatio;
        int chromaWidth = (width + hRatio - 1) / hRatio;

        for (int cy = 0; cy < chromaHeight; cy++)
        {
            for (int cx = 0; cx < chromaWidth; cx++)
            {
                int sumCb = 0, sumCr = 0, count = 0;

                for (int dy = 0; dy < vRatio; dy++)
                {
                    int py = cy * vRatio + dy;
                    if (py >= height) break;

                    for (int dx = 0; dx < hRatio; dx++)
                    {
                        int px = cx * hRatio + dx;
                        if (px >= width) break;

                        int off = (py * width + px) * 4;
                        byte r = rgba[off];
                        byte g = rgba[off + 1];
                        byte b = rgba[off + 2];

                        sumCb += (int)(-0.168736f * r - 0.331264f * g + 0.5f * b + 128);
                        sumCr += (int)(0.5f * r - 0.418688f * g - 0.081312f * b + 128);
                        count++;
                    }
                }

                int idx = cy * cbWidth + cx;
                cbPlane[idx] = ClampByte(sumCb / count);
                crPlane[idx] = ClampByte(sumCr / count);
            }
        }
    }

    /// <summary>
    /// AVX2 RGBA→YCbCr conversion, 8 pixels at a time.
    /// </summary>
    private static int ConvertRgbaToYCbCrAvx2(
        ReadOnlySpan<byte> rgba,
        Span<byte> yPlane, Span<byte> cbPlane, Span<byte> crPlane,
        int pixelCount)
    {
        int i = 0;

        var v128 = Vector256.Create(128.0f);
        var vR_Y = Vector256.Create(0.299f);
        var vG_Y = Vector256.Create(0.587f);
        var vB_Y = Vector256.Create(0.114f);
        var vR_Cb = Vector256.Create(-0.168736f);
        var vG_Cb = Vector256.Create(-0.331264f);
        var vB_Cb = Vector256.Create(0.5f);
        var vR_Cr = Vector256.Create(0.5f);
        var vG_Cr = Vector256.Create(-0.418688f);
        var vB_Cr = Vector256.Create(-0.081312f);
        var vZero = Vector256.Create(0.0f);
        var v255 = Vector256.Create(255.0f);

        ref byte rgbaRef = ref MemoryMarshal.GetReference(rgba);

        for (; i + 8 <= pixelCount; i += 8)
        {
            int baseOff = i * 4;

            // Load and deinterleave 8 RGBA pixels
            var rF = Vector256.Create(
                (float)Unsafe.Add(ref rgbaRef, baseOff + 0),
                (float)Unsafe.Add(ref rgbaRef, baseOff + 4),
                (float)Unsafe.Add(ref rgbaRef, baseOff + 8),
                (float)Unsafe.Add(ref rgbaRef, baseOff + 12),
                (float)Unsafe.Add(ref rgbaRef, baseOff + 16),
                (float)Unsafe.Add(ref rgbaRef, baseOff + 20),
                (float)Unsafe.Add(ref rgbaRef, baseOff + 24),
                (float)Unsafe.Add(ref rgbaRef, baseOff + 28));

            var gF = Vector256.Create(
                (float)Unsafe.Add(ref rgbaRef, baseOff + 1),
                (float)Unsafe.Add(ref rgbaRef, baseOff + 5),
                (float)Unsafe.Add(ref rgbaRef, baseOff + 9),
                (float)Unsafe.Add(ref rgbaRef, baseOff + 13),
                (float)Unsafe.Add(ref rgbaRef, baseOff + 17),
                (float)Unsafe.Add(ref rgbaRef, baseOff + 21),
                (float)Unsafe.Add(ref rgbaRef, baseOff + 25),
                (float)Unsafe.Add(ref rgbaRef, baseOff + 29));

            var bF = Vector256.Create(
                (float)Unsafe.Add(ref rgbaRef, baseOff + 2),
                (float)Unsafe.Add(ref rgbaRef, baseOff + 6),
                (float)Unsafe.Add(ref rgbaRef, baseOff + 10),
                (float)Unsafe.Add(ref rgbaRef, baseOff + 14),
                (float)Unsafe.Add(ref rgbaRef, baseOff + 18),
                (float)Unsafe.Add(ref rgbaRef, baseOff + 22),
                (float)Unsafe.Add(ref rgbaRef, baseOff + 26),
                (float)Unsafe.Add(ref rgbaRef, baseOff + 30));

            // Y  =  0.299·R + 0.587·G + 0.114·B
            var yF = Avx.Add(Avx.Add(Avx.Multiply(vR_Y, rF), Avx.Multiply(vG_Y, gF)), Avx.Multiply(vB_Y, bF));

            // Cb = -0.169·R - 0.331·G + 0.500·B + 128
            var cbF = Avx.Add(Avx.Add(Avx.Add(Avx.Multiply(vR_Cb, rF), Avx.Multiply(vG_Cb, gF)), Avx.Multiply(vB_Cb, bF)), v128);

            // Cr =  0.500·R - 0.419·G - 0.081·B + 128
            var crF = Avx.Add(Avx.Add(Avx.Add(Avx.Multiply(vR_Cr, rF), Avx.Multiply(vG_Cr, gF)), Avx.Multiply(vB_Cr, bF)), v128);

            // Clamp and convert
            yF = Avx.Min(Avx.Max(yF, vZero), v255);
            cbF = Avx.Min(Avx.Max(cbF, vZero), v255);
            crF = Avx.Min(Avx.Max(crF, vZero), v255);

            var yi = Avx.ConvertToVector256Int32(yF);
            var cbi = Avx.ConvertToVector256Int32(cbF);
            var cri = Avx.ConvertToVector256Int32(crF);

            for (int j = 0; j < 8; j++)
            {
                yPlane[i + j] = (byte)yi.GetElement(j);
                cbPlane[i + j] = (byte)cbi.GetElement(j);
                crPlane[i + j] = (byte)cri.GetElement(j);
            }
        }

        return i;
    }

    private static int ConvertRgbaToYCbCrSse2(
        ReadOnlySpan<byte> rgba,
        Span<byte> yPlane, Span<byte> cbPlane, Span<byte> crPlane,
        int pixelCount)
    {
        int i = 0;

        var v128 = Vector128.Create(128.0f);
        var vR_Y = Vector128.Create(0.299f);
        var vG_Y = Vector128.Create(0.587f);
        var vB_Y = Vector128.Create(0.114f);
        var vR_Cb = Vector128.Create(-0.168736f);
        var vG_Cb = Vector128.Create(-0.331264f);
        var vB_Cb = Vector128.Create(0.5f);
        var vR_Cr = Vector128.Create(0.5f);
        var vG_Cr = Vector128.Create(-0.418688f);
        var vB_Cr = Vector128.Create(-0.081312f);
        var vZero = Vector128.Create(0.0f);
        var v255 = Vector128.Create(255.0f);

        ref byte rgbaRef = ref MemoryMarshal.GetReference(rgba);

        for (; i + 4 <= pixelCount; i += 4)
        {
            int baseOff = i * 4;

            var rF = Vector128.Create(
                (float)Unsafe.Add(ref rgbaRef, baseOff + 0),
                (float)Unsafe.Add(ref rgbaRef, baseOff + 4),
                (float)Unsafe.Add(ref rgbaRef, baseOff + 8),
                (float)Unsafe.Add(ref rgbaRef, baseOff + 12));

            var gF = Vector128.Create(
                (float)Unsafe.Add(ref rgbaRef, baseOff + 1),
                (float)Unsafe.Add(ref rgbaRef, baseOff + 5),
                (float)Unsafe.Add(ref rgbaRef, baseOff + 9),
                (float)Unsafe.Add(ref rgbaRef, baseOff + 13));

            var bF = Vector128.Create(
                (float)Unsafe.Add(ref rgbaRef, baseOff + 2),
                (float)Unsafe.Add(ref rgbaRef, baseOff + 6),
                (float)Unsafe.Add(ref rgbaRef, baseOff + 10),
                (float)Unsafe.Add(ref rgbaRef, baseOff + 14));

            var yF = Sse.Add(Sse.Add(Sse.Multiply(vR_Y, rF), Sse.Multiply(vG_Y, gF)), Sse.Multiply(vB_Y, bF));
            var cbF = Sse.Add(Sse.Add(Sse.Add(Sse.Multiply(vR_Cb, rF), Sse.Multiply(vG_Cb, gF)), Sse.Multiply(vB_Cb, bF)), v128);
            var crF = Sse.Add(Sse.Add(Sse.Add(Sse.Multiply(vR_Cr, rF), Sse.Multiply(vG_Cr, gF)), Sse.Multiply(vB_Cr, bF)), v128);

            yF = Sse.Min(Sse.Max(yF, vZero), v255);
            cbF = Sse.Min(Sse.Max(cbF, vZero), v255);
            crF = Sse.Min(Sse.Max(crF, vZero), v255);

            var yi = Sse2.ConvertToVector128Int32(yF);
            var cbi = Sse2.ConvertToVector128Int32(cbF);
            var cri = Sse2.ConvertToVector128Int32(crF);

            for (int j = 0; j < 4; j++)
            {
                yPlane[i + j] = (byte)yi.GetElement(j);
                cbPlane[i + j] = (byte)cbi.GetElement(j);
                crPlane[i + j] = (byte)cri.GetElement(j);
            }
        }

        return i;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static void RgbToYCbCrPixel(byte r, byte g, byte b, out byte y, out byte cb, out byte cr)
    {
        y = ClampByte((int)(0.299f * r + 0.587f * g + 0.114f * b));
        cb = ClampByte((int)(-0.168736f * r - 0.331264f * g + 0.5f * b + 128));
        cr = ClampByte((int)(0.5f * r - 0.418688f * g - 0.081312f * b + 128));
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static byte ClampByte(int value) => (byte)Math.Clamp(value, 0, 255);

    private static void GetSamplingFactors(
        JpegChromaSubsampling subsampling,
        out int yH, out int yV,
        out int cbH, out int cbV,
        out int crH, out int crV)
    {
        switch (subsampling)
        {
            case JpegChromaSubsampling.Yuv444:
                yH = 1; yV = 1; cbH = 1; cbV = 1; crH = 1; crV = 1;
                break;
            case JpegChromaSubsampling.Yuv422:
                yH = 2; yV = 1; cbH = 1; cbV = 1; crH = 1; crV = 1;
                break;
            default: // Yuv420
                yH = 2; yV = 2; cbH = 1; cbV = 1; crH = 1; crV = 1;
                break;
        }
    }


    private void EncodeScanData(
        ReadOnlySpan<byte> yPlane, ReadOnlySpan<byte> cbPlane, ReadOnlySpan<byte> crPlane,
        int width, int height,
        int yH, int yV, int cbH, int cbV,
        int cbWidth, int cbHeight)
    {
        var writer = new JpegBitWriter(_stream);
        Span<int> block = stackalloc int[64];
        Span<int> zigzag = stackalloc int[64];

        int prevDcY = 0, prevDcCb = 0, prevDcCr = 0;

        // MCU dimensions in pixels
        int mcuPixelW = yH * 8;
        int mcuPixelH = yV * 8;
        int mcuCountX = (width + mcuPixelW - 1) / mcuPixelW;
        int mcuCountY = (height + mcuPixelH - 1) / mcuPixelH;

        for (int mcuY = 0; mcuY < mcuCountY; mcuY++)
        {
            for (int mcuX = 0; mcuX < mcuCountX; mcuX++)
            {
                // Encode Y blocks (yH × yV blocks per MCU)
                for (int bv = 0; bv < yV; bv++)
                {
                    for (int bh = 0; bh < yH; bh++)
                    {
                        int blockX = mcuX * mcuPixelW + bh * 8;
                        int blockY = mcuY * mcuPixelH + bv * 8;

                        ExtractBlock(yPlane, width, height, blockX, blockY, block);
                        LevelShift(block);
                        JpegFdct.Transform(block);
                        Quantize(block, _luminanceQuant);
                        ZigzagReorder(block, zigzag);

                        int dc = zigzag[0];
                        JpegHuffmanEncoder.LuminanceDc.EncodeDc(writer, dc - prevDcY);
                        JpegHuffmanEncoder.LuminanceAc.EncodeAc(writer, zigzag);
                        prevDcY = dc;
                    }
                }

                // Encode Cb block
                {
                    int blockX = mcuX * 8;
                    int blockY = mcuY * 8;

                    ExtractBlock(cbPlane, cbWidth, cbHeight, blockX, blockY, block);
                    LevelShift(block);
                    JpegFdct.Transform(block);
                    Quantize(block, _chrominanceQuant);
                    ZigzagReorder(block, zigzag);

                    int dc = zigzag[0];
                    JpegHuffmanEncoder.ChrominanceDc.EncodeDc(writer, dc - prevDcCb);
                    JpegHuffmanEncoder.ChrominanceAc.EncodeAc(writer, zigzag);
                    prevDcCb = dc;
                }

                // Encode Cr block
                {
                    int blockX = mcuX * 8;
                    int blockY = mcuY * 8;

                    ExtractBlock(crPlane, cbWidth, cbHeight, blockX, blockY, block);
                    LevelShift(block);
                    JpegFdct.Transform(block);
                    Quantize(block, _chrominanceQuant);
                    ZigzagReorder(block, zigzag);

                    int dc = zigzag[0];
                    JpegHuffmanEncoder.ChrominanceDc.EncodeDc(writer, dc - prevDcCr);
                    JpegHuffmanEncoder.ChrominanceAc.EncodeAc(writer, zigzag);
                    prevDcCr = dc;
                }
            }
        }

        writer.Flush();
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static void ExtractBlock(ReadOnlySpan<byte> plane, int planeWidth, int planeHeight, int blockX, int blockY, Span<int> block)
    {
        for (int y = 0; y < 8; y++)
        {
            int py = Math.Min(blockY + y, planeHeight - 1);
            int rowOffset = py * planeWidth;

            for (int x = 0; x < 8; x++)
            {
                int px = Math.Min(blockX + x, planeWidth - 1);
                block[y * 8 + x] = plane[rowOffset + px];
            }
        }
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static void LevelShift(Span<int> block)
    {
        for (int i = 0; i < 64; i++)
            block[i] -= 128;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static void Quantize(Span<int> block, ReadOnlySpan<int> quantTable)
    {
        for (int i = 0; i < 64; i++)
        {
            // Round to nearest (toward zero for negative values)
            int q = quantTable[i];
            int val = block[i];
            block[i] = val >= 0
                ? (val + q / 2) / q
                : -((-val + q / 2) / q);
        }
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static void ZigzagReorder(ReadOnlySpan<int> block, Span<int> zigzag)
    {
        var order = JpegZigZag.Order;
        for (int i = 0; i < 64; i++)
        {
            zigzag[i] = block[order[i]];
        }
    }

    private void WriteMarker(JpegMarker marker)
    {
        _stream.WriteByte(0xFF);
        _stream.WriteByte((byte)marker);
    }

    private void WriteSOI()
    {
        WriteMarker(JpegMarker.SOI);
    }

    private void WriteEOI()
    {
        WriteMarker(JpegMarker.EOI);
    }

    private void WriteAPP0()
    {
        WriteMarker(JpegMarker.APP0);

        Span<byte> data = stackalloc byte[16];
        BinaryPrimitives.WriteUInt16BigEndian(data, 16);       // Length
        data[2] = (byte)'J';                                    // Identifier "JFIF\0"
        data[3] = (byte)'F';
        data[4] = (byte)'I';
        data[5] = (byte)'F';
        data[6] = 0;
        data[7] = 1;                                            // Version major
        data[8] = 1;                                            // Version minor
        data[9] = 0;                                            // Pixel aspect ratio (0 = no units)
        BinaryPrimitives.WriteUInt16BigEndian(data[10..], 1);   // X density
        BinaryPrimitives.WriteUInt16BigEndian(data[12..], 1);   // Y density
        data[14] = 0;                                           // Thumbnail width
        data[15] = 0;                                           // Thumbnail height

        _stream.Write(data);
    }

    private void WriteDQT(int tableId, ReadOnlySpan<int> table)
    {
        WriteMarker(JpegMarker.DQT);

        // Length = 2 (length field) + 1 (Pq/Tq) + 64 (table values) = 67
        Span<byte> header = stackalloc byte[3];
        BinaryPrimitives.WriteUInt16BigEndian(header, 67);
        header[2] = (byte)(tableId & 0x0F); // Pq=0 (8-bit precision), Tq=tableId
        _stream.Write(header);

        Span<byte> values = stackalloc byte[64];
        var order = JpegZigZag.Order;
        for (int i = 0; i < 64; i++)
        {
            values[i] = (byte)table[order[i]];
        }
        _stream.Write(values);
    }

    private void WriteSOF0(int width, int height,
        int yH, int yV, int cbH, int cbV, int crH, int crV)
    {
        WriteMarker(JpegMarker.SOF0);

        // Length = 2 + 1 (precision) + 2 (height) + 2 (width) + 1 (nComponents) + 3*nComponents
        int length = 2 + 1 + 2 + 2 + 1 + 3 * 3;
        Span<byte> data = stackalloc byte[length];

        BinaryPrimitives.WriteUInt16BigEndian(data, (ushort)length);
        data[2] = 8; // Sample precision (8 bits)
        BinaryPrimitives.WriteUInt16BigEndian(data[3..], (ushort)height);
        BinaryPrimitives.WriteUInt16BigEndian(data[5..], (ushort)width);
        data[7] = 3; // Number of components

        // Y component
        data[8] = 1; // Component ID
        data[9] = (byte)((yH << 4) | yV); // Sampling factors
        data[10] = 0; // Quantization table ID

        // Cb component
        data[11] = 2;
        data[12] = (byte)((cbH << 4) | cbV);
        data[13] = 1;

        // Cr component
        data[14] = 3;
        data[15] = (byte)((crH << 4) | crV);
        data[16] = 1;

        _stream.Write(data);
    }

    private void WriteDHT(int tableId, int tableClass, JpegHuffmanEncoder encoder)
    {
        WriteMarker(JpegMarker.DHT);

        // Length = 2 + 1 (Tc/Th) + 16 (BITS) + n (HUFFVAL)
        int length = 2 + 1 + 16 + encoder.Values.Length;
        Span<byte> header = stackalloc byte[3];
        BinaryPrimitives.WriteUInt16BigEndian(header, (ushort)length);
        header[2] = (byte)((tableClass << 4) | (tableId & 0x0F));
        _stream.Write(header);

        _stream.Write(encoder.CodeLengths);
        _stream.Write(encoder.Values);
    }

    private void WriteSOS()
    {
        WriteMarker(JpegMarker.SOS);

        // Length = 2 + 1 (Ns) + 2*Ns + 3 (Ss, Se, AhAl)
        int length = 2 + 1 + 2 * 3 + 3;
        Span<byte> data = stackalloc byte[length];

        BinaryPrimitives.WriteUInt16BigEndian(data, (ushort)length);
        data[2] = 3; // Number of components

        // Y:  DC table 0, AC table 0
        data[3] = 1;    // Component selector
        data[4] = 0x00; // Td=0, Ta=0

        // Cb: DC table 1, AC table 1
        data[5] = 2;
        data[6] = 0x11;

        // Cr: DC table 1, AC table 1
        data[7] = 3;
        data[8] = 0x11;

        data[9] = 0;   // Ss (start of spectral selection)
        data[10] = 63; // Se (end of spectral selection)
        data[11] = 0;  // Ah=0, Al=0

        _stream.Write(data);
    }
}
