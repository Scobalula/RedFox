using RedFox.Graphics2D;
using RedFox.Graphics2D.Jpeg;

namespace RedFox.Tests.Graphics2D;

public sealed class JpegImageTranslatorTests
{
    [Fact]
    public void JpegTranslator_RoundTripSolidImage_PreservesPixels()
    {
        byte[] sourcePixels = CreateSolidRgbaPixels(16, 16, 128, 128, 128, 255);
        Image source = new(16, 16, ImageFormat.R8G8B8A8Unorm, sourcePixels);
        JpegImageTranslator translator = new()
        {
            EncoderOptions = new JpegEncoderOptions
            {
                Quality = 100,
                Subsampling = JpegChromaSubsampling.Yuv444,
            },
        };

        using MemoryStream stream = new();
        translator.Write(stream, source);
        stream.Position = 0;

        Image decoded = translator.Read(stream);

        Assert.Equal(source.Width, decoded.Width);
        Assert.Equal(source.Height, decoded.Height);
        Assert.Equal(source.Format, decoded.Format);
        Assert.Equal(source.PixelData.ToArray(), decoded.PixelData.ToArray());
    }

    private static byte[] CreateSolidRgbaPixels(int width, int height, byte r, byte g, byte b, byte a)
    {
        byte[] pixels = new byte[width * height * 4];
        for (int offset = 0; offset < pixels.Length; offset += 4)
        {
            pixels[offset] = r;
            pixels[offset + 1] = g;
            pixels[offset + 2] = b;
            pixels[offset + 3] = a;
        }

        return pixels;
    }
}