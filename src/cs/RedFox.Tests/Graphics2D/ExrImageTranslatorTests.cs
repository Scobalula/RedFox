using RedFox.Graphics2D;
using RedFox.Graphics2D.Exr;
using RedFox.Graphics2D.IO;

namespace RedFox.Tests.Graphics2D;

public sealed class ExrImageTranslatorTests
{
    [Theory]
    [InlineData(ImageCompressionPreference.None, 32)]
    [InlineData(ImageCompressionPreference.Balanced, 32)]
    public void ExrTranslator_RoundTripFloatPattern_PreservesPixels(ImageCompressionPreference compressionPreference, int bitsPerChannel)
    {
        Image source = ImageTranslatorTestHarness.CreateFloatPatternImage(4, 3);
        ImageTranslatorManager manager = ImageTranslatorTestHarness.CreateManager(new ExrImageTranslator());
        ImageTranslatorOptions options = new()
        {
            Compression = compressionPreference,
            BitsPerChannel = bitsPerChannel,
        };
        byte[] encoded = ImageTranslatorTestHarness.WriteImageWithManager(manager, source, "pattern.exr", options);
        Image decoded = ImageTranslatorTestHarness.ReadImageWithManager(manager, encoded, "pattern.exr");

        Assert.Equal(source.Width, decoded.Width);
        Assert.Equal(source.Height, decoded.Height);
        Assert.Equal(ImageFormat.R32G32B32A32Float, decoded.Format);
        Assert.Equal(source.PixelData.ToArray(), decoded.PixelData.ToArray());
    }

    [Fact]
    public void ExrSamples_ReadAcrossCorpus_DoesNotThrowAndProducesPixels()
    {
        string[] exrFiles = ImageTranslatorTestHarness.GetInputFiles("Exr", ".exr");
        if (exrFiles.Length == 0)
            return;

        ImageTranslatorManager manager = ImageTranslatorTestHarness.CreateManager(new ExrImageTranslator());
        List<string> failures = [];

        foreach (string exrFile in exrFiles)
        {
            try
            {
                using FileStream inputFileStream = File.OpenRead(exrFile);
                Image image = manager.Read(inputFileStream, exrFile);

                Assert.True(image.Width > 0, $"Expected positive width for '{exrFile}'.");
                Assert.True(image.Height > 0, $"Expected positive height for '{exrFile}'.");
                Assert.Equal(ImageFormat.R32G32B32A32Float, image.Format);
                Assert.True(image.PixelData.Length > 0, $"Expected pixel data for '{exrFile}'.");
            }
            catch (Exception exception)
            {
                failures.Add($"{Path.GetFileName(exrFile)} :: {exception.GetType().Name}: {exception.Message}");
            }
        }

        Assert.True(failures.Count == 0, string.Join(Environment.NewLine, failures));
    }
}