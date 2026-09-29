using Spectre.Console;

namespace RedFox.GameExtraction.CommandLine.Commands;

internal sealed class ExtractCommand : ICommandLineCommand
{
    public string Name => "extract";

    public IReadOnlyList<string> Aliases => ["x", "export"];

    public string Usage => "[pattern] [type:<type>]";

    public string Description => "Export assets using the current settings";

    public async Task ExecuteAsync(CommandLineSession session, IReadOnlyList<string> arguments, CancellationToken cancellationToken)
    {
        AssetFilter filter = AssetFilter.Parse(arguments);
        List<Asset> assets = [.. session.Manager.Assets.Where(filter.Matches)];

        if (assets.Count == 0)
        {
            throw new InvalidOperationException(session.Manager.Sources.Count == 0 ? "No sources mounted" : "No assets match");
        }

        if (filter.IsEmpty && session.IsInteractive && !session.Console.Confirm($"Export all {assets.Count:N0} assets?"))
        {
            return;
        }

        await session.ExportAsync(assets, cancellationToken).ConfigureAwait(false);
    }

    public IEnumerable<string> GetCompletions(CommandLineSession session, IReadOnlyList<string> arguments) => AssetFilter.GetCompletions(session.Manager.Assets);
}
