using RedFox.Imaging;
using RedFox.Imaging.Primitives;
using RedFox.Imaging.Formats.Exr;
using RedFox.Imaging.Formats.Dds;
using RedFox.Imaging.IO;

namespace RedFox.Tests.Imaging;

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
        string previewPath = ImageTranslatorTestHarness.WriteRgbaDdsOutput(decoded, "RoundTrip/pattern.exr", "Exr");
        Image preview = DdsLoader.Load(previewPath);

        Assert.Equal(source.Width, decoded.Width);
        Assert.Equal(source.Height, decoded.Height);
        Assert.Equal(ImageFormat.R32G32B32A32Float, decoded.Format);
        Assert.Equal(source.PixelData.ToArray(), decoded.PixelData.ToArray());
        Assert.Equal(ImageFormat.R8G8B8A8Unorm, preview.Format);
        Assert.Equal(source.Width, preview.Width);
        Assert.Equal(source.Height, preview.Height);
    }

    [Fact]
    public void ExrSamples_ReadAcrossCorpus_DoesNotThrowAndProducesPixels()
    {
        string[] exrFiles = ImageTranslatorTestHarness.GetRequiredInputFiles("Exr", ".exr");

        ImageTranslatorManager manager = ImageTranslatorTestHarness.CreateManager(new ExrImageTranslator());
        List<string> failures = [];

        foreach (string exrFile in exrFiles)
        {
            try
            {
                using FileStream inputFileStream = File.OpenRead(exrFile);
                Image image = manager.Read(inputFileStream, exrFile);
                ImageTranslatorTestHarness.WriteRgbaDdsOutput(image, exrFile, "Exr");

                Assert.True(image.Width > 0, $"Expected positive width for '{exrFile}'.");
                Assert.True(image.Height > 0, $"Expected positive height for '{exrFile}'.");
                Assert.Equal(ImageFormat.R32G32B32A32Float, image.Format);
                Assert.True(image.PixelData.Length > 0, $"Expected pixel data for '{exrFile}'.");
            }
            catch (Exception exception)
            {
                string failedInputPath = ImageTranslatorTestHarness.CopyFailedInputToOutput(exrFile, "Exr");
                failures.Add($"{exrFile} :: {exception.GetType().Name}: {exception.Message}{Environment.NewLine}{exception.StackTrace}{Environment.NewLine}Source copied to '{failedInputPath}'.");
            }
        }

        Assert.True(failures.Count == 0, string.Join(Environment.NewLine, failures));
    }
}
