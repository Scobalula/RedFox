using System.Numerics;
using RedFox.Graphics2D;
using RedFox.Graphics2D.BC;
using RedFox.Graphics2D.IO;
using RedFox.Graphics2D.Ktx;

namespace RedFox.Tests.Graphics2D;

public sealed class KtxImageTranslatorTests
{
    [Fact]
    public void KtxTranslator_RoundTripViaManager_PreservesRgbaImageData()
    {
        Image source = ImageTranslatorTestHarness.CreatePatternImage(width: 5, height: 3, includeTransparency: true);
        ImageTranslatorManager manager = ImageTranslatorTestHarness.CreateManager(new KtxImageTranslator());

        byte[] encoded = ImageTranslatorTestHarness.WriteImageWithManager(manager, source, "texture.ktx");
        Image decoded = ImageTranslatorTestHarness.ReadImageWithManager(manager, encoded, "texture.ktx");

        AssertImageMatches(source, decoded);
    }

    [Fact]
    public void KtxTranslator_RoundTripViaManager_PreservesMipAndArrayOrdering()
    {
        Image source = new(width: 4, height: 4, depth: 1, arraySize: 2, mipLevels: 2, format: ImageFormat.R8G8B8A8Unorm, isCubemap: false);

        FillSlice(source.GetSlice(mipLevel: 0, arrayIndex: 0), seed: 0x10);
        FillSlice(source.GetSlice(mipLevel: 1, arrayIndex: 0), seed: 0x20);
        FillSlice(source.GetSlice(mipLevel: 0, arrayIndex: 1), seed: 0x30);
        FillSlice(source.GetSlice(mipLevel: 1, arrayIndex: 1), seed: 0x40);

        ImageTranslatorManager manager = ImageTranslatorTestHarness.CreateManager(new KtxImageTranslator());
        byte[] encoded = ImageTranslatorTestHarness.WriteImageWithManager(manager, source, "layers.ktx");
        Image decoded = ImageTranslatorTestHarness.ReadImageWithManager(manager, encoded, "layers.ktx");

        AssertImageMatches(source, decoded);
    }

    [Fact]
    public void KtxTranslator_RoundTripViaManager_PreservesCubemapFaceOrdering()
    {
        Image source = new(width: 4, height: 4, depth: 1, arraySize: 6, mipLevels: 1, format: ImageFormat.R8G8B8A8Unorm, isCubemap: true);

        for (int face = 0; face < 6; face++)
            FillSlice(source.GetSlice(mipLevel: 0, arrayIndex: face), seed: (byte)(0x10 + (face * 0x10)));

        ImageTranslatorManager manager = ImageTranslatorTestHarness.CreateManager(new KtxImageTranslator());
        byte[] encoded = ImageTranslatorTestHarness.WriteImageWithManager(manager, source, "cubemap.ktx");
        Image decoded = ImageTranslatorTestHarness.ReadImageWithManager(manager, encoded, "cubemap.ktx");

        AssertImageMatches(source, decoded);
    }

    [Fact]
    public void KtxTranslator_RoundTripViaManager_PreservesBc7CompressedPayload()
    {
        Vector4[] sourcePixels =
        [
            new(0.1f, 0.2f, 0.3f, 1f), new(0.9f, 0.2f, 0.1f, 1f), new(0.3f, 0.8f, 0.4f, 1f), new(0.7f, 0.6f, 0.2f, 1f),
            new(0.2f, 0.4f, 0.7f, 1f), new(0.5f, 0.5f, 0.5f, 1f), new(0.8f, 0.3f, 0.7f, 1f), new(0.4f, 0.7f, 0.8f, 1f),
            new(0.6f, 0.1f, 0.4f, 1f), new(0.3f, 0.9f, 0.2f, 1f), new(0.2f, 0.3f, 0.9f, 1f), new(0.8f, 0.7f, 0.1f, 1f),
            new(0.4f, 0.2f, 0.6f, 1f), new(0.7f, 0.4f, 0.3f, 1f), new(0.1f, 0.8f, 0.5f, 1f), new(0.9f, 0.9f, 0.9f, 1f),
        ];

        byte[] compressedPixels = new byte[ImageFormatInfo.CalculatePitch(ImageFormat.BC7Unorm, 4, 4).SlicePitch];
        BC7Codec codec = new(ImageFormat.BC7Unorm);
        codec.Encode(sourcePixels, compressedPixels, 4, 4);

        Image source = new(4, 4, ImageFormat.BC7Unorm, compressedPixels);
        ImageTranslatorManager manager = ImageTranslatorTestHarness.CreateManager(new KtxImageTranslator());
        byte[] encoded = ImageTranslatorTestHarness.WriteImageWithManager(manager, source, "compressed.ktx");
        Image decoded = ImageTranslatorTestHarness.ReadImageWithManager(manager, encoded, "compressed.ktx");

        AssertImageMatches(source, decoded);
    }

    [Fact]
    public void KtxImageTranslator_IsValidRecognizesKtxHeader()
    {
        KtxImageTranslator translator = new();
        byte[] validHeader =
        [
            0xAB, 0x4B, 0x54, 0x58, 0x20, 0x31, 0x31, 0xBB,
            0x0D, 0x0A, 0x1A, 0x0A,
        ];
        byte[] invalidHeader = Enumerable.Repeat((byte)0xFF, validHeader.Length).ToArray();

        Assert.True(translator.IsValid(validHeader, "texture.ktx", ".ktx"));
        Assert.False(translator.IsValid(invalidHeader, "texture.ktx", ".ktx"));
    }

    private static void FillSlice(in ImageSlice slice, byte seed)
    {
        Span<byte> pixelSpan = slice.PixelSpan;

        for (int index = 0; index < pixelSpan.Length; index += 4)
        {
            pixelSpan[index + 0] = (byte)(seed + index);
            pixelSpan[index + 1] = (byte)(seed + index + 1);
            pixelSpan[index + 2] = (byte)(seed + index + 2);
            pixelSpan[index + 3] = 255;
        }
    }

    private static void AssertImageMatches(Image expected, Image actual)
    {
        Assert.Equal(expected.Width, actual.Width);
        Assert.Equal(expected.Height, actual.Height);
        Assert.Equal(expected.Depth, actual.Depth);
        Assert.Equal(expected.ArraySize, actual.ArraySize);
        Assert.Equal(expected.MipLevels, actual.MipLevels);
        Assert.Equal(expected.Format, actual.Format);
        Assert.Equal(expected.IsCubemap, actual.IsCubemap);
        Assert.Equal(expected.PixelData.ToArray(), actual.PixelData.ToArray());
    }
}
