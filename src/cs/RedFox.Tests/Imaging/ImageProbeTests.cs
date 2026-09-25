using System.Buffers.Binary;
using RedFox.Imaging;
using RedFox.Imaging.Formats;
using RedFox.Imaging.IO;
using RedFox.Imaging.Primitives;

namespace RedFox.Tests.Imaging;

public sealed class ImageProbeTests
{
    [Fact]
    public void RegisterAll_RegistersEveryBuiltInTranslatorWithMetadataSupport()
    {
        ImageTranslatorManager manager = CreateManager();

        Assert.Equal(["BMP", "DDS", "EXR", "JPEG", "KTX", "PNG", "TGA", "TIFF"], manager.Translators.Select(t => t.Name).Order());
        Assert.All(manager.Translators, t => Assert.True(t.CanReadInfo));
    }

    [Theory]
    [InlineData("image.png")]
    [InlineData("image.dds")]
    [InlineData("image.ktx")]
    [InlineData("image.tga")]
    [InlineData("image.bmp")]
    [InlineData("image.jpg")]
    [InlineData("image.tiff")]
    [InlineData("image.exr")]
    public void ReadInfo_MatchesDecodedImage_FromOffsetStream(string fileName)
    {
        ImageTranslatorManager manager = CreateManager();
        Image source = ImageTranslatorTestHarness.CreatePatternImage(6, 5, includeTransparency: false);
        byte[] encoded = ImageTranslatorTestHarness.WriteImageWithManager(manager, source, fileName);
        using MemoryStream stream = new([.. new byte[7], .. encoded]);
        stream.Position = 7;

        ImageInfo info = manager.ReadInfo(stream, fileName);
        stream.Position = 7;
        Image decoded = manager.Read(stream, fileName);

        Assert.Equal(decoded.Info, info);
        Assert.Equal(6, info.Width);
        Assert.Equal(5, info.Height);
        Assert.Equal(fileName.Split('.')[1].ToUpperInvariant().Replace("JPG", "JPEG"), manager.GetTranslator(new MemoryStream(encoded), fileName).Name);
    }

    [Fact]
    public void Read_RejectsImagesAboveTheConfiguredByteLimit()
    {
        ImageTranslatorManager manager = CreateManager();
        byte[] encoded = ImageTranslatorTestHarness.WriteImageWithManager(manager, ImageTranslatorTestHarness.CreatePatternImage(16, 16, includeTransparency: false), "image.png");
        manager.MaxImageBytes = 1023;

        Assert.Throws<InvalidDataException>(() => ImageTranslatorTestHarness.ReadImageWithManager(manager, encoded, "image.png"));
    }

    [Fact]
    public void Dds_WithImpossibleDimensions_IsRejectedBeforeAllocation()
    {
        byte[] encoded = WriteDds(new Image(4, 4, ImageFormat.R32G32B32A32Float));
        BinaryPrimitives.WriteUInt32LittleEndian(encoded.AsSpan(12), 65535);
        BinaryPrimitives.WriteUInt32LittleEndian(encoded.AsSpan(16), 65535);

        Assert.Throws<InvalidDataException>(() => ImageTranslatorTestHarness.ReadImageWithManager(CreateManager(), encoded, "image.dds"));
    }

    [Fact]
    public void Dds_WithTruncatedPayload_ThrowsInvalidData()
    {
        byte[] encoded = WriteDds(new Image(64, 64, ImageFormat.R8G8B8A8Unorm));

        Assert.Throws<InvalidDataException>(() => ImageTranslatorTestHarness.ReadImageWithManager(CreateManager(), encoded[..200], "image.dds"));
    }

    [Fact]
    public void Tga_WithTruncatedOrOversizedHeader_ThrowsInvalidData()
    {
        byte[] header = new byte[18];
        header[2] = 2;
        header[16] = 32;
        BinaryPrimitives.WriteUInt16LittleEndian(header.AsSpan(12), 512);
        BinaryPrimitives.WriteUInt16LittleEndian(header.AsSpan(14), 512);

        Assert.Throws<InvalidDataException>(() => ImageTranslatorTestHarness.ReadImageWithManager(CreateManager(), header, "image.tga"));

        BinaryPrimitives.WriteUInt16LittleEndian(header.AsSpan(12), 65535);
        BinaryPrimitives.WriteUInt16LittleEndian(header.AsSpan(14), 65535);

        Assert.Throws<InvalidDataException>(() => CreateManager().ReadInfo(new MemoryStream(header), "image.tga"));
    }

    [Fact]
    public void Bmp_WithOversizedPalette_ThrowsInvalidData()
    {
        Image source = ImageTranslatorTestHarness.CreatePatternImage(4, 4, includeTransparency: false);
        byte[] encoded = ImageTranslatorTestHarness.WriteImageWithManager(CreateManager(), source, "image.bmp");
        BinaryPrimitives.WriteUInt16LittleEndian(encoded.AsSpan(28), 8);
        BinaryPrimitives.WriteUInt32LittleEndian(encoded.AsSpan(46), 100_000);

        Assert.Throws<InvalidDataException>(() => ImageTranslatorTestHarness.ReadImageWithManager(CreateManager(), encoded, "image.bmp"));
    }

    private static ImageTranslatorManager CreateManager()
    {
        ImageTranslatorManager manager = new();
        BuiltInImageFormats.RegisterAll(manager);
        return manager;
    }

    private static byte[] WriteDds(Image image)
    {
        return ImageTranslatorTestHarness.WriteImageWithManager(CreateManager(), image, "image.dds");
    }
}
