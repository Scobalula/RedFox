using System.Numerics;
using RedFox.Graphics2D;
using RedFox.Graphics2D.Codecs;
using RedFox.Graphics2D.BlockCompression.Vulkan;

namespace RedFox.Tests.Graphics2D;

public sealed class VulkanBcConverterEngineGpuTests
{
    [Theory]
    [InlineData(ImageFormat.R8G8B8A8Unorm, 0.005f)]
    [InlineData(ImageFormat.R16G16B16A16Float, 0.002f)]
    [InlineData(ImageFormat.R32G32B32A32Float, 0.0005f)]
    public void TryConvert_DecodeBc7_WhenAvailable_MatchesCpuPath(ImageFormat targetFormat, float tolerance)
    {
        const int width = 8;
        const int height = 8;

        using VulkanBcConverterEngine engine = new(new VulkanBcConverterEngineOptions
        {
            AllowCpuBridgeConversion = false,
        });

        if (!engine.IsAvailable)
            return;

        byte[] rgbaSource = CreateRgba8Source(width, height);
        Image bcImage = new(width, height, ImageFormat.R8G8B8A8Unorm, (byte[])rgbaSource.Clone());
        bcImage.Convert(ImageFormat.BC7Unorm);

        byte[] bcBytes = bcImage.PixelMemory.ToArray();
        byte[] gpuOutput = new byte[ImageFormatInfo.CalculatePitch(targetFormat, width, height).SlicePitch];

        if (!engine.TryConvert(bcBytes, ImageFormat.BC7Unorm, gpuOutput, targetFormat, width, height, ImageConvertFlags.None))
            return;

        Image gpuImage = new(width, height, targetFormat, gpuOutput);
        Image cpuImage = new(width, height, ImageFormat.BC7Unorm, (byte[])bcBytes.Clone());
        cpuImage.Convert(targetFormat);

        AssertPixelsClose(cpuImage.DecodeSlice(), gpuImage.DecodeSlice(), tolerance);
    }

    [Fact]
    public void TryConvert_EncodeBc7_WhenAvailable_RoundTripsReasonably()
    {
        const int width = 8;
        const int height = 8;

        using VulkanBcConverterEngine engine = new(new VulkanBcConverterEngineOptions
        {
            AllowCpuBridgeConversion = false,
        });

        if (!engine.IsAvailable)
            return;

        byte[] rgbaSource = CreateRgba8Source(width, height);
        byte[] bcOutput = new byte[ImageFormatInfo.CalculatePitch(ImageFormat.BC7Unorm, width, height).SlicePitch];
        if (!engine.TryConvert(rgbaSource, ImageFormat.R8G8B8A8Unorm, bcOutput, ImageFormat.BC7Unorm, width, height, ImageConvertFlags.None))
            return;

        byte[] roundTrip = new byte[rgbaSource.Length];
        if (!engine.TryConvert(bcOutput, ImageFormat.BC7Unorm, roundTrip, ImageFormat.R8G8B8A8Unorm, width, height, ImageConvertFlags.None))
            return;

        Image sourceImage = new(width, height, ImageFormat.R8G8B8A8Unorm, rgbaSource);
        Image roundTripImage = new(width, height, ImageFormat.R8G8B8A8Unorm, roundTrip);

        AssertRoundTripError(sourceImage.DecodeSlice(), roundTripImage.DecodeSlice(), maxPerChannelError: 0.2f, averagePerChannelError: 0.06f);
    }

    [Fact]
    public void TryConvert_EncodeBc6H_WhenAvailable_RoundTripsReasonably()
    {
        const int width = 8;
        const int height = 8;

        using VulkanBcConverterEngine engine = new(new VulkanBcConverterEngineOptions
        {
            AllowCpuBridgeConversion = false,
        });

        if (!engine.IsAvailable)
            return;

        byte[] hdrSource = CreateHdrSource(width, height);
        byte[] bcOutput = new byte[ImageFormatInfo.CalculatePitch(ImageFormat.BC6HUF16, width, height).SlicePitch];
        if (!engine.TryConvert(hdrSource, ImageFormat.R16G16B16A16Float, bcOutput, ImageFormat.BC6HUF16, width, height, ImageConvertFlags.None))
            return;

        byte[] roundTrip = new byte[ImageFormatInfo.CalculatePitch(ImageFormat.R32G32B32A32Float, width, height).SlicePitch];
        if (!engine.TryConvert(bcOutput, ImageFormat.BC6HUF16, roundTrip, ImageFormat.R32G32B32A32Float, width, height, ImageConvertFlags.None))
            return;

        Image sourceImage = new(width, height, ImageFormat.R16G16B16A16Float, hdrSource);
        Image roundTripImage = new(width, height, ImageFormat.R32G32B32A32Float, roundTrip);

        AssertRoundTripError(sourceImage.DecodeSlice(), roundTripImage.DecodeSlice(), maxPerChannelError: 0.55f, averagePerChannelError: 0.15f);
    }

    private static byte[] CreateRgba8Source(int width, int height)
    {
        Vector4[] sourcePixels = new Vector4[width * height];
        for (int i = 0; i < sourcePixels.Length; i++)
        {
            float t = i / (float)Math.Max(1, sourcePixels.Length - 1);
            float x = (i % width) / (float)Math.Max(1, width - 1);
            float y = (i / width) / (float)Math.Max(1, height - 1);
            sourcePixels[i] = new Vector4(t, 1.0f - x, (0.6f * y) + 0.2f, 1.0f);
        }

        byte[] sourceBytes = new byte[ImageFormatInfo.CalculatePitch(ImageFormat.R8G8B8A8Unorm, width, height).SlicePitch];
        PixelCodec.R8G8B8A8Unorm.Encode(sourcePixels, sourceBytes, width, height);
        return sourceBytes;
    }

    private static byte[] CreateHdrSource(int width, int height)
    {
        Vector4[] sourcePixels = new Vector4[width * height];
        for (int i = 0; i < sourcePixels.Length; i++)
        {
            float x = (i % width) / (float)Math.Max(1, width - 1);
            float y = (i / width) / (float)Math.Max(1, height - 1);
            sourcePixels[i] = new Vector4(
                0.5f + (x * 3.0f),
                0.25f + (y * 2.5f),
                0.75f + ((x + y) * 1.5f),
                1.0f);
        }

        byte[] sourceBytes = new byte[ImageFormatInfo.CalculatePitch(ImageFormat.R16G16B16A16Float, width, height).SlicePitch];
        PixelCodec.R16G16B16A16Float.Encode(sourcePixels, sourceBytes, width, height);
        return sourceBytes;
    }

    private static void AssertPixelsClose(ReadOnlySpan<Vector4> expected, ReadOnlySpan<Vector4> actual, float tolerance)
    {
        Assert.Equal(expected.Length, actual.Length);

        for (int i = 0; i < expected.Length; i++)
        {
            Vector4 delta = Vector4.Abs(expected[i] - actual[i]);
            Assert.True(delta.X <= tolerance, $"Pixel {i} red differed by {delta.X}.");
            Assert.True(delta.Y <= tolerance, $"Pixel {i} green differed by {delta.Y}.");
            Assert.True(delta.Z <= tolerance, $"Pixel {i} blue differed by {delta.Z}.");
            Assert.True(delta.W <= tolerance, $"Pixel {i} alpha differed by {delta.W}.");
        }
    }

    private static void AssertRoundTripError(ReadOnlySpan<Vector4> expected, ReadOnlySpan<Vector4> actual, float maxPerChannelError, float averagePerChannelError)
    {
        Assert.Equal(expected.Length, actual.Length);

        float maxError = 0.0f;
        float sumError = 0.0f;
        int channelCount = expected.Length * 4;

        for (int i = 0; i < expected.Length; i++)
        {
            Vector4 delta = Vector4.Abs(expected[i] - actual[i]);
            maxError = Math.Max(maxError, Math.Max(Math.Max(delta.X, delta.Y), Math.Max(delta.Z, delta.W)));
            sumError += delta.X + delta.Y + delta.Z + delta.W;
        }

        Assert.True(maxError <= maxPerChannelError, $"Maximum per-channel error {maxError} exceeded {maxPerChannelError}.");
        Assert.True((sumError / channelCount) <= averagePerChannelError, $"Average per-channel error {(sumError / channelCount)} exceeded {averagePerChannelError}.");
    }
}
