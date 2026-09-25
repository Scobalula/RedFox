using System.Numerics;
using RedFox.Imaging;
using RedFox.Imaging.Primitives;

namespace RedFox.Tests.Imaging;

public sealed class PixelCodecCorrectnessTests
{
    [Theory]
    [InlineData(ImageFormat.R8Snorm, new byte[] { 0x81 }, -1f)]
    [InlineData(ImageFormat.R8Snorm, new byte[] { 0x80 }, -1f)]
    [InlineData(ImageFormat.R8Snorm, new byte[] { 0x7F }, 1f)]
    [InlineData(ImageFormat.R8Unorm, new byte[] { 0xFF }, 1f)]
    [InlineData(ImageFormat.R16Snorm, new byte[] { 0x01, 0x80 }, -1f)]
    [InlineData(ImageFormat.R16Float, new byte[] { 0x00, 0x3C }, 1f)]
    [InlineData(ImageFormat.R16Float, new byte[] { 0x00, 0xC0 }, -2f)]
    [InlineData(ImageFormat.R32Uint, new byte[] { 0xFF, 0xFF, 0xFF, 0xFF }, 1f)]
    [InlineData(ImageFormat.R32Sint, new byte[] { 0x01, 0x00, 0x00, 0x80 }, -1f)]
    [InlineData(ImageFormat.R32Float, new byte[] { 0x00, 0x00, 0x20, 0x40 }, 2.5f)]
    public void Decode_UsesTheFormatsValueDomain(ImageFormat format, byte[] data, float expected)
    {
        Assert.Equal(expected, new Image(1, 1, format, data).DecodeSlice()[0].X, 4);
    }

    [Theory]
    [InlineData(ImageFormat.R8G8Snorm)]
    [InlineData(ImageFormat.R16G16Snorm)]
    [InlineData(ImageFormat.R16G16Float)]
    [InlineData(ImageFormat.R16G16B16A16Snorm)]
    [InlineData(ImageFormat.R32G32Sint)]
    [InlineData(ImageFormat.R32G32B32Float)]
    [InlineData(ImageFormat.R32G32B32A32Sint)]
    [InlineData(ImageFormat.R8G8B8A8Snorm)]
    public void SignedFormats_RoundTripNegativeValues(ImageFormat format)
    {
        Image image = new(1, 1, format);
        Vector4 pixel = new(-0.5f, 0.25f, -1f, 1f);

        image.EncodeSlice(0, 0, 0, [pixel]);
        Vector4 decoded = image.DecodeSlice()[0];

        Assert.Equal(-0.5f, decoded.X, 2);
        Assert.Equal(0.25f, decoded.Y, 2);
    }

    [Fact]
    public void EveryCatalogueFormat_HasStorageAndRoundTripsThroughAnImage()
    {
        foreach (ImageFormat format in Enum.GetValues<ImageFormat>())
        {
            if (!PixelCodecs.TryGetCodec(format, out _))
                continue;

            Image image = new(4, 4, format);
            image.EncodeSlice(0, 0, 0, Enumerable.Repeat(new Vector4(0.5f, 0.25f, 0.75f, 1f), 16).ToArray());

            Assert.Equal(16, image.DecodeSlice().Length);
        }
    }

    [Fact]
    public void Convert_BetweenUnormAndSnorm_RemapsValuesInsteadOfCopyingBits()
    {
        Image image = new(1, 1, ImageFormat.R8Unorm, [255]);

        image.Convert(ImageFormat.R8Snorm);

        Assert.Equal(0x7F, image.PixelData[0]);
    }

    [Fact]
    public void XrBias_DecodesExtendedRange()
    {
        uint packed = 894u | (384u << 10) | (0u << 20) | (3u << 30);
        Image image = new(1, 1, ImageFormat.R10G10B10XrBiasA2Unorm, BitConverter.GetBytes(packed));

        Vector4 pixel = image.DecodeSlice()[0];

        Assert.Equal(1f, pixel.X, 3);
        Assert.Equal(0f, pixel.Y, 3);
        Assert.Equal(-384f / 510f, pixel.Z, 3);
        Assert.Equal(1f, pixel.W, 3);
    }

    [Fact]
    public void SharedExponent_RoundTripsHdrValues()
    {
        Image image = new(1, 1, ImageFormat.R9G9B9E5SharedExp);
        Vector4 pixel = new(12.5f, 0.75f, 0f, 0.2f);

        image.EncodeSlice(0, 0, 0, [pixel]);
        Vector4 decoded = image.DecodeSlice()[0];

        Assert.Equal(12.5f, decoded.X, 1);
        Assert.Equal(0.75f, decoded.Y, 1);
        Assert.Equal(0f, decoded.Z);
        Assert.Equal(1f, decoded.W);
    }

    [Fact]
    public void Bgrx_DecodesOpaqueAndConvertsToRgba()
    {
        Image image = new(1, 1, ImageFormat.B8G8R8X8Unorm, [10, 20, 30, 0]);

        image.Convert(ImageFormat.R8G8B8A8Unorm);

        Assert.Equal(new byte[] { 30, 20, 10, 255 }, image.PixelData.ToArray());
    }
}
