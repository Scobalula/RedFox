using RedFox.Graphics2D;
using RedFox.Graphics2D.Bmp;
using RedFox.Graphics2D.IO;

namespace RedFox.Tests.Graphics2D;

public sealed class BmpImageTranslatorTests
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void BmpTranslator_RoundTripPattern_PreservesPixels(bool includeTransparency)
    {
        Image source = ImageTranslatorTestHarness.CreatePatternImage(17, 13, includeTransparency);
        ImageTranslatorManager manager = ImageTranslatorTestHarness.CreateManager(new BmpImageTranslator());
        byte[] encoded = ImageTranslatorTestHarness.WriteImageWithManager(manager, source, "pattern.bmp");
        Image decoded = ImageTranslatorTestHarness.ReadImageWithManager(manager, encoded, "pattern.bmp");

        Assert.Equal(source.Width, decoded.Width);
        Assert.Equal(source.Height, decoded.Height);
        Assert.Equal(source.Format, decoded.Format);
        Assert.Equal(source.PixelData.ToArray(), decoded.PixelData.ToArray());
    }

    [Fact]
    public void BmpSamples_ReadAcrossCorpus_DoesNotThrowAndProducesPixels()
    {
        string[] bmpFiles = ImageTranslatorTestHarness.GetInputFiles("Bmp", ".bmp", ".dib");
        if (bmpFiles.Length == 0)
            return;

        ImageTranslatorManager manager = ImageTranslatorTestHarness.CreateManager(new BmpImageTranslator());
        List<string> failures = [];

        foreach (string bmpFile in bmpFiles)
        {
            try
            {
                using FileStream inputFileStream = File.OpenRead(bmpFile);
                Image image = manager.Read(inputFileStream, bmpFile);

                Assert.True(image.Width > 0, $"Expected positive width for '{bmpFile}'.");
                Assert.True(image.Height > 0, $"Expected positive height for '{bmpFile}'.");
                Assert.Equal(ImageFormat.R8G8B8A8Unorm, image.Format);
                Assert.Equal(image.Width * image.Height * 4, image.PixelData.Length);
            }
            catch (Exception exception)
            {
                failures.Add($"{Path.GetFileName(bmpFile)} :: {exception.GetType().Name}: {exception.Message}");
            }
        }

        Assert.True(failures.Count == 0, string.Join(Environment.NewLine, failures));
    }
}