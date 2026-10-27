using System.Numerics;
using RedFox.Imaging.Processing;

namespace RedFox.Graphics3D.Rendering.Handles;

internal readonly record struct MaterialChannelTransform(Matrix4x4 Matrix, Vector4 Bias)
{
    public static MaterialChannelTransform Identity { get; } = new(Matrix4x4.Identity, Vector4.Zero);

    public static MaterialChannelTransform From(MaterialChannelSource source)
    {
        Matrix4x4	matrix	= default;
        Vector4		bias	= source.Bias;

        for (int component = 0; component < 4; component++)
        {
            ColorChannel	channel	= source.Channels[Math.Min(component, source.Channels.Count - 1)];
            float			scale	= source.Scale[component];

            if (channel == ColorChannel.One)
            {
                bias[component] += scale;
            }
            else if (channel != ColorChannel.Zero)
            {
                matrix[(int)channel, component] = scale;
            }
        }

        return new MaterialChannelTransform(matrix, bias);
    }
}
