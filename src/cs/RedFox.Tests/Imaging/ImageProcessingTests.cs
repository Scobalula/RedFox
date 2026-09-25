using System.Numerics;
using RedFox.Imaging;
using RedFox.Imaging.Primitives;
using RedFox.Imaging.Processing;

namespace RedFox.Tests.Imaging;

public sealed class ImageProcessingTests
{
    [Fact]
    public void Crop_CopiesTheRequestedRectangle()
    {
        Image source = ImageTranslatorTestHarness.CreatePatternImage(4, 4, includeTransparency: true);

        Image cropped = source.Crop(1, 2, 3, 2);

        Assert.Equal(3, cropped.Width);
        Assert.Equal(2, cropped.Height);
        Assert.Equal(PixelAt(source, 1, 2), PixelAt(cropped, 0, 0));
        Assert.Equal(PixelAt(source, 3, 3), PixelAt(cropped, 2, 1));
        Assert.Throws<ArgumentOutOfRangeException>(() => source.Crop(2, 0, 3, 1));
    }

    [Fact]
    public void Flips_MirrorPixelsAndPreserveMips()
    {
        Image source = ImageTranslatorTestHarness.CreatePatternImage(4, 2, includeTransparency: false).GenerateMipmaps();

        Image horizontal = source.FlipHorizontal();
        Image vertical = source.FlipVertical();

        Assert.Equal(source.MipLevels, horizontal.MipLevels);
        Assert.Equal(PixelAt(source, 0, 1), PixelAt(horizontal, 3, 1));
        Assert.Equal(PixelAt(source, 2, 0), PixelAt(vertical, 2, 1));
        Assert.Equal(source.PixelData.ToArray(), horizontal.FlipHorizontal().PixelData.ToArray());
    }

    [Fact]
    public void Rotations_SwapDimensionsAndCompose()
    {
        Image source = ImageTranslatorTestHarness.CreatePatternImage(3, 2, includeTransparency: true);

        Image rotated = source.Rotate90();

        Assert.Equal(2, rotated.Width);
        Assert.Equal(3, rotated.Height);
        Assert.Equal(PixelAt(source, 0, 1), PixelAt(rotated, 0, 0));
        Assert.Equal(PixelAt(source, 0, 0), PixelAt(rotated, 1, 0));
        Assert.Equal(source.PixelData.ToArray(), rotated.Rotate270().PixelData.ToArray());
        Assert.Equal(source.Rotate180().PixelData.ToArray(), rotated.Rotate90().PixelData.ToArray());
        Assert.Equal(source.PixelData.ToArray(), rotated.Rotate90().Rotate90().Rotate90().PixelData.ToArray());
    }

    [Fact]
    public void Crop_OnBlockCompressedImage_ReencodesThroughCodec()
    {
        Image source = ImageTranslatorTestHarness.CreateSolidRgbaImage(8, 8, 255, 0, 0, 255);
        source.Convert(ImageFormat.BC1Unorm);

        Image cropped = source.Crop(4, 4, 4, 4);

        Assert.Equal(ImageFormat.BC1Unorm, cropped.Format);
        Assert.Equal(8, cropped.PixelData.Length);
        Assert.Equal(1f, cropped.GetSlice().GetPixel(1, 1).X, 2);
    }

    [Fact]
    public void Resize_NearestNeighbor_ReplicatesPixels()
    {
        Image source = ImageTranslatorTestHarness.CreatePatternImage(2, 2, includeTransparency: true);

        Image resized = source.Resize(4, 4, ResamplingFilter.NearestNeighbor);

        Assert.Equal(PixelAt(source, 0, 0), PixelAt(resized, 1, 1));
        Assert.Equal(PixelAt(source, 1, 0), PixelAt(resized, 2, 0));
        Assert.Equal(PixelAt(source, 1, 1), PixelAt(resized, 3, 3));
    }

    [Fact]
    public void Resize_Bilinear_InterpolatesBetweenNeighbours()
    {
        Image source = CreateFloatImage(2, 1, new Vector4(0f, 0f, 0f, 1f), new Vector4(1f, 1f, 1f, 1f));

        Vector4[] resized = source.Resize(4, 1, ResamplingFilter.Bilinear).DecodeSlice();

        Assert.Equal(0f, resized[0].X, 4);
        Assert.Equal(0.25f, resized[1].X, 4);
        Assert.Equal(0.75f, resized[2].X, 4);
        Assert.Equal(1f, resized[3].X, 4);
    }

    [Fact]
    public void Resize_Box_AveragesCoveredPixels()
    {
        Image source = CreateFloatImage(4, 1, new Vector4(0f), new Vector4(1f), new Vector4(2f), new Vector4(4f));

        Vector4[] resized = source.Resize(2, 1, ResamplingFilter.Box).DecodeSlice();

        Assert.Equal(new Vector4(0.5f), resized[0]);
        Assert.Equal(new Vector4(3f), resized[1]);
    }

    [Fact]
    public void GenerateMipmaps_BuildsFullChainWithBoxFilteredLevels()
    {
        Image source = CreateFloatImage(2, 2, new Vector4(0f, 0f, 0f, 1f), new Vector4(1f, 1f, 1f, 1f), new Vector4(1f, 1f, 1f, 1f), new Vector4(0f, 0f, 0f, 1f));
        Image wide = new(8, 2, ImageFormat.R8G8B8A8Unorm);

        Image mipped = source.GenerateMipmaps();

        Assert.Equal(2, mipped.MipLevels);
        Assert.Equal(source.PixelData.ToArray(), mipped.GetSlice(0).PixelSpan.ToArray());
        Assert.Equal(new Vector4(0.5f, 0.5f, 0.5f, 1f), mipped.DecodeSlice(1)[0]);
        Assert.Equal(4, wide.GenerateMipmaps().MipLevels);
        Assert.Throws<ArgumentOutOfRangeException>(() => source.GenerateMipmaps(3, MipmapMode.Standard));
    }

    [Fact]
    public void GenerateMipmaps_FiltersSrgbInLinearSpace()
    {
        Image linear = CreateBlackWhiteImage(ImageFormat.R8G8B8A8Unorm);
        Image srgb = CreateBlackWhiteImage(ImageFormat.R8G8B8A8UnormSrgb);

        byte linearMip = linear.GenerateMipmaps().GetSlice(1).PixelSpan[0];
        byte srgbMip = srgb.GenerateMipmaps().GetSlice(1).PixelSpan[0];

        Assert.Equal(128, linearMip);
        Assert.Equal(188, srgbMip);
    }

    [Fact]
    public void GenerateMipmaps_NormalMap_RenormalisesFilteredVectors()
    {
        Image source = CreateFloatImage(2, 1, new Vector4(1f, 0f, 0f, 1f), new Vector4(0f, 1f, 0f, 1f));

        Vector4 normal = source.GenerateMipmaps(MipmapMode.NormalMap).DecodeSlice(1)[0];

        Assert.Equal(MathF.Sqrt(0.5f), normal.X, 4);
        Assert.Equal(MathF.Sqrt(0.5f), normal.Y, 4);
        Assert.Equal(1f, new Vector3(normal.X, normal.Y, normal.Z).Length(), 4);
    }

    [Fact]
    public void GenerateMipmaps_NormalMap_ReconstructsZForTwoChannelFormats()
    {
        byte[] data = [(byte)(0.2f * 255), 128, (byte)(0.8f * 255 + 1), 128];
        Image source = new(2, 1, ImageFormat.R8G8Unorm, data);

        ReadOnlySpan<byte> mip = source.GenerateMipmaps(MipmapMode.NormalMap).GetSlice(1).PixelSpan;

        Assert.InRange(mip[0], 127, 129);
        Assert.InRange(mip[1], 127, 129);
    }

    [Fact]
    public void Swizzle_RearrangesChannels()
    {
        Image source = ImageTranslatorTestHarness.CreateSolidRgbaImage(1, 1, 10, 20, 30, 40);

        byte[] swizzled = source.Swizzle(ColorChannel.Blue, ColorChannel.Green, ColorChannel.Red, ColorChannel.One).PixelData.ToArray();

        Assert.Equal(new byte[] { 30, 20, 10, 255 }, swizzled);
    }

    [Fact]
    public void ExtractAndCombineChannels_RoundTrip()
    {
        Image source = ImageTranslatorTestHarness.CreatePatternImage(3, 3, includeTransparency: true);

        Image red = source.ExtractChannel(ColorChannel.Red, ImageFormat.R8Unorm);
        Image green = source.ExtractChannel(ColorChannel.Green, ImageFormat.R8Unorm);
        Image blue = source.ExtractChannel(ColorChannel.Blue, ImageFormat.R8Unorm);
        Image alpha = source.ExtractChannel(ColorChannel.Alpha, ImageFormat.R8Unorm);
        Image combined = ImageChannels.CombineChannels(red, green, blue, alpha, ImageFormat.R8G8B8A8Unorm);

        Assert.Equal(ImageFormat.R8Unorm, alpha.Format);
        Assert.Equal(source.PixelData[3], alpha.PixelData[0]);
        Assert.Equal(source.PixelData.ToArray(), combined.PixelData.ToArray());
        Assert.Throws<ArgumentException>(() => ImageChannels.CombineChannels(red, new Image(2, 2, ImageFormat.R8Unorm), null, null, ImageFormat.R8G8B8A8Unorm));
    }

    private static uint PixelAt(Image image, int x, int y)
    {
        return image.GetSlice().GetPixelRowSpan<uint>(y)[x];
    }

    private static Image CreateBlackWhiteImage(ImageFormat format)
    {
        return new Image(2, 1, format, [0, 0, 0, 255, 255, 255, 255, 255]);
    }

    private static Image CreateFloatImage(int width, int height, params Vector4[] pixels)
    {
        Image image = new(width, height, ImageFormat.R32G32B32A32Float);
        image.EncodeSlice(0, 0, 0, pixels);
        return image;
    }
}
