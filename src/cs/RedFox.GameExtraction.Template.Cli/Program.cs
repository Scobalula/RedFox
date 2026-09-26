using RedFox.GameExtraction.Template;

namespace RedFox.GameExtraction.Template.Cli;

internal static class Program
{
    private const int PreviewByteCount = 32;
    private const int CancelledExitCode = 130;

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

        using CancellationTokenSource cancellationSource = new();
        bool cancellationRequested = false;
        ConsoleCancelEventHandler cancelHandler = (_, eventArgs) =>
        {
            if (cancellationRequested)
            {
                eventArgs.Cancel = false;
                return;
            }

            cancellationRequested = true;
            eventArgs.Cancel = true;
            cancellationSource.Cancel();
            Console.Error.WriteLine("Cancellation requested; finishing the current operation...");
        };
        Console.CancelKeyPress += cancelHandler;

        AssetManager manager = TemplateAssetManagerFactory.Create();
        IAssetSource? source = null;

        try
        {
            Progress<string> progress = new(message => Console.WriteLine(message));
            source = await manager.MountFileAsync(parsedArguments.ZipPath!, null, progress, cancellationSource.Token).ConfigureAwait(false);

            return parsedArguments.Command switch
            {
                CliCommand.List => WriteAssetList(source),
                CliCommand.Read => await ReadAssetAsync(manager, source, parsedArguments.AssetPath!, cancellationSource.Token).ConfigureAwait(false),
                CliCommand.Export => await ExportAssetsAsync(manager, source, parsedArguments.OutputDirectory!, cancellationSource.Token).ConfigureAwait(false),
                CliCommand.Vfs => WriteVirtualFileSystem(manager),
                _ => 1,
            };
        }
        catch (OperationCanceledException) when (cancellationSource.IsCancellationRequested)
        {
            Console.Error.WriteLine("Conversion cancelled.");
            return CancelledExitCode;
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

            Console.CancelKeyPress -= cancelHandler;
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

    private static async Task<int> ReadAssetAsync(AssetManager manager, IAssetSource source, string assetPath, CancellationToken cancellationToken)
    {
        if (!source.TryGetAsset(assetPath, out Asset? asset) || asset is null)
        {
            Console.Error.WriteLine($"Asset '{assetPath}' was not found in the archive.");
            return 1;
        }

        AssetReadResult result = await manager.ReadAsync(asset, cancellationToken).ConfigureAwait(false);
        cancellationToken.ThrowIfCancellationRequested();

        byte[]? bytes = result.Data as byte[];
        Console.WriteLine($"Asset: {asset.Name}");
        Console.WriteLine($"Type: {asset.Type}");
        Console.WriteLine($"Payload: {result.Data?.GetType().Name ?? "None"}");
        if (bytes is not null)
        {
            byte[] previewBytes = bytes.Take(PreviewByteCount).ToArray();
            Console.WriteLine($"Size: {bytes.Length:N0} bytes");
            Console.WriteLine($"Preview ({previewBytes.Length} bytes): {Convert.ToHexString(previewBytes)}");
        }
        return 0;
    }

    private static async Task<int> ExportAssetsAsync(AssetManager manager, IAssetSource source, string outputDirectory, CancellationToken cancellationToken)
    {
        ExportConfiguration configuration = new()
        {
            OutputDirectory = Path.GetFullPath(outputDirectory),
            PreserveDirectoryStructure = true,
        };

        Progress<string> progress = new(message => Console.WriteLine(message));
        await manager.ExportAsync(source.Assets, configuration, progress, cancellationToken).ConfigureAwait(false);
        cancellationToken.ThrowIfCancellationRequested();
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
