using System.Runtime.InteropServices;
using RedFox.Graphics2D;
using RedFox.Graphics2D.IO;

namespace RedFox.Tests.Graphics2D;

internal static class ImageTranslatorTestHarness
{
    public static ImageTranslatorManager CreateManager(params ImageTranslator[] translators)
    {
        ImageTranslatorManager manager = new();
        foreach (ImageTranslator translator in translators)
            manager.Register(translator);

        return manager;
    }

    public static byte[] WriteImageWithManager(ImageTranslatorManager manager, Image image, string sourcePath)
    {
        using MemoryStream stream = new();
        manager.Write(stream, sourcePath, image);
        return stream.ToArray();
    }

    public static byte[] WriteImageWithManager(ImageTranslatorManager manager, Image image, string sourcePath, ImageTranslatorOptions options)
    {
        using MemoryStream stream = new();
        manager.Write(stream, sourcePath, image, options);
        return stream.ToArray();
    }

    public static Image ReadImageWithManager(ImageTranslatorManager manager, byte[] data, string sourcePath)
    {
        using MemoryStream stream = new(data, writable: false);
        return manager.Read(stream, sourcePath);
    }

    public static string[] GetInputFiles(string preferredDirectoryName, params string[] extensions)
    {
        string? testsRoot = Environment.GetEnvironmentVariable("REDFOX_TESTS_DIR");
        if (string.IsNullOrWhiteSpace(testsRoot))
            return [];

        string inputDirectory = Path.Combine(testsRoot, "Input");
        string preferredDirectory = Path.Combine(inputDirectory, preferredDirectoryName);
        string legacyDirectory = Path.Combine(testsRoot, preferredDirectoryName);
        HashSet<string> files = new(StringComparer.OrdinalIgnoreCase);

        AddFiles(files, inputDirectory, extensions);
        AddFiles(files, preferredDirectory, extensions);
        AddFiles(files, legacyDirectory, extensions);

        string[] results = [.. files];
        Array.Sort(results, StringComparer.OrdinalIgnoreCase);
        return results;
    }

    public static Image CreatePatternImage(int width, int height, bool includeTransparency)
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
                pixels[offset + 3] = includeTransparency
                    ? (byte)(255 - ((x * 5 + y * 3) & 0x7F))
                    : (byte)255;
            }
        }

        return new Image(width, height, ImageFormat.R8G8B8A8Unorm, pixels);
    }

    public static Image CreateSolidRgbaImage(int width, int height, byte r, byte g, byte b, byte a)
    {
        byte[] pixels = new byte[width * height * 4];
        for (int offset = 0; offset < pixels.Length; offset += 4)
        {
            pixels[offset + 0] = r;
            pixels[offset + 1] = g;
            pixels[offset + 2] = b;
            pixels[offset + 3] = a;
        }

        return new Image(width, height, ImageFormat.R8G8B8A8Unorm, pixels);
    }

    public static Image CreateFloatPatternImage(int width, int height)
    {
        float[] pixels = new float[width * height * 4];

        for (int y = 0; y < height; y++)
        {
            for (int x = 0; x < width; x++)
            {
                int offset = ((y * width) + x) * 4;
                pixels[offset + 0] = (float)(x + 1) / (width + 1);
                pixels[offset + 1] = (float)(y + 1) / (height + 1);
                pixels[offset + 2] = ((x + y) % 5) * 0.25f;
                pixels[offset + 3] = 1.0f;
            }
        }

        byte[] pixelBytes = MemoryMarshal.AsBytes(pixels.AsSpan()).ToArray();
        return new Image(width, height, ImageFormat.R32G32B32A32Float, pixelBytes);
    }

    private static void AddFiles(HashSet<string> files, string directory, IReadOnlyList<string> extensions)
    {
        if (!Directory.Exists(directory))
            return;

        foreach (string extension in extensions)
        {
            foreach (string file in Directory.EnumerateFiles(directory, $"*{extension}", SearchOption.AllDirectories))
                files.Add(file);
        }
    }
}