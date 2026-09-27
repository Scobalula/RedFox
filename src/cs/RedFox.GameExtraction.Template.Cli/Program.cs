using RedFox.GameExtraction.Template;

namespace RedFox.GameExtraction.Template.Cli;

internal static class Program
{
    private const int PreviewByteCount = 32;
    private const int CancelledExitCode = 130;
    private static readonly IAnsiConsole ErrorConsole = AnsiConsole.Create(new AnsiConsoleSettings { Out = new AnsiConsoleOutput(Console.Error) });
    private static readonly Style ErrorStyle = Style.Parse("red");

    private static async Task<int> Main(string[] arguments)
    {
        if (!CliArguments.TryParse(arguments, out CliArguments? parsedArguments, out string? error))
        {
            ErrorConsole.WriteLine(error, ErrorStyle);
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
            ErrorConsole.WriteLine("Cancellation requested; finishing the current operation...", ErrorStyle);
        };
        Console.CancelKeyPress += cancelHandler;

        AssetManager manager = TemplateAssetManagerFactory.Create();
        IAssetSource? source = null;

        try
        {
            Progress<string> progress = new(message => AnsiConsole.WriteLine(message));
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
            ErrorConsole.WriteLine("Conversion cancelled.", ErrorStyle);
            return CancelledExitCode;
        }
        catch (Exception exception)
        {
            ErrorConsole.WriteLine(exception.Message, ErrorStyle);
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
        AnsiConsole.WriteLine($"Source: {source.Name}");
        AnsiConsole.WriteLine($"Assets: {source.Assets.Count}");

        foreach (Asset asset in source.Assets.OrderBy(asset => asset.Name, StringComparer.OrdinalIgnoreCase))
        {
            AnsiConsole.WriteLine($"{asset.Name} [{asset.Type}]");
        }

        return 0;
    }

    private static async Task<int> ReadAssetAsync(AssetManager manager, IAssetSource source, string assetPath, CancellationToken cancellationToken)
    {
        if (!source.TryGetAsset(assetPath, out Asset? asset) || asset is null)
        {
            ErrorConsole.WriteLine($"Asset '{assetPath}' was not found in the archive.", ErrorStyle);
            return 1;
        }

        AssetReadResult result = await manager.ReadAsync(asset, cancellationToken).ConfigureAwait(false);
        cancellationToken.ThrowIfCancellationRequested();

        byte[]? bytes = result.Data as byte[];
        AnsiConsole.WriteLine($"Asset: {asset.Name}");
        AnsiConsole.WriteLine($"Type: {asset.Type}");
        AnsiConsole.WriteLine($"Payload: {result.Data?.GetType().Name ?? "None"}");
        if (bytes is not null)
        {
            byte[] previewBytes = bytes.Take(PreviewByteCount).ToArray();
            AnsiConsole.WriteLine($"Size: {bytes.Length:N0} bytes");
            AnsiConsole.WriteLine($"Preview ({previewBytes.Length} bytes): {Convert.ToHexString(previewBytes)}");
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

        Progress<string> progress = new(message => AnsiConsole.WriteLine(message));
        await manager.ExportAsync(source.Assets, configuration, progress, cancellationToken).ConfigureAwait(false);
        cancellationToken.ThrowIfCancellationRequested();
        AnsiConsole.WriteLine($"Exported {source.Assets.Count} assets to {configuration.OutputDirectory}");
        return 0;
    }

    private static int WriteVirtualFileSystem(AssetManager manager)
    {
        if (!manager.TryGetService(out AssetFileSystemService? fileSystemService))
        {
            ErrorConsole.WriteLine("No virtual file system service is registered.", ErrorStyle);
            return 1;
        }

        IReadOnlyList<string> files = fileSystemService.FileSystem
            .EnumerateFiles(null, "*", SearchOption.AllDirectories)
            .Select(file => file.FullPath)
            .OrderBy(path => path, StringComparer.OrdinalIgnoreCase)
            .ToArray();

        AnsiConsole.WriteLine($"VFS files: {files.Count}");
        foreach (string path in files)
        {
            AnsiConsole.WriteLine(path);
        }

        return 0;
    }

    private static void WriteUsage()
    {
        AnsiConsole.WriteLine("RedFox GameExtraction ZIP template CLI");
        AnsiConsole.WriteLine("Usage:");
        AnsiConsole.WriteLine("  RedFox.GameExtraction.Template.Cli list <zip-path>");
        AnsiConsole.WriteLine("  RedFox.GameExtraction.Template.Cli read <zip-path> <asset-path>");
        AnsiConsole.WriteLine("  RedFox.GameExtraction.Template.Cli export <zip-path> <output-directory>");
        AnsiConsole.WriteLine("  RedFox.GameExtraction.Template.Cli vfs <zip-path>");
    }
}
