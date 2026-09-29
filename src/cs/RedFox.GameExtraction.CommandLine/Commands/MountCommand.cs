using System.Diagnostics;
using Spectre.Console;

namespace RedFox.GameExtraction.CommandLine.Commands;

internal sealed class MountCommand : ICommandLineCommand
{
    private const string ProcessKeyword = "process";

    public string Name => CommandLineTokenizer.MountCommandName;

    public IReadOnlyList<string> Aliases => ["m"];

    public string Usage => "<path…> | process [name|pid]";

    public string Description => "Mount files, folders, or a running process";

    public async Task ExecuteAsync(CommandLineSession session, IReadOnlyList<string> arguments, CancellationToken cancellationToken)
    {
        if (arguments.Count == 0)
        {
            throw new ArgumentException("Specify a path to mount");
        }

        if (session.Config.SupportsProcessSources && arguments[0].Equals(ProcessKeyword, StringComparison.OrdinalIgnoreCase))
        {
            await MountProcessAsync(session, arguments.Count > 1 ? arguments[1] : null, cancellationToken).ConfigureAwait(false);
            return;
        }

        foreach (string path in arguments)
        {
            await session.MountAsync(path, cancellationToken).ConfigureAwait(false);
        }
    }

    public IEnumerable<string> GetCompletions(CommandLineSession session, IReadOnlyList<string> arguments)
    {
        IEnumerable<string> paths = PathCompletion.GetCompletions(arguments[^1]);

        return arguments.Count == 1 && session.Config.SupportsProcessSources ? paths.Prepend(ProcessKeyword) : paths;
    }

    private static async Task MountProcessAsync(CommandLineSession session, string? target, CancellationToken cancellationToken)
    {
        IReadOnlyDictionary<string, object?> options = session.Config.SourceOptions;

        if (target is not null)
        {
            AssetSourceRequest request = int.TryParse(target, out int processId) ? AssetSourceRequest.ForProcess(processId, options) : AssetSourceRequest.ForProcess(target, options);
            await session.MountAsync(request, cancellationToken).ConfigureAwait(false);
            return;
        }

        List<AssetSourceRequest> candidates = FindProcessCandidates(session);

        if (candidates.Count == 0)
        {
            throw new InvalidOperationException("No supported processes are running");
        }

        if (candidates.Count > 1 && !session.IsInteractive)
        {
            throw new InvalidOperationException("Multiple supported processes are running, specify a name or PID");
        }

        AssetSourceRequest selected = candidates.Count == 1 ? candidates[0] : session.Console.Prompt(new SelectionPrompt<AssetSourceRequest>().Title("Select a process").HighlightStyle(Style.Parse(session.Theme.Accent)).UseConverter(request => Markup.Escape(request.Description)).AddChoices(candidates));

        await session.MountAsync(selected, cancellationToken).ConfigureAwait(false);
    }

    private static List<AssetSourceRequest> FindProcessCandidates(CommandLineSession session)
    {
        List<AssetSourceRequest> candidates = [];

        foreach (Process process in Process.GetProcesses())
        {
            using (process)
            {
                AssetSourceRequest request = AssetSourceRequest.ForProcess(process.Id, session.Config.SourceOptions);

                if (session.Manager.SourceReaders.Any(reader => CanOpen(reader, request)))
                {
                    candidates.Add(request);
                }
            }
        }

        return candidates;
    }

    private static bool CanOpen(IAssetSourceReader reader, AssetSourceRequest request)
    {
        try
        {
            return reader.CanOpen(request);
        }
        catch (Exception)
        {
            return false;
        }
    }
}
