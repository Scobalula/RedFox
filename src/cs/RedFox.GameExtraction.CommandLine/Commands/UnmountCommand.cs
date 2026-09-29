using Spectre.Console;

namespace RedFox.GameExtraction.CommandLine.Commands;

internal sealed class UnmountCommand : ICommandLineCommand
{
    private const string AllKeyword = "all";

    public string Name => "unmount";

    public IReadOnlyList<string> Aliases => ["u"];

    public string Usage => "<number|name|all>";

    public string Description => "Unload mounted sources";

    public async Task ExecuteAsync(CommandLineSession session, IReadOnlyList<string> arguments, CancellationToken cancellationToken)
    {
        if (arguments.Count == 0)
        {
            throw new ArgumentException("Specify a source number, name, or all");
        }

        IReadOnlyList<IAssetSource> sources = session.Manager.Sources;
        IReadOnlyList<IAssetSource> selected = arguments[0].Equals(AllKeyword, StringComparison.OrdinalIgnoreCase) ? sources : [.. arguments.Select(argument => FindSource(sources, argument))];

        foreach (IAssetSource source in selected)
        {
            await session.Manager.UnloadAsync(source, cancellationToken).ConfigureAwait(false);
            session.WriteSuccess($"Unmounted {Markup.Escape(source.Name)}");
        }
    }

    public IEnumerable<string> GetCompletions(CommandLineSession session, IReadOnlyList<string> arguments) => session.Manager.Sources.Select(source => source.Name).Prepend(AllKeyword);

    private static IAssetSource FindSource(IReadOnlyList<IAssetSource> sources, string argument)
    {
        if (int.TryParse(argument, out int number) && number >= 1 && number <= sources.Count)
        {
            return sources[number - 1];
        }

        return sources.FirstOrDefault(source => source.Name.Equals(argument, StringComparison.OrdinalIgnoreCase)) ?? throw new ArgumentException($"No mounted source matches {argument}");
    }
}
