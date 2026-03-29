using RedFox.Graphics2D;
using RedFox.Graphics2D.IO;
using RedFox.Graphics2D.Tga;

namespace RedFox.Tests.Graphics2D;

public sealed class TgaImageTranslatorTests
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void TgaTranslator_RoundTripPattern_PreservesPixels(bool includeTransparency)
    {
        Image source = ImageTranslatorTestHarness.CreatePatternImage(19, 11, includeTransparency);
        ImageTranslatorManager manager = ImageTranslatorTestHarness.CreateManager(new TgaImageTranslator());
        byte[] encoded = ImageTranslatorTestHarness.WriteImageWithManager(manager, source, "pattern.tga");
        Image decoded = ImageTranslatorTestHarness.ReadImageWithManager(manager, encoded, "pattern.tga");

        Assert.Equal(source.Width, decoded.Width);
        Assert.Equal(source.Height, decoded.Height);
        Assert.Equal(source.Format, decoded.Format);
        Assert.Equal(source.PixelData.ToArray(), decoded.PixelData.ToArray());
    }

    [Fact]
    public void TgaSamples_ReadAcrossCorpus_DoesNotThrowAndProducesPixels()
    {
        string[] tgaFiles = ImageTranslatorTestHarness.GetInputFiles("Tga", ".tga");
        if (tgaFiles.Length == 0)
            return;

        ImageTranslatorManager manager = ImageTranslatorTestHarness.CreateManager(new TgaImageTranslator());
        List<string> failures = [];

        foreach (string tgaFile in tgaFiles)
        {
            try
            {
                using FileStream inputFileStream = File.OpenRead(tgaFile);
                Image image = manager.Read(inputFileStream, tgaFile);

                Assert.True(image.Width > 0, $"Expected positive width for '{tgaFile}'.");
                Assert.True(image.Height > 0, $"Expected positive height for '{tgaFile}'.");
                Assert.Equal(ImageFormat.R8G8B8A8Unorm, image.Format);
                Assert.Equal(image.Width * image.Height * 4, image.PixelData.Length);
            }
            catch (Exception exception)
            {
                failures.Add($"{Path.GetFileName(tgaFile)} :: {exception.GetType().Name}: {exception.Message}");
            }
        }

        Assert.True(failures.Count == 0, string.Join(Environment.NewLine, failures));
    }
}