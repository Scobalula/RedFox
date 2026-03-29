using RedFox.Graphics2D;
using RedFox.Graphics2D.IO;
using RedFox.Graphics2D.Jpeg;

namespace RedFox.Tests.Graphics2D;

public sealed class JpegImageTranslatorTests
{
    [Theory]
    [InlineData(100, ImageCompressionPreference.None)]
    [InlineData(100, ImageCompressionPreference.SmallestSize)]
    public void JpegTranslator_RoundTripSolidImage_PreservesPixels(int quality, ImageCompressionPreference compressionPreference)
    {
        Image source = ImageTranslatorTestHarness.CreateSolidRgbaImage(16, 16, 128, 128, 128, 255);
        ImageTranslatorManager manager = ImageTranslatorTestHarness.CreateManager(new JpegImageTranslator());
        ImageTranslatorOptions options = new()
        {
            Quality = quality,
            Compression = compressionPreference,
        };
        byte[] encoded = ImageTranslatorTestHarness.WriteImageWithManager(manager, source, "solid.jpg", options);
        Image decoded = ImageTranslatorTestHarness.ReadImageWithManager(manager, encoded, "solid.jpg");

        Assert.Equal(source.Width, decoded.Width);
        Assert.Equal(source.Height, decoded.Height);
        Assert.Equal(source.Format, decoded.Format);
        Assert.Equal(source.PixelData.ToArray(), decoded.PixelData.ToArray());
    }

    [Fact]
    public void JpegSamples_ReadAcrossCorpus_DoesNotThrowAndProducesPixels()
    {
        string[] jpegFiles = ImageTranslatorTestHarness.GetInputFiles("Jpeg", ".jpg", ".jpeg", ".jpe", ".jfif");
        if (jpegFiles.Length == 0)
            return;

        ImageTranslatorManager manager = ImageTranslatorTestHarness.CreateManager(new JpegImageTranslator());
        List<string> failures = [];

        foreach (string jpegFile in jpegFiles)
        {
            try
            {
                using FileStream inputFileStream = File.OpenRead(jpegFile);
                Image image = manager.Read(inputFileStream, jpegFile);

                Assert.True(image.Width > 0, $"Expected positive width for '{jpegFile}'.");
                Assert.True(image.Height > 0, $"Expected positive height for '{jpegFile}'.");
                Assert.Equal(ImageFormat.R8G8B8A8Unorm, image.Format);
                Assert.Equal(image.Width * image.Height * 4, image.PixelData.Length);
            }
            catch (Exception exception)
            {
                failures.Add($"{Path.GetFileName(jpegFile)} :: {exception.GetType().Name}: {exception.Message}");
            }
        }

        Assert.True(failures.Count == 0, string.Join(Environment.NewLine, failures));
    }
}