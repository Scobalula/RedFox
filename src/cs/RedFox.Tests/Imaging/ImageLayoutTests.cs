using System.Numerics;
using RedFox.Imaging;
using RedFox.Imaging.Codecs;
using RedFox.Imaging.Primitives;

namespace RedFox.Tests.Imaging;

public sealed class ImageLayoutTests
{
    [Fact]
    public void Slices_IdentifyTheirMipArrayAndDepthIndices()
    {
        Image image = new(8, 4, 1, 2, 3, ImageFormat.R8G8B8A8Unorm);

        Assert.Equal(6, image.SliceCount);

        foreach (ImageSlice slice in image.Slices)
        {
            ref readonly ImageSlice resolved = ref image.GetSlice(slice.MipLevel, slice.ArrayIndex, slice.DepthIndex);

            Assert.Equal(Math.Max(1, 8 >> slice.MipLevel), slice.Width);
            Assert.Equal(Math.Max(1, 4 >> slice.MipLevel), slice.Height);
            Assert.Equal(slice.MipLevel, resolved.MipLevel);
            Assert.Equal(slice.ArrayIndex, resolved.ArrayIndex);
            Assert.True(slice.Pixels.Equals(resolved.Pixels));
        }
    }

    [Fact]
    public void VolumeSlices_HalveDepthPerMip()
    {
        Image image = new(4, 4, 4, 1, 3, ImageFormat.R8Unorm);

        Assert.Equal(4 + 2 + 1, image.SliceCount);
        Assert.Equal(1, image.GetSlice(1, 0, 1).DepthIndex);
        Assert.Throws<ArgumentOutOfRangeException>(() => image.GetSlice(1, 0, 2));
        Assert.Equal(image.Info.CalculateTotalByteCount(), image.PixelData.Length);
    }

    [Theory]
    [InlineData(4, 4, 1, 1, 4, false)]
    [InlineData(4, 4, 1, 5, 1, true)]
    [InlineData(4, 4, 2, 6, 1, true)]
    [InlineData(70000, 70000, 1, 1, 1, false)]
    [InlineData(0, 4, 1, 1, 1, false)]
    public void Constructor_RejectsInvalidLayouts(int width, int height, int depth, int arraySize, int mipLevels, bool isCubemap)
    {
        Assert.ThrowsAny<ArgumentException>(() => new Image(width, height, depth, arraySize, mipLevels, ImageFormat.R32G32B32A32Float, isCubemap));
    }

    [Fact]
    public void Constructor_AdoptsOversizedBufferButExposesExactLayoutSize()
    {
        byte[] data = new byte[100];
        Image image = new(2, 2, ImageFormat.R8G8B8A8Unorm, data);

        image.PixelData[0] = 42;

        Assert.Equal(16, image.PixelData.Length);
        Assert.Equal(42, data[0]);
        Assert.Throws<ArgumentException>(() => new Image(4, 4, ImageFormat.R8G8B8A8Unorm, new byte[10]));
    }

    [Fact]
    public void GetSlice_RejectsNegativeIndices()
    {
        Image image = new(4, 4, ImageFormat.R8G8B8A8Unorm);

        Assert.Throws<ArgumentOutOfRangeException>(() => image.GetSlice(-1, 0, 0));
        Assert.Throws<ArgumentOutOfRangeException>(() => image.GetSlice(0, -1, 0));
    }

    [Fact]
    public void GetPixelRowSpan_ReturnsTypedRowAndRejectsMismatchedPixelTypes()
    {
        Image image = ImageTranslatorTestHarness.CreatePatternImage(3, 2, includeTransparency: true);
        ImageSlice slice = image.GetSlice();

        Span<uint> row = slice.GetPixelRowSpan<uint>(1);

        Assert.Equal(3, row.Length);
        Assert.Equal(BitConverter.ToUInt32(image.PixelData.Slice(12, 4)), row[0]);
        Assert.Throws<InvalidOperationException>(() => slice.GetPixelRowSpan<ushort>(0));
        Assert.Throws<ArgumentOutOfRangeException>(() => slice.GetRowSpan(2));
        Assert.Throws<InvalidOperationException>(() => slice.GetBlockRowSpan(0));
    }

    [Fact]
    public void BlockCompressedSlices_ExposeBlockRowsOnly()
    {
        Image image = new(8, 8, ImageFormat.BC1Unorm);
        ImageSlice slice = image.GetSlice();

        Assert.True(slice.IsBlockCompressed);
        Assert.Equal(16, slice.GetBlockRowSpan(1).Length);
        Assert.Throws<ArgumentOutOfRangeException>(() => slice.GetBlockRowSpan(2));
        Assert.Throws<InvalidOperationException>(() => slice.GetRowSpan(0));
        Assert.Throws<InvalidOperationException>(() => slice.GetPixelRowSpan<uint>(0));
    }

    [Fact]
    public void GetPixel_RejectsOutOfBoundsCoordinates()
    {
        Image image = new(2, 2, ImageFormat.R8G8B8A8Unorm);

        Assert.Throws<ArgumentOutOfRangeException>(() => image.GetSlice().GetPixel(2, 0));
        Assert.Throws<ArgumentOutOfRangeException>(() => image.GetSlice().GetPixel(0, -1));
    }

    [Fact]
    public void Convert_WhenEngineDeclines_ProducesCpuResult()
    {
        Image expected = ImageTranslatorTestHarness.CreatePatternImage(4, 4, includeTransparency: true);
        Image actual = ImageTranslatorTestHarness.CreatePatternImage(4, 4, includeTransparency: true);

        expected.Convert(ImageFormat.B8G8R8A8Unorm);
        actual.Convert(ImageFormat.B8G8R8A8Unorm, new TestConverterEngine(result: false));

        Assert.Equal(expected.PixelData.ToArray(), actual.PixelData.ToArray());
    }

    [Fact]
    public void EncodeSlice_RoundTripsThroughCodec()
    {
        Image image = new(2, 1, ImageFormat.R32G32B32A32Float);
        Vector4[] pixels = [new(0.25f, 0.5f, 0.75f, 1f), new(1f, 0f, 0f, 0.5f)];

        image.EncodeSlice(0, 0, 0, pixels);

        Assert.Equal(pixels, image.DecodeSlice());
        Assert.Throws<ArgumentException>(() => image.EncodeSlice(0, 0, 0, new Vector4[3]));
    }

    [Fact]
    public void ImageInfo_Validate_DistinguishesMalformedFromUnsupported()
    {
        Assert.Throws<InvalidDataException>(() => new ImageInfo(65535, 65535, ImageFormat.R32G32B32A32Float).Validate());
        Assert.Throws<InvalidDataException>(() => new ImageInfo(4, 4, 1, 1, 10, ImageFormat.R8Unorm, false).Validate());
        Assert.Throws<NotSupportedException>(() => new ImageInfo(4, 4, ImageFormat.NV12).Validate());
        Assert.Equal(13, ImageInfo.GetMaxMipLevels(4096, 16, 1));
    }

    [Fact]
    public void PixelCodecs_ResolveTheSameImmutableInstanceForEveryBlockFormat()
    {
        ImageFormat[] formats = [ImageFormat.BC1Unorm, ImageFormat.BC2Unorm, ImageFormat.BC3Unorm, ImageFormat.BC4Unorm, ImageFormat.BC5Snorm, ImageFormat.BC6HUF16, ImageFormat.BC7UnormSrgb];

        foreach (ImageFormat format in formats)
        {
            IPixelCodec codec = PixelCodecs.GetCodec(format);

            Assert.Same(codec, PixelCodecs.GetCodec(format));
            Assert.Equal(format, codec.Format);
            Assert.Equal(0, codec.BytesPerPixel);
        }

        Assert.Throws<NotSupportedException>(() => PixelCodecs.GetCodec(ImageFormat.NV12));
    }
}
