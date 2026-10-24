using System.Numerics;
using System.Runtime.InteropServices;
using RedFox.Imaging;
using RedFox.Imaging.Primitives;

namespace RedFox.Graphics3D.Rendering;

internal static class EnvironmentLightExtractor
{
    private const int	MaxGridWidth			= 512;
    private const int	MaxLights				= 4;
    private const float	ThresholdOverMean		= 4.0f;
    private const float	MinimumPowerFraction	= 0.02f;

    private static readonly Vector3 LuminanceWeights = new(0.2126f, 0.7152f, 0.0722f);

    public static EnvironmentLight[] Extract(Image image)
    {
        int			factor		= Math.Max(1, image.Width / MaxGridWidth);
        int			width		= image.Width / factor;
        int			height		= image.Height / factor;
        Vector3[]	radiance	= new Vector3[width * height];

        if (image.Format == ImageFormat.R16G16B16A16Float)
        {
            ReadOnlySpan<Half> pixels = MemoryMarshal.Cast<byte, Half>(image.GetSlice(0, 0, 0).PixelSpan);

            for (int y = 0; y < height * factor; y++)
            {
                for (int x = 0; x < width * factor; x++)
                {
                    int source = (y * image.Width + x) * 4;
                    radiance[(y / factor) * width + (x / factor)] += new Vector3((float)pixels[source], (float)pixels[source + 1], (float)pixels[source + 2]);
                }
            }
        }
        else
        {
            Vector4[] pixels = image.DecodeSlice(0, 0, 0);

            for (int y = 0; y < height * factor; y++)
            {
                for (int x = 0; x < width * factor; x++)
                    radiance[(y / factor) * width + (x / factor)] += pixels[y * image.Width + x].AsVector3();
            }
        }

        float[]		luminance	= new float[radiance.Length];
        float[]		solidAngles	= new float[radiance.Length];
        Vector3[]	directions	= new Vector3[radiance.Length];
        float		totalPower	= 0.0f;
        float		totalAngle	= 0.0f;

        for (int y = 0; y < height; y++)
        {
            float solidAngle = (2.0f * MathF.PI / width) * (MathF.PI / height) * MathF.Sin((y + 0.5f) / height * MathF.PI);

            for (int x = 0; x < width; x++)
            {
                int index = y * width + x;
                radiance[index]		/= factor * factor;
                luminance[index]	= Vector3.Dot(radiance[index], LuminanceWeights);
                solidAngles[index]	= solidAngle;
                directions[index]	= GetDirection((x + 0.5f) / width, (y + 0.5f) / height);
                totalPower			+= luminance[index] * solidAngle;
                totalAngle			+= solidAngle;
            }
        }

        float			threshold	= totalPower / totalAngle * ThresholdOverMean;
        bool[]			visited		= new bool[radiance.Length];
        Stack<int>		pending		= new();
        List<int>		region		= [];
        List<EnvironmentLight>	lights		= [];

        for (int seed = 0; seed < radiance.Length; seed++)
        {
            if (visited[seed] || luminance[seed] <= threshold)
                continue;

            visited[seed] = true;
            pending.Push(seed);
            region.Clear();

            while (pending.TryPop(out int index))
            {
                region.Add(index);
                int x = index % width;
                int y = index / width;

                for (int offsetY = -1; offsetY <= 1; offsetY++)
                {
                    for (int offsetX = -1; offsetX <= 1; offsetX++)
                    {
                        int neighbourY = y + offsetY;
                        if (neighbourY < 0 || neighbourY >= height)
                            continue;

                        int neighbour = neighbourY * width + ((x + offsetX + width) % width);
                        if (visited[neighbour] || luminance[neighbour] <= threshold)
                            continue;

                        visited[neighbour] = true;
                        pending.Push(neighbour);
                    }
                }
            }

            float	power		= 0.0f;
            Vector3	color		= Vector3.Zero;
            Vector3	centroid	= Vector3.Zero;

            foreach (int index in region)
            {
                power		+= luminance[index] * solidAngles[index];
                color		+= radiance[index] * solidAngles[index];
                centroid	+= directions[index] * (luminance[index] * solidAngles[index]);
            }

            if (power < totalPower * MinimumPowerFraction)
                continue;

            Vector3 direction	= Vector3.Normalize(centroid);
            Vector3 up			= MathF.Abs(direction.Y) < 0.999f ? Vector3.UnitY : Vector3.UnitX;
            Vector3 tangent		= Vector3.Normalize(Vector3.Cross(up, direction));
            Vector3 bitangent	= Vector3.Cross(direction, tangent);

            Vector2	mean			= Vector2.Zero;
            float	covarianceXX	= 0.0f;
            float	covarianceXY	= 0.0f;
            float	covarianceYY	= 0.0f;

            foreach (int index in region)
            {
                Vector3 sample = directions[index] / MathF.Max(Vector3.Dot(directions[index], direction), 0.05f);
                mean += new Vector2(Vector3.Dot(sample, tangent), Vector3.Dot(sample, bitangent)) * (luminance[index] * solidAngles[index] / power);
            }

            foreach (int index in region)
            {
                Vector3	sample		= directions[index] / MathF.Max(Vector3.Dot(directions[index], direction), 0.05f);
                Vector2	offset		= new Vector2(Vector3.Dot(sample, tangent), Vector3.Dot(sample, bitangent)) - mean;
                float	weight		= luminance[index] * solidAngles[index] / power;
                float	footprint	= solidAngles[index] / 12.0f;

                covarianceXX	+= (offset.X * offset.X + footprint) * weight;
                covarianceXY	+= offset.X * offset.Y * weight;
                covarianceYY	+= (offset.Y * offset.Y + footprint) * weight;
            }

            float	halfTrace	= (covarianceXX + covarianceYY) * 0.5f;
            float	spread		= MathF.Sqrt(((covarianceXX - covarianceYY) * (covarianceXX - covarianceYY) * 0.25f) + (covarianceXY * covarianceXY));
            float	angle		= 0.5f * MathF.Atan2(2.0f * covarianceXY, covarianceXX - covarianceYY);
            Vector3	majorAxis	= (tangent * MathF.Cos(angle)) + (bitangent * MathF.Sin(angle));

            Vector2 halfAngles = new(MathF.Atan(MathF.Sqrt(3.0f * (halfTrace + spread))), MathF.Atan(MathF.Sqrt(3.0f * MathF.Max(halfTrace - spread, 0.0f))));

            lights.Add(new EnvironmentLight(direction, color, majorAxis, halfAngles));
        }

        lights.Sort((left, right) => Vector3.Dot(right.Power, LuminanceWeights).CompareTo(Vector3.Dot(left.Power, LuminanceWeights)));
        return [.. lights.Take(MaxLights)];
    }

    private static Vector3 GetDirection(float u, float v)
    {
        float phi	= (u - 0.5f) * 2.0f * MathF.PI;
        float theta	= v * MathF.PI;
        return new Vector3(MathF.Sin(theta) * MathF.Cos(phi), MathF.Cos(theta), MathF.Sin(theta) * MathF.Sin(phi));
    }
}
