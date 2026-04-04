using System.Numerics;
using RedFox.Graphics2D;
using RedFox.Graphics2D.Codecs;
using RedFox.Graphics2D.BlockCompression.Vulkan;

namespace RedFox.Tests.Graphics2D;

public sealed class ImageConverterEngineTests
{
    [Fact]
    public void Convert_WithCustomEngine_UsesEnginePath()
    {
        const int width = 4;
        const int height = 4;

        byte[] sourceBytes = CreateRgba8Source(width, height);
        Image image = new(width, height, ImageFormat.R8G8B8A8Unorm, sourceBytes);
        TestConverterEngine converterEngine = new(result: true);

        image.Convert(ImageFormat.BC7Unorm, ImageConvertFlags.None, converterEngine);

        Assert.Equal(ImageFormat.BC7Unorm, image.Format);
        Assert.Equal(1, converterEngine.CallCount);
    }

    [Fact]
    public void Convert_WithCustomEngineFallback_UsesCpuCodecPath()
    {
        const int width = 4;
        const int height = 4;

        byte[] sourceBytes = CreateRgba8Source(width, height);
        Image image = new(width, height, ImageFormat.R8G8B8A8Unorm, sourceBytes);
        TestConverterEngine converterEngine = new(result: false);

        image.Convert(ImageFormat.BC7Unorm, ImageConvertFlags.None, converterEngine);

        Assert.Equal(ImageFormat.BC7Unorm, image.Format);
        Assert.Equal(1, converterEngine.CallCount);
        Assert.Equal(ImageFormatInfo.CalculatePitch(ImageFormat.BC7Unorm, width, height).SlicePitch, image.PixelData.Length);
    }

    [Fact]
    public void VulkanEngine_WithCpuBridgeDisabled_FallsBackToImageCpuPath()
    {
        const int width = 4;
        const int height = 4;

        byte[] sourceBytes = CreateRgba8Source(width, height);
        Image image = new(width, height, ImageFormat.R8G8B8A8Unorm, sourceBytes);
        using VulkanBcConverterEngine converterEngine = new(new VulkanBcConverterEngineOptions
        {
            AllowCpuBridgeConversion = false,
        });

        image.Convert(ImageFormat.BC7Unorm, ImageConvertFlags.None, converterEngine);

        Assert.Equal(ImageFormat.BC7Unorm, image.Format);
        Assert.Equal(ImageFormatInfo.CalculatePitch(ImageFormat.BC7Unorm, width, height).SlicePitch, image.PixelData.Length);
    }

    private static byte[] CreateRgba8Source(int width, int height)
    {
        Vector4[] sourcePixels = new Vector4[width * height];
        for (int i = 0; i < sourcePixels.Length; i++)
        {
            float t = i / (float)Math.Max(1, sourcePixels.Length - 1);
            sourcePixels[i] = new Vector4(t, 1.0f - t, (0.5f * t) + 0.25f, 1.0f);
        }

        byte[] sourceBytes = new byte[ImageFormatInfo.CalculatePitch(ImageFormat.R8G8B8A8Unorm, width, height).SlicePitch];
        PixelCodec.R8G8B8A8Unorm.Encode(sourcePixels, sourceBytes, width, height);
        return sourceBytes;
    }
}
