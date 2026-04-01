using System.Numerics;
using RedFox.Graphics2D;
using RedFox.Graphics2D.BC;
using RedFox.Graphics2D.Codecs;

namespace RedFox.Tests.Graphics2D;

public sealed class BcCodecTests
{
    [Fact]
    public void PixelCodec_GetCodec_ResolvesBc6hAndBc7Codecs()
    {
        IPixelCodec bc6UnsignedCodec = PixelCodec.GetCodec(ImageFormat.BC6HUF16);
        IPixelCodec bc6SignedCodec = PixelCodec.GetCodec(ImageFormat.BC6HSF16);
        IPixelCodec bc7Codec = PixelCodec.GetCodec(ImageFormat.BC7Unorm);

        Assert.IsType<BC6HCodec>(bc6UnsignedCodec);
        Assert.IsType<BC6HCodec>(bc6SignedCodec);
        Assert.IsType<BC7Codec>(bc7Codec);
    }

    [Fact]
    public void Bc7Codec_ImageEncodeDecode_RoundTripsNonMultipleOfFourDimensionsWithinTolerance()
    {
        const int width = 6;
        const int height = 5;

        Vector4[] sourcePixels = CreateOpaqueColorPattern(width, height);
        int compressedSize = ImageFormatInfo.CalculatePitch(ImageFormat.BC7Unorm, width, height).SlicePitch;
        byte[] compressed = new byte[compressedSize];
        Vector4[] decodedPixels = new Vector4[width * height];

        BC7Codec codec = new(ImageFormat.BC7Unorm);
        codec.Encode(sourcePixels, compressed, width, height);
        codec.Decode(compressed, decodedPixels, width, height);

        AssertBlockErrorWithinTolerance(sourcePixels, decodedPixels, maxAbsoluteError: 0.38f, maxAverageAbsoluteError: 0.10f);
    }

    [Fact]
    public void Bc6HCodec_BlockEncodeDecode_UnsignedHdrGradientStaysWithinTolerance()
    {
        Vector4[] sourcePixels = CreateUnsignedHdrGradient();
        byte[] compressed = new byte[16];
        Vector4[] decodedPixels = new Vector4[16];

        BC6HCodec.EncodeBlock(sourcePixels, compressed, signed: false);
        BC6HCodec.DecodeBlock(compressed, decodedPixels, signed: false);

        Assert.All(decodedPixels, pixel => Assert.Equal(1.0f, pixel.W, 5));
        AssertBlockErrorWithinTolerance(sourcePixels, decodedPixels, maxAbsoluteError: 0.75f, maxAverageAbsoluteError: 0.22f);
    }

    [Fact]
    public void Bc6HCodec_BlockEncodeDecode_SignedHdrGradientPreservesNegativeAndPositiveValues()
    {
        Vector4[] sourcePixels = CreateSignedHdrGradient();
        byte[] compressed = new byte[16];
        Vector4[] decodedPixels = new Vector4[16];

        BC6HCodec.EncodeBlock(sourcePixels, compressed, signed: true);
        BC6HCodec.DecodeBlock(compressed, decodedPixels, signed: true);

        Assert.True(decodedPixels[0].X < 0f, "Expected the first decoded pixel to preserve a negative X component.");
        Assert.True(decodedPixels[^1].X > 0f, "Expected the last decoded pixel to preserve a positive X component.");
        AssertBlockErrorWithinTolerance(sourcePixels, decodedPixels, maxAbsoluteError: 0.80f, maxAverageAbsoluteError: 0.24f);
    }

    [Fact]
    public void BcPartitionTables_ArePubliclyAccessible()
    {
        Assert.Equal(32, BC6HPartitionTable.Partitions2.Length);
        Assert.Equal(32, BC6HPartitionTable.AnchorTable.Length);
        Assert.Equal(64, BC7PartitionTable.Partitions2.Length);
        Assert.Equal(64, BC7PartitionTable.Partitions3.Length);
        Assert.Equal(16, BC7PartitionTable.Weights4.Length);
    }

    [Fact]
    public void ImageConvert_WithFastBc7Flag_ProducesBc7Output()
    {
        const int width = 6;
        const int height = 5;

        Vector4[] sourcePixels = CreateOpaqueColorPattern(width, height);
        byte[] sourceBytes = new byte[ImageFormatInfo.CalculatePitch(ImageFormat.R8G8B8A8Unorm, width, height).SlicePitch];
        PixelCodec.R8G8B8A8Unorm.Encode(sourcePixels, sourceBytes, width, height);

        Image image = new(width, height, ImageFormat.R8G8B8A8Unorm, sourceBytes);
        image.Convert(ImageFormat.BC7Unorm, ImageConvertFlags.PreferFastBc7Encoding);

        Assert.Equal(ImageFormat.BC7Unorm, image.Format);
        Assert.Equal(ImageFormatInfo.CalculatePitch(ImageFormat.BC7Unorm, width, height).SlicePitch, image.PixelData.Length);

        Vector4[] decodedPixels = new Vector4[width * height];
        PixelCodec.Bc7Unorm.Decode(image.PixelData, decodedPixels, width, height);
        AssertBlockErrorWithinTolerance(sourcePixels, decodedPixels, maxAbsoluteError: 0.42f, maxAverageAbsoluteError: 0.12f);
    }

    [Fact]
    public void ImageConvert_WithFastBc6HFlag_ProducesBc6HOutput()
    {
        const int width = 4;
        const int height = 4;

        Vector4[] sourcePixels = CreateSignedHdrGradient();
        byte[] sourceBytes = new byte[ImageFormatInfo.CalculatePitch(ImageFormat.R16G16B16A16Float, width, height).SlicePitch];
        PixelCodec.R16G16B16A16Float.Encode(sourcePixels, sourceBytes, width, height);

        Image image = new(width, height, ImageFormat.R16G16B16A16Float, sourceBytes);
        image.Convert(ImageFormat.BC6HSF16, ImageConvertFlags.PreferFastBc6HEncoding);

        Assert.Equal(ImageFormat.BC6HSF16, image.Format);
        Assert.Equal(ImageFormatInfo.CalculatePitch(ImageFormat.BC6HSF16, width, height).SlicePitch, image.PixelData.Length);

        Vector4[] decodedPixels = new Vector4[width * height];
        PixelCodec.Bc6HSf16.Decode(image.PixelData, decodedPixels, width, height);

        Assert.True(decodedPixels[0].X < 0f, "Expected fast BC6H conversion to preserve negative values.");
        Assert.True(decodedPixels[^1].X > 0f, "Expected fast BC6H conversion to preserve positive values.");
        AssertBlockErrorWithinTolerance(sourcePixels, decodedPixels, maxAbsoluteError: 0.90f, maxAverageAbsoluteError: 0.30f);
    }

    private static Vector4[] CreateOpaqueColorPattern(int width, int height)
    {
        Vector4[] pixels = new Vector4[width * height];

        for (int y = 0; y < height; y++)
        {
            for (int x = 0; x < width; x++)
            {
                float red = (x + 1) / (float)(width + 1);
                float green = (y + 1) / (float)(height + 1);
                float blue = ((x * 3) + (y * 5)) % 11 / 10.0f;
                pixels[(y * width) + x] = new Vector4(red, green, blue, 1.0f);
            }
        }

        return pixels;
    }

    private static Vector4[] CreateUnsignedHdrGradient()
    {
        Vector4[] pixels = new Vector4[16];

        for (int index = 0; index < pixels.Length; index++)
        {
            float t = index / 15.0f;
            pixels[index] = new Vector4(
                0.5f + (1.75f * t),
                1.25f + (0.80f * t),
                2.0f + (0.60f * t),
                1.0f);
        }

        return pixels;
    }

    private static Vector4[] CreateSignedHdrGradient()
    {
        Vector4[] pixels = new Vector4[16];

        for (int index = 0; index < pixels.Length; index++)
        {
            float t = index / 15.0f;
            pixels[index] = new Vector4(
                -0.75f + (1.5f * t),
                -0.40f + (0.9f * t),
                -0.20f + (0.7f * t),
                1.0f);
        }

        return pixels;
    }

    private static void AssertBlockErrorWithinTolerance(
        ReadOnlySpan<Vector4> expected,
        ReadOnlySpan<Vector4> actual,
        float maxAbsoluteError,
        float maxAverageAbsoluteError)
    {
        float worstError = 0f;
        float totalError = 0f;
        int componentCount = expected.Length * 4;

        for (int index = 0; index < expected.Length; index++)
        {
            Vector4 difference = Vector4.Abs(expected[index] - actual[index]);
            worstError = Math.Max(worstError, Math.Max(Math.Max(difference.X, difference.Y), Math.Max(difference.Z, difference.W)));
            totalError += difference.X + difference.Y + difference.Z + difference.W;
        }

        float averageError = totalError / componentCount;
        Assert.InRange(worstError, 0f, maxAbsoluteError);
        Assert.InRange(averageError, 0f, maxAverageAbsoluteError);
    }
}
