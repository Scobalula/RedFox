using RedFox.Graphics2D;
using RedFox.Graphics2D.Png;

namespace RedFox.Tests.Graphics2D;

public sealed class PngImageTranslatorTests
{
    [Fact]
    public void PngTranslator_RoundTripSmallPattern_PreservesPixels()
    {
        Image source = CreatePatternImage(64, 64);
        PngImageTranslator translator = new();

        using MemoryStream stream = new();
        translator.Write(stream, source);
        stream.Position = 0;

        Image decoded = translator.Read(stream);

        Assert.Equal(source.Width, decoded.Width);
        Assert.Equal(source.Height, decoded.Height);
        Assert.Equal(source.Format, decoded.Format);
        Assert.Equal(source.PixelData.ToArray(), decoded.PixelData.ToArray());
    }

    [Fact]
    public void PngTranslator_RoundTripLargePattern_PreservesPixels()
    {
        Image source = CreatePatternImage(600, 500);
        PngImageTranslator translator = new();

        using MemoryStream stream = new();
        translator.Write(stream, source);
        stream.Position = 0;

        Image decoded = translator.Read(stream);

        Assert.Equal(source.Width, decoded.Width);
        Assert.Equal(source.Height, decoded.Height);
        Assert.Equal(source.Format, decoded.Format);
        Assert.Equal(source.PixelData.ToArray(), decoded.PixelData.ToArray());
    }

    private static Image CreatePatternImage(int width, int height)
    {
        byte[] pixels = new byte[width * height * 4];

        for (int y = 0; y < height; y++)
        {
            for (int x = 0; x < width; x++)
            {
                int offset = (y * width + x) * 4;
                pixels[offset + 0] = (byte)((x * 37 + y * 11) & 0xFF);
                pixels[offset + 1] = (byte)((x * 17 + y * 29) & 0xFF);
                pixels[offset + 2] = (byte)((x * 7 + y * 43) & 0xFF);
                pixels[offset + 3] = (byte)(255 - ((x * 5 + y * 3) & 0x3F));
            }
        }

        return new Image(width, height, ImageFormat.R8G8B8A8Unorm, pixels);
    }
}