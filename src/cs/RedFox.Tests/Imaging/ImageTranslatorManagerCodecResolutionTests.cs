using System.Numerics;
using RedFox.Imaging;
using RedFox.Imaging.Primitives;
using RedFox.Imaging.BlockCompression;
using RedFox.Imaging.Codecs;
using RedFox.Imaging.Formats.Exr;
using RedFox.Imaging.IO;
using RedFox.Imaging.Formats.Png;

namespace RedFox.Tests.Imaging;

public sealed class ImageTranslatorManagerCodecResolutionTests
{
    [Fact]
    public void WritePng_WithBc7Image_UsesPixelCodecSwitchResolution()
    {
        Image bc7Image = CreateBlockCompressedImage(
            ImageTranslatorTestHarness.CreatePatternImage(8, 8, includeTransparency: true),
            ImageFormat.BC7Unorm);

        ImageTranslatorManager manager = new();
        manager.Register(new PngImageTranslator());

        byte[] pngData = ImageTranslatorTestHarness.WriteImageWithManager(manager, bc7Image, "pattern.png");
        Image decoded = ImageTranslatorTestHarness.ReadImageWithManager(manager, pngData, "pattern.png");

        Assert.Equal(8, decoded.Width);
        Assert.Equal(8, decoded.Height);
        Assert.Equal(ImageFormat.R8G8B8A8Unorm, decoded.Format);
    }

    [Fact]
    public void WriteExr_WithBc6HImage_UsesPixelCodecSwitchResolution()
    {
        Image bc6Image = CreateBlockCompressedImage(
            ImageTranslatorTestHarness.CreateFloatPatternImage(8, 8),
            ImageFormat.BC6HUF16);

        ImageTranslatorManager manager = new();
        manager.Register(new ExrImageTranslator());

        byte[] exrData = ImageTranslatorTestHarness.WriteImageWithManager(manager, bc6Image, "pattern.exr");
        Image decoded = ImageTranslatorTestHarness.ReadImageWithManager(manager, exrData, "pattern.exr");

        Assert.Equal(8, decoded.Width);
        Assert.Equal(8, decoded.Height);
        Assert.Equal(ImageFormat.R32G32B32A32Float, decoded.Format);
    }

    private static Image CreateBlockCompressedImage(Image source, ImageFormat targetFormat)
    {
        ArgumentNullException.ThrowIfNull(source);

        ref readonly ImageSlice sourceSlice = ref source.GetSlice();
        Vector4[] pixels = source.DecodeSlice();
        int compressedSize = ImageFormatInfo.CalculatePitch(targetFormat, sourceSlice.Width, sourceSlice.Height).SlicePitch;
        byte[] encoded = new byte[compressedSize];

        IPixelCodec codec = PixelCodec.GetCodec(targetFormat);
        codec.Encode(pixels, encoded, sourceSlice.Width, sourceSlice.Height);

        return new Image(sourceSlice.Width, sourceSlice.Height, targetFormat, encoded);
    }
}
