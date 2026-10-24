using System.Numerics;
using System.Runtime.InteropServices;
using RedFox.Imaging;
using RedFox.Imaging.Primitives;

namespace RedFox.Graphics3D;

// Builds the split-sum prefiltered environment: mip N holds the panorama convolved with a GGX lobe of roughness
// N / (levels - 1), so the shader picks a blur level straight from roughness. Box-filtered mips look blocky once
// magnified, so each level is integrated with filtered importance sampling (Krivanek and Colbert) from a float pyramid.
internal static class EnvironmentPrefilter
{
    private const int SourceWidth = 1024;
    private const int SampleCount = 64;

    public static Image Prefilter(Image panorama, int levels)
    {
        Image result = new(panorama.Width, panorama.Height, 1, 1, levels, ImageFormat.R16G16B16A16Float);
        panorama.GetSlice().PixelSpan.CopyTo(result.GetSlice(0, 0, 0).PixelSpan);

        Image source = panorama;
        while (source.Width > SourceWidth)
            source = Halve(source);

        List<(Vector4[] Pixels, int Width, int Height)> pyramid = BuildPyramid(source);

        for (int level = 1; level < levels; level++)
        {
            float roughness = level / (float)(levels - 1);
            float alpha = roughness * roughness;
            int width = Math.Max(1, panorama.Width >> level);
            int height = Math.Max(1, panorama.Height >> level);
            Vector4[] pixels = new Vector4[width * height];

            Parallel.For(0, height, y =>
            {
                for (int x = 0; x < width; x++)
                {
                    Vector3 direction = GetDirection((x + 0.5f) / width, (y + 0.5f) / height);
                    pixels[y * width + x] = new Vector4(Convolve(pyramid, direction, alpha), 1.0f);
                }
            });

            result.EncodeSlice(level, 0, 0, pixels);
        }

        return result;
    }

    public static Image Halve(Image source)
    {
        int width = source.Width / 2;
        int height = source.Height / 2;
        Image result = new(width, height, ImageFormat.R16G16B16A16Float);

        ReadOnlySpan<Half>	sourcePixels	= MemoryMarshal.Cast<byte, Half>(source.GetSlice().PixelSpan);
        Span<Half>			resultPixels	= MemoryMarshal.Cast<byte, Half>(result.PixelData);

        for (int y = 0; y < height; y++)
        {
            for (int x = 0; x < width; x++)
            {
                int top		= ((y * 2) * source.Width + (x * 2)) * 4;
                int bottom	= top + (source.Width * 4);

                for (int channel = 0; channel < 4; channel++)
                {
                    float sum = (float)sourcePixels[top + channel] + (float)sourcePixels[top + 4 + channel] + (float)sourcePixels[bottom + channel] + (float)sourcePixels[bottom + 4 + channel];
                    resultPixels[((y * width + x) * 4) + channel] = (Half)(sum * 0.25f);
                }
            }
        }

        return result;
    }

    private static List<(Vector4[] Pixels, int Width, int Height)> BuildPyramid(Image source)
    {
        List<(Vector4[] Pixels, int Width, int Height)> pyramid = [(source.DecodeSlice(0, 0, 0), source.Width, source.Height)];

        while (pyramid[^1].Width > 4)
        {
            (Vector4[] previous, int previousWidth, _) = pyramid[^1];
            int width = previousWidth / 2;
            int height = Math.Max(1, pyramid[^1].Height / 2);
            Vector4[] pixels = new Vector4[width * height];

            for (int y = 0; y < height; y++)
            {
                for (int x = 0; x < width; x++)
                {
                    int top = (y * 2) * previousWidth + (x * 2);
                    pixels[y * width + x] = (previous[top] + previous[top + 1] + previous[top + previousWidth] + previous[top + previousWidth + 1]) * 0.25f;
                }
            }

            pyramid.Add((pixels, width, height));
        }

        return pyramid;
    }

    private static Vector3 Convolve(List<(Vector4[] Pixels, int Width, int Height)> pyramid, Vector3 normal, float alpha)
    {
        Vector3 up = MathF.Abs(normal.Y) < 0.999f ? Vector3.UnitY : Vector3.UnitX;
        Vector3 tangent = Vector3.Normalize(Vector3.Cross(up, normal));
        Vector3 bitangent = Vector3.Cross(normal, tangent);

        float alpha2 = alpha * alpha;
        float texelSolidAngle = (2.0f * MathF.PI / pyramid[0].Width) * (MathF.PI / pyramid[0].Height);
        Vector3 sum = Vector3.Zero;
        float weight = 0.0f;

        // The split-sum approximation assumes the view and normal match the reflection direction.
        for (int i = 0; i < SampleCount; i++)
        {
            float phi = 2.0f * MathF.PI * i / SampleCount;
            float xi = RadicalInverse((uint)i);
            float cosTheta = MathF.Sqrt((1.0f - xi) / (1.0f + (alpha2 - 1.0f) * xi));
            float sinTheta = MathF.Sqrt(MathF.Max(1.0f - cosTheta * cosTheta, 0.0f));

            Vector3 half = (tangent * (sinTheta * MathF.Cos(phi))) + (bitangent * (sinTheta * MathF.Sin(phi))) + (normal * cosTheta);
            Vector3 light = (2.0f * Vector3.Dot(normal, half) * half) - normal;
            float nDotL = Vector3.Dot(normal, light);

            if (nDotL <= 0.0f)
                continue;

            float denominator = cosTheta * cosTheta * (alpha2 - 1.0f) + 1.0f;
            float pdf = alpha2 / (MathF.PI * denominator * denominator) * 0.25f;
            float sampleSolidAngle = 1.0f / (SampleCount * pdf + 0.0001f);
            float sourceSolidAngle = texelSolidAngle * MathF.Max(MathF.Sqrt(MathF.Max(1.0f - light.Y * light.Y, 0.0f)), 0.001f);
            float lod = MathF.Max(0.5f * MathF.Log2(sampleSolidAngle / sourceSolidAngle) + 1.0f, 0.0f);

            sum += Sample(pyramid, light, lod) * nDotL;
            weight += nDotL;
        }

        return weight > 0.0f ? sum / weight : Sample(pyramid, normal, 0.0f);
    }

    private static Vector3 Sample(List<(Vector4[] Pixels, int Width, int Height)> pyramid, Vector3 direction, float lod)
    {
        float u = MathF.Atan2(direction.Z, direction.X) / (2.0f * MathF.PI) + 0.5f;
        float v = MathF.Acos(Math.Clamp(direction.Y, -1.0f, 1.0f)) / MathF.PI;
        float level = Math.Min(lod, pyramid.Count - 1);
        int lower = (int)level;
        int upper = Math.Min(lower + 1, pyramid.Count - 1);

        return Vector3.Lerp(SampleBilinear(pyramid[lower], u, v), SampleBilinear(pyramid[upper], u, v), level - lower);
    }

    private static Vector3 SampleBilinear((Vector4[] Pixels, int Width, int Height) level, float u, float v)
    {
        float x = u * level.Width - 0.5f;
        float y = Math.Clamp(v * level.Height - 0.5f, 0.0f, level.Height - 1);
        int x0 = (int)MathF.Floor(x);
        int y0 = (int)y;
        int y1 = Math.Min(y0 + 1, level.Height - 1);
        float fx = x - x0;
        float fy = y - y0;

        // Longitude wraps around the panorama while latitude clamps at the poles.
        int left = ((x0 % level.Width) + level.Width) % level.Width;
        int right = (left + 1) % level.Width;

        Vector4 top = Vector4.Lerp(level.Pixels[y0 * level.Width + left], level.Pixels[y0 * level.Width + right], fx);
        Vector4 bottom = Vector4.Lerp(level.Pixels[y1 * level.Width + left], level.Pixels[y1 * level.Width + right], fx);
        Vector4 color = Vector4.Lerp(top, bottom, fy);

        return new Vector3(color.X, color.Y, color.Z);
    }

    private static Vector3 GetDirection(float u, float v)
    {
        float phi = (u - 0.5f) * 2.0f * MathF.PI;
        float theta = v * MathF.PI;
        return new Vector3(MathF.Sin(theta) * MathF.Cos(phi), MathF.Cos(theta), MathF.Sin(theta) * MathF.Sin(phi));
    }

    private static float RadicalInverse(uint bits)
    {
        bits = (bits << 16) | (bits >> 16);
        bits = ((bits & 0x55555555u) << 1) | ((bits & 0xAAAAAAAAu) >> 1);
        bits = ((bits & 0x33333333u) << 2) | ((bits & 0xCCCCCCCCu) >> 2);
        bits = ((bits & 0x0F0F0F0Fu) << 4) | ((bits & 0xF0F0F0F0u) >> 4);
        bits = ((bits & 0x00FF00FFu) << 8) | ((bits & 0xFF00FF00u) >> 8);
        return bits * 2.3283064365386963e-10f;
    }
}
