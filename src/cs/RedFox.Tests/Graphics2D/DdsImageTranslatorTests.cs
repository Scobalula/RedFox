using RedFox.Graphics2D;
using RedFox.Graphics2D.IO;

namespace RedFox.Tests.Graphics2D;

public sealed class DdsImageTranslatorTests
{
    [Fact]
    public void DdsTranslator_RoundTripViaManager_PreservesImageData()
    {
        byte[] sourcePixels =
        [
            255, 0, 0, 255,
            0, 255, 0, 255,
            0, 0, 255, 255,
            255, 255, 255, 255,
        ];

        Image source = new(2, 2, ImageFormat.R8G8B8A8Unorm, sourcePixels);
        ImageTranslatorManager manager = ImageTranslatorTestHarness.CreateManager(new DdsImageTranslator());
        byte[] encoded = ImageTranslatorTestHarness.WriteImageWithManager(manager, source, "texture.dds");
        Image loaded = ImageTranslatorTestHarness.ReadImageWithManager(manager, encoded, "texture.dds");

        Assert.Equal(source.Width, loaded.Width);
        Assert.Equal(source.Height, loaded.Height);
        Assert.Equal(source.Depth, loaded.Depth);
        Assert.Equal(source.ArraySize, loaded.ArraySize);
        Assert.Equal(source.MipLevels, loaded.MipLevels);
        Assert.Equal(source.Format, loaded.Format);
        Assert.Equal(source.IsCubemap, loaded.IsCubemap);
        Assert.Equal(source.PixelData.ToArray(), loaded.PixelData.ToArray());
    }

    [Fact]
    public void DdsImageTranslator_IsValidRecognizesDdsMagic()
    {
        DdsImageTranslator translator = new();
        byte[] validHeader = [0x44, 0x44, 0x53, 0x20];
        byte[] invalidHeader = [0x00, 0x01, 0x02, 0x03];

        Assert.True(translator.IsValid(validHeader, "texture.dds", ".dds"));
        Assert.False(translator.IsValid(invalidHeader, "texture.dds", ".dds"));
    }

    [Fact]
    public void DdsTranslator_ViaManager_InvalidCubemap_ThrowsInvalidDataException()
    {
        Image invalidCubemap = new(4, 4, depth: 1, arraySize: 5, mipLevels: 1, ImageFormat.R8G8B8A8Unorm, isCubemap: true);
        ImageTranslatorManager manager = ImageTranslatorTestHarness.CreateManager(new DdsImageTranslator());
        using MemoryStream stream = new();

        Assert.Throws<InvalidDataException>(() => manager.Write(stream, "cubemap.dds", invalidCubemap));
    }

    [Fact]
    public void DdsSamples_ReadAcrossCorpus_DoesNotThrowAndProducesImageData()
    {
        string[] ddsFiles = ImageTranslatorTestHarness.GetInputFiles("DDS", ".dds");
        if (ddsFiles.Length == 0)
            return;

        ImageTranslatorManager manager = ImageTranslatorTestHarness.CreateManager(new DdsImageTranslator());
        List<string> failures = [];

        foreach (string ddsFile in ddsFiles)
        {
            try
            {
                using FileStream inputFileStream = File.OpenRead(ddsFile);
                Image image = manager.Read(inputFileStream, ddsFile);

                Assert.True(image.Width > 0, $"Expected positive width for '{ddsFile}'.");
                Assert.True(image.Height > 0, $"Expected positive height for '{ddsFile}'.");
                Assert.True(image.PixelData.Length > 0, $"Expected pixel data for '{ddsFile}'.");
            }
            catch (Exception exception)
            {
                failures.Add($"{Path.GetFileName(ddsFile)} :: {exception.GetType().Name}: {exception.Message}");
            }
        }

        Assert.True(failures.Count == 0, string.Join(Environment.NewLine, failures));
    }
}
