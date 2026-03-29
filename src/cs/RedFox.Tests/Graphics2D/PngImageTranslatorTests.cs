using RedFox.Graphics2D;
using RedFox.Graphics2D.IO;
using RedFox.Graphics2D.Png;

namespace RedFox.Tests.Graphics2D;

public sealed class PngImageTranslatorTests
{
    [Theory]
    [InlineData(64, 64, ImageCompressionPreference.Fast)]
    [InlineData(600, 500, ImageCompressionPreference.SmallestSize)]
    public void PngTranslator_RoundTripPattern_PreservesPixels(int width, int height, ImageCompressionPreference compressionPreference)
    {
        Image source = ImageTranslatorTestHarness.CreatePatternImage(width, height, includeTransparency: true);
        ImageTranslatorManager manager = ImageTranslatorTestHarness.CreateManager(new PngImageTranslator());
        ImageTranslatorOptions options = new()
        {
            Compression = compressionPreference,
        };
        byte[] encoded = ImageTranslatorTestHarness.WriteImageWithManager(manager, source, "pattern.png", options);
        Image decoded = ImageTranslatorTestHarness.ReadImageWithManager(manager, encoded, "pattern.png");

        Assert.Equal(source.Width, decoded.Width);
        Assert.Equal(source.Height, decoded.Height);
        Assert.Equal(source.Format, decoded.Format);
        Assert.Equal(source.PixelData.ToArray(), decoded.PixelData.ToArray());
    }

    [Fact]
    public void PngSamples_ReadAcrossCorpus_DoesNotThrowAndProducesPixels()
    {
        string[] pngFiles = ImageTranslatorTestHarness.GetInputFiles("Png", ".png");
        if (pngFiles.Length == 0)
            return;

        ImageTranslatorManager manager = ImageTranslatorTestHarness.CreateManager(new PngImageTranslator());
        List<string> failures = [];

        foreach (string pngFile in pngFiles)
        {
            try
            {
                using FileStream inputFileStream = File.OpenRead(pngFile);
                Image image = manager.Read(inputFileStream, pngFile);

                Assert.True(image.Width > 0, $"Expected positive width for '{pngFile}'.");
                Assert.True(image.Height > 0, $"Expected positive height for '{pngFile}'.");
                Assert.True(image.PixelData.Length > 0, $"Expected pixel data for '{pngFile}'.");
            }
            catch (Exception exception)
            {
                failures.Add($"{Path.GetFileName(pngFile)} :: {exception.GetType().Name}: {exception.Message}");
            }
        }

        Assert.True(failures.Count == 0, string.Join(Environment.NewLine, failures));
    }
}