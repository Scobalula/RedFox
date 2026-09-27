using System.Runtime.InteropServices;
using RedFox.Imaging;
using RedFox.Imaging.Primitives;
using RedFox.Imaging.IO;
using RedFox.Imaging.Formats.Dds;

namespace RedFox.Tests.Imaging;

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

    public static string[] GetRequiredInputFiles(string preferredDirectoryName, params string[] extensions)
    {
        string? testsRoot = Environment.GetEnvironmentVariable("REDFOX_TESTS_DIR");
        if (string.IsNullOrWhiteSpace(testsRoot))
            throw new DirectoryNotFoundException("REDFOX_TESTS_DIR must point to the test-data root containing the required image inputs.");

        if (!Directory.Exists(testsRoot))
            throw new DirectoryNotFoundException($"REDFOX_TESTS_DIR points to a directory that does not exist: '{testsRoot}'.");

        string inputDirectory = Path.Combine(testsRoot, "Input");
        string preferredDirectory = Path.Combine(inputDirectory, preferredDirectoryName);
        string legacyDirectory = Path.Combine(testsRoot, preferredDirectoryName);
        HashSet<string> files = new(StringComparer.OrdinalIgnoreCase);

        AddFiles(files, inputDirectory, extensions);
        AddFiles(files, preferredDirectory, extensions);
        AddFiles(files, legacyDirectory, extensions);

        string[] results = [.. files];
        Array.Sort(results, StringComparer.OrdinalIgnoreCase);

        if (results.Length == 0)
            throw new FileNotFoundException($"No required {string.Join(", ", extensions)} test images were found under '{testsRoot}'. Expected them under 'Input', 'Input/{preferredDirectoryName}', or '{preferredDirectoryName}'.");

        return results;
    }

    public static string WriteRgbaDdsOutput(Image image, string sourcePath, string category)
    {
        string projectDirectory = GetProjectDirectory();
        string? testsRoot = Environment.GetEnvironmentVariable("REDFOX_TESTS_DIR");
        string relativeSourcePath = GetRelativeInputPath(sourcePath, category, testsRoot);
        string outputPath = Path.Combine(projectDirectory, "OUTPUT", category, Path.ChangeExtension(relativeSourcePath, ".dds"));
        Directory.CreateDirectory(Path.GetDirectoryName(outputPath)!);

        Image rgbaImage = new(image.Info, image.PixelData.ToArray());
        rgbaImage.Convert(ImageFormat.R8G8B8A8Unorm);
        DdsWriter.Save(outputPath, rgbaImage);

        string failedInputPath = Path.Combine(projectDirectory, "OUTPUT", category, "Failed", relativeSourcePath);
        if (File.Exists(failedInputPath))
            File.Delete(failedInputPath);

        return outputPath;
    }

    public static string CopyFailedInputToOutput(string sourcePath, string category)
    {
        string projectDirectory = GetProjectDirectory();
        string relativeSourcePath = GetRelativeInputPath(sourcePath, category, Environment.GetEnvironmentVariable("REDFOX_TESTS_DIR"));
        string outputPath = Path.Combine(projectDirectory, "OUTPUT", category, "Failed", relativeSourcePath);
        Directory.CreateDirectory(Path.GetDirectoryName(outputPath)!);
        File.Copy(sourcePath, outputPath, overwrite: true);
        return outputPath;
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

    private static string GetProjectDirectory()
    {
        DirectoryInfo? directory = new(AppContext.BaseDirectory);
        while (directory is not null)
        {
            if (File.Exists(Path.Combine(directory.FullName, "RedFox.Tests.csproj")))
                return directory.FullName;

            directory = directory.Parent;
        }

        throw new DirectoryNotFoundException("Could not locate RedFox.Tests.csproj for test output.");
    }

    private static string GetRelativeInputPath(string sourcePath, string category, string? testsRoot)
    {
        string relativePath = Path.IsPathRooted(sourcePath) ? Path.GetRelativePath(testsRoot ?? throw new DirectoryNotFoundException("REDFOX_TESTS_DIR must be set to write output for corpus inputs."), sourcePath) : sourcePath;
        string[] segments = relativePath.Split([Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar], StringSplitOptions.RemoveEmptyEntries);
        int firstSegment = segments.Length > 0 && string.Equals(segments[0], "Input", StringComparison.OrdinalIgnoreCase) ? 1 : 0;
        if (firstSegment < segments.Length && string.Equals(segments[firstSegment], category, StringComparison.OrdinalIgnoreCase))
            firstSegment++;

        return Path.Combine(segments.Skip(firstSegment).ToArray());
    }
}
