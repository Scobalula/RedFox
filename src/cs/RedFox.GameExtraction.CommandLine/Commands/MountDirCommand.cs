using Microsoft.Extensions.Logging;
using Spectre.Console;

namespace RedFox.GameExtraction.CommandLine.Commands;

/// <summary>
/// Mounts every supported file in a directory as its own source.
/// </summary>
internal sealed class MountDirCommand : ICommandLineCommand
{
    private const int MaximumFailuresShown = 20;

    public string Name => "mountdir";

    public string Usage => "<directory…>";

    public string Description => "Mount every supported file in a folder as its own source";

    public async Task ExecuteAsync(CommandLineSession session, IReadOnlyList<string> arguments, CancellationToken cancellationToken)
    {
        if (arguments.Count == 0)
        {
            throw new ArgumentException("Specify a directory to mount");
        }

        if (!session.Config.SupportsFileSources)
        {
            throw new NotSupportedException("File sources are not supported");
        }

        foreach (string path in arguments)
        {
            await MountDirectoryAsync(session, path, cancellationToken).ConfigureAwait(false);
        }
    }

    public IEnumerable<string> GetCompletions(CommandLineSession session, IReadOnlyList<string> arguments) => PathCompletion.GetCompletions(arguments[^1]);

    private static async Task MountDirectoryAsync(CommandLineSession session, string path, CancellationToken cancellationToken)
    {
        string fullPath = Path.GetFullPath(path);

        if (!Directory.Exists(fullPath))
        {
            throw new DirectoryNotFoundException($"Directory not found: {path}");
        }

        EnumerationOptions enumerationOptions = new()
        {
            RecurseSubdirectories = true,
            IgnoreInaccessible = true,
            AttributesToSkip = 0,
        };

        List<string> files = [];

        foreach (string file in Directory.EnumerateFiles(fullPath, "*", enumerationOptions))
        {
            cancellationToken.ThrowIfCancellationRequested();

            if (session.Manager.FindSourceReader(AssetSourceRequest.ForFile(file, session.Config.SourceOptions)) is not null)
            {
                files.Add(file);
            }
        }

        if (files.Count == 0)
        {
            throw new FileNotFoundException($"No supported files found in {path}");
        }

        files.Sort(StringComparer.OrdinalIgnoreCase);

        session.Manager.Logger.LogInformation("Mounting {FileCount} supported files from {Directory}", files.Count, fullPath);

        int mounted = 0;
        List<(string File, Exception Error)> failures = [];

        foreach (string file in files)
        {
            try
            {
                await session.MountAsync(AssetSourceRequest.ForFile(file, session.Config.SourceOptions), cancellationToken).ConfigureAwait(false);
                mounted++;
            }
            catch (Exception exception) when (exception is not OperationCanceledException)
            {
                failures.Add((file, exception));

                if (failures.Count <= MaximumFailuresShown)
                {
                    session.WriteError($"{Markup.Escape(Path.GetFileName(file))} [{session.Theme.Muted}]· {Markup.Escape(exception.Message)}[/]");
                }
            }
        }

        if (failures.Count > MaximumFailuresShown)
        {
            session.WriteMuted($"… {failures.Count - MaximumFailuresShown:N0} more failures");
        }

        if (failures.Count == 0)
        {
            session.WriteSuccess($"Mounted {mounted:N0} sources from {Markup.Escape(fullPath)}");
        }
        else
        {
            session.WriteWarning($"Mounted {mounted:N0} of {files.Count:N0} files [{session.Theme.Muted}]· {failures.Count:N0} failed[/]");
        }
    }
}
