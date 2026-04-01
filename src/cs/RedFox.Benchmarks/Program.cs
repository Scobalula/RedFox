using System.Diagnostics;
using System.Numerics;
using System.Runtime.Intrinsics.X86;
using RedFox.Graphics2D;
using RedFox.Graphics2D.BC;

const int width = 256;
const int height = 256;

Console.WriteLine("RedFox BC Benchmarks");
Console.WriteLine($".NET: {Environment.Version}");
Console.WriteLine($"CPU threads: {Environment.ProcessorCount}");
Console.WriteLine($"SSE2: {Sse2.IsSupported}, SSE: {Sse.IsSupported}, AVX2: {Avx2.IsSupported}");
Console.WriteLine($"Image size: {width}x{height}");
Console.WriteLine();

Vector4[] ldrPixels = CreateLdrPattern(width, height);
Vector4[] hdrPixels = CreateHdrPattern(width, height);

BC7Codec bc7 = new(ImageFormat.BC7Unorm);
BC6HCodec bc6h = new(ImageFormat.BC6HSF16);

byte[] bc7Compressed = new byte[ImageFormatInfo.CalculatePitch(ImageFormat.BC7Unorm, width, height).SlicePitch];
byte[] bc6hCompressed = new byte[ImageFormatInfo.CalculatePitch(ImageFormat.BC6HSF16, width, height).SlicePitch];
Vector4[] decodeBuffer = new Vector4[width * height];

bc7.Encode(ldrPixels, bc7Compressed, width, height);
bc6h.Encode(hdrPixels, bc6hCompressed, width, height);

Measure("BC7 Encode", width, height, iterations: 12, () => bc7.Encode(ldrPixels, bc7Compressed, width, height));
Measure("BC7 EncodeFast", width, height, iterations: 20, () => bc7.EncodeFast(ldrPixels, bc7Compressed, width, height));
Measure("BC7 Decode", width, height, iterations: 80, () => bc7.Decode(bc7Compressed, decodeBuffer, width, height));

Console.WriteLine();

Measure("BC6H Encode", width, height, iterations: 10, () => bc6h.Encode(hdrPixels, bc6hCompressed, width, height));
Measure("BC6H EncodeFast", width, height, iterations: 18, () => bc6h.EncodeFast(hdrPixels, bc6hCompressed, width, height));
Measure("BC6H Decode", width, height, iterations: 70, () => bc6h.Decode(bc6hCompressed, decodeBuffer, width, height));

static void Measure(string name, int width, int height, int iterations, Action action)
{
    action();
    GC.Collect();
    GC.WaitForPendingFinalizers();
    GC.Collect();

    Stopwatch stopwatch = Stopwatch.StartNew();
    for (int i = 0; i < iterations; i++)
        action();
    stopwatch.Stop();

    double elapsedMs = stopwatch.Elapsed.TotalMilliseconds;
    double mpixPerSec = (width * height * (double)iterations) / stopwatch.Elapsed.TotalSeconds / 1_000_000.0;
    Console.WriteLine($"{name,-16} {elapsedMs,10:F2} ms total  {elapsedMs / iterations,8:F2} ms/iter  {mpixPerSec,8:F2} MPix/s");
}

static Vector4[] CreateLdrPattern(int width, int height)
{
    Vector4[] pixels = new Vector4[width * height];

    for (int y = 0; y < height; y++)
    {
        for (int x = 0; x < width; x++)
        {
            float fx = x / (float)Math.Max(1, width - 1);
            float fy = y / (float)Math.Max(1, height - 1);
            float wave = 0.5f + (0.5f * MathF.Sin((fx * 9.5f) + (fy * 13.0f)));
            float stripe = ((x / 7) % 2 == 0) ? 0.18f : 0.82f;
            float alpha = ((x + y) % 19 == 0) ? 0.65f : 1.0f;

            pixels[(y * width) + x] = new Vector4(
                Math.Clamp((fx * 0.65f) + (wave * 0.35f), 0f, 1f),
                Math.Clamp((fy * 0.55f) + (stripe * 0.45f), 0f, 1f),
                Math.Clamp((wave * 0.60f) + (fy * 0.30f), 0f, 1f),
                alpha);
        }
    }

    return pixels;
}

static Vector4[] CreateHdrPattern(int width, int height)
{
    Vector4[] pixels = new Vector4[width * height];

    for (int y = 0; y < height; y++)
    {
        for (int x = 0; x < width; x++)
        {
            float fx = x / (float)Math.Max(1, width - 1);
            float fy = y / (float)Math.Max(1, height - 1);
            float wave = MathF.Sin((fx * 11.0f) - (fy * 7.0f));
            float ridge = MathF.Cos((fx * 5.0f) + (fy * 3.5f));

            pixels[(y * width) + x] = new Vector4(
                (-0.9f + (fx * 2.6f)) + (wave * 0.45f),
                (-0.6f + (fy * 2.1f)) + (ridge * 0.35f),
                (-0.3f + ((fx + fy) * 1.5f)) + ((wave - ridge) * 0.25f),
                1f);
        }
    }

    return pixels;
}
