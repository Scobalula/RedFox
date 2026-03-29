using System.Runtime.InteropServices;
using RedFox.Graphics2D;
using RedFox.Graphics2D.Bmp;
using RedFox.Graphics2D.Exr;
using RedFox.Graphics2D.IO;
using RedFox.Graphics2D.Jpeg;
using RedFox.Graphics2D.Png;
using RedFox.Graphics2D.Tga;
using RedFox.Graphics2D.Tiff;

namespace RedFox.Samples.Examples;

/// <summary>
/// Demonstrates writing and reading multiple image formats using generic manager-level options.
/// </summary>
internal sealed class ImageTranslationSample : ISample
{
    /// <inheritdoc />
    public string Name => "graphics2d-images";

    /// <inheritdoc />
    public string Description => "Writes sample images across multiple formats using generic quality and compression options.";

    /// <inheritdoc />
    public int Run(string[] arguments)
    {
        string outputDirectory = arguments.Length > 0
            ? Path.GetFullPath(arguments[0])
            : Path.Combine(Environment.CurrentDirectory, "artifacts", "images");

        Directory.CreateDirectory(outputDirectory);

        ImageTranslatorManager manager = CreateManager();
        Image standardDynamicRangeImage = CreatePatternImage(width: 192, height: 128);
        Image highDynamicRangeImage = CreateHighDynamicRangeImage(width: 96, height: 64);

        Console.WriteLine($"Writing image samples to: {outputDirectory}");

        WriteVariant(manager, standardDynamicRangeImage, Path.Combine(outputDirectory, "pattern-fast.png"), new ImageTranslatorOptions { Compression = ImageCompressionPreference.Fast });
        WriteVariant(manager, standardDynamicRangeImage, Path.Combine(outputDirectory, "pattern-small.png"), new ImageTranslatorOptions { Compression = ImageCompressionPreference.SmallestSize });
        WriteVariant(manager, standardDynamicRangeImage, Path.Combine(outputDirectory, "pattern-quality55.jpg"), new ImageTranslatorOptions { Quality = 55, Compression = ImageCompressionPreference.Fast });
        WriteVariant(manager, standardDynamicRangeImage, Path.Combine(outputDirectory, "pattern-quality92.jpg"), new ImageTranslatorOptions { Quality = 92, Compression = ImageCompressionPreference.None });
        WriteVariant(manager, standardDynamicRangeImage, Path.Combine(outputDirectory, "pattern-balanced.tiff"), new ImageTranslatorOptions { Compression = ImageCompressionPreference.Balanced });
        WriteVariant(manager, standardDynamicRangeImage, Path.Combine(outputDirectory, "pattern-smallest.tiff"), new ImageTranslatorOptions { Compression = ImageCompressionPreference.SmallestSize });
        WriteVariant(manager, standardDynamicRangeImage, Path.Combine(outputDirectory, "pattern.bmp"), new ImageTranslatorOptions());
        WriteVariant(manager, standardDynamicRangeImage, Path.Combine(outputDirectory, "pattern.tga"), new ImageTranslatorOptions());
        WriteVariant(manager, highDynamicRangeImage, Path.Combine(outputDirectory, "pattern-half.exr"), new ImageTranslatorOptions { Compression = ImageCompressionPreference.Balanced, BitsPerChannel = 16 });
        WriteVariant(manager, highDynamicRangeImage, Path.Combine(outputDirectory, "pattern-float.exr"), new ImageTranslatorOptions { Compression = ImageCompressionPreference.SmallestSize, BitsPerChannel = 32 });

        Console.WriteLine("For precise codec-specific control, use the individual translator or writer APIs directly.");
        return 0;
    }

    private static ImageTranslatorManager CreateManager()
    {
        ImageTranslatorManager manager = new();
        manager.Register(new BmpImageTranslator());
        manager.Register(new ExrImageTranslator());
        manager.Register(new JpegImageTranslator());
        manager.Register(new PngImageTranslator());
        manager.Register(new TgaImageTranslator());
        manager.Register(new TiffImageTranslator());
        return manager;
    }

    private static void WriteVariant(ImageTranslatorManager manager, Image image, string outputPath, ImageTranslatorOptions options)
    {
        manager.Write(outputPath, image, options);

        using FileStream inputFileStream = File.OpenRead(outputPath);
        Image decoded = manager.Read(inputFileStream, outputPath);
        long sizeInBytes = new FileInfo(outputPath).Length;

        Console.WriteLine($"  {Path.GetFileName(outputPath),-20} size={sizeInBytes,8} bytes  decoded={decoded.Width}x{decoded.Height} {decoded.Format}");
    }

    private static Image CreatePatternImage(int width, int height)
    {
        byte[] pixels = new byte[width * height * 4];

        for (int y = 0; y < height; y++)
        {
            for (int x = 0; x < width; x++)
            {
                int offset = ((y * width) + x) * 4;
                pixels[offset + 0] = (byte)((x * 37 + y * 11) & 0xFF);
                pixels[offset + 1] = (byte)((x * 17 + y * 29) & 0xFF);
                pixels[offset + 2] = (byte)((x * 7 + y * 43) & 0xFF);
                pixels[offset + 3] = (byte)(255 - ((x * 5 + y * 3) & 0x7F));
            }
        }

        return new Image(width, height, ImageFormat.R8G8B8A8Unorm, pixels);
    }

    private static Image CreateHighDynamicRangeImage(int width, int height)
    {
        float[] pixels = new float[width * height * 4];

        for (int y = 0; y < height; y++)
        {
            for (int x = 0; x < width; x++)
            {
                int offset = ((y * width) + x) * 4;
                pixels[offset + 0] = (float)(x + 1) / width * 2.0f;
                pixels[offset + 1] = (float)(y + 1) / height * 1.5f;
                pixels[offset + 2] = ((x + y) % 9) * 0.35f;
                pixels[offset + 3] = 1.0f;
            }
        }

        byte[] pixelBytes = MemoryMarshal.AsBytes(pixels.AsSpan()).ToArray();
        return new Image(width, height, ImageFormat.R32G32B32A32Float, pixelBytes);
    }
}