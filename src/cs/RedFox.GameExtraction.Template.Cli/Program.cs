using RedFox.GameExtraction.Template;

namespace RedFox.GameExtraction.Template.Cli;

internal static class Program
{
    private const int PreviewByteCount = 32;

    private static async Task<int> Main(string[] arguments)
    {
        if (!CliArguments.TryParse(arguments, out CliArguments? parsedArguments, out string? error))
        {
            Console.Error.WriteLine(error);
            WriteUsage();
            return 1;
        }

        if (parsedArguments is null || parsedArguments.Command == CliCommand.Help)
        {
            WriteUsage();
            return 0;
        }

        AssetManager manager = TemplateAssetManagerFactory.Create();
        IAssetSource? source = null;

        try
        {
            Progress<string> progress = new(message => Console.WriteLine(message));
            source = await manager.MountFileAsync(
                parsedArguments.ZipPath!,
                null,
                progress,
                CancellationToken.None).ConfigureAwait(false);

            return parsedArguments.Command switch
            {
                CliCommand.List => WriteAssetList(source),
                CliCommand.Read => await ReadAssetAsync(manager, source, parsedArguments.AssetPath!).ConfigureAwait(false),
                CliCommand.Export => await ExportAssetsAsync(manager, source, parsedArguments.OutputDirectory!).ConfigureAwait(false),
                CliCommand.Vfs => WriteVirtualFileSystem(manager),
                _ => 1,
            };
        }
        catch (Exception exception)
        {
            Console.Error.WriteLine(exception.Message);
            return 1;
        }
        finally
        {
            if (source is not null)
            {
                await manager.UnloadAsync(source).ConfigureAwait(false);
            }
        }
    }

    private static int WriteAssetList(IAssetSource source)
    {
        Console.WriteLine($"Source: {source.Name}");
        Console.WriteLine($"Assets: {source.Assets.Count}");

        foreach (Asset asset in source.Assets.OrderBy(asset => asset.Name, StringComparer.OrdinalIgnoreCase))
        {
            Console.WriteLine($"{asset.Name} [{asset.Type}]");
        }

        return 0;
    }

    private static async Task<int> ReadAssetAsync(AssetManager manager, IAssetSource source, string assetPath)
    {
        if (!source.TryGetAsset(assetPath, out Asset? asset) || asset is null)
        {
            Console.Error.WriteLine($"Asset '{assetPath}' was not found in the archive.");
            return 1;
        }

        AssetReadResult result = await manager.ReadAsync(asset).ConfigureAwait(false);
        AssetReadResult<byte[]> byteResult = result as AssetReadResult<byte[]>
            ?? throw new InvalidOperationException("Template read operations must return byte[] results.");

        byte[] previewBytes = byteResult.Data.Take(PreviewByteCount).ToArray();
        Console.WriteLine($"Asset: {asset.Name}");
        Console.WriteLine($"Type: {asset.Type}");
        Console.WriteLine($"Size: {byteResult.Data.Length:N0} bytes");
        Console.WriteLine($"Preview ({previewBytes.Length} bytes): {Convert.ToHexString(previewBytes)}");
        return 0;
    }

    private static async Task<int> ExportAssetsAsync(AssetManager manager, IAssetSource source, string outputDirectory)
    {
        ExportConfiguration configuration = new()
        {
            OutputDirectory = Path.GetFullPath(outputDirectory),
            PreserveDirectoryStructure = true,
        };

        Progress<string> progress = new(message => Console.WriteLine(message));
        await manager.ExportAsync(source.Assets, configuration, progress).ConfigureAwait(false);
        Console.WriteLine($"Exported {source.Assets.Count} assets to {configuration.OutputDirectory}");
        return 0;
    }

    private static int WriteVirtualFileSystem(AssetManager manager)
    {
        if (!manager.TryGetService(out AssetFileSystemService? fileSystemService))
        {
            Console.Error.WriteLine("No virtual file system service is registered.");
            return 1;
        }

        IReadOnlyList<string> files = fileSystemService.FileSystem
            .EnumerateFiles(null, "*", SearchOption.AllDirectories)
            .Select(file => file.FullPath)
            .OrderBy(path => path, StringComparer.OrdinalIgnoreCase)
            .ToArray();

        Console.WriteLine($"VFS files: {files.Count}");
        foreach (string path in files)
        {
            Console.WriteLine(path);
        }

        return 0;
    }

    private static void WriteUsage()
    {
        Console.WriteLine("RedFox GameExtraction ZIP template CLI");
        Console.WriteLine("Usage:");
        Console.WriteLine("  RedFox.GameExtraction.Template.Cli list <zip-path>");
        Console.WriteLine("  RedFox.GameExtraction.Template.Cli read <zip-path> <asset-path>");
        Console.WriteLine("  RedFox.GameExtraction.Template.Cli export <zip-path> <output-directory>");
        Console.WriteLine("  RedFox.GameExtraction.Template.Cli vfs <zip-path>");
    }
}
