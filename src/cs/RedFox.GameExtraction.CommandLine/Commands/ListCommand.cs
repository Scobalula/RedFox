using Spectre.Console;

namespace RedFox.GameExtraction.CommandLine.Commands;

internal sealed class ListCommand : ICommandLineCommand
{
    private const int MaximumRows = 100;

    public string Name => "list";

    public IReadOnlyList<string> Aliases => ["ls", "find"];

    public string Usage => "[pattern] [type:<type>]";

    public string Description => "List and search assets";

    public Task ExecuteAsync(CommandLineSession session, IReadOnlyList<string> arguments, CancellationToken cancellationToken)
    {
        AssetFilter filter = AssetFilter.Parse(arguments);
        List<Asset> assets = [.. session.Manager.Assets.Where(filter.Matches)];

        if (assets.Count == 0)
        {
            session.WriteMuted(session.Manager.Sources.Count == 0 ? "No sources mounted" : "No assets match");
            return Task.CompletedTask;
        }

        Table table = new Table().Border(TableBorder.Simple).BorderColor(Style.Parse(session.Theme.Muted).Foreground).AddColumn("Name").AddColumn("Type").AddColumn("Information");

        foreach (string column in session.Config.MetadataColumns)
        {
            table.AddColumn(column);
        }

        foreach (Asset asset in assets.Take(MaximumRows))
        {
            table.AddRow([FormatName(session, asset.Name), Markup.Escape(asset.Type), Markup.Escape(asset.Information ?? string.Empty), .. session.Config.MetadataColumns.Select(column => Markup.Escape(asset.Metadata.GetValueOrDefault(column)?.ToString() ?? string.Empty))]);
        }

        session.Console.Write(table);
        session.WriteMuted(assets.Count > MaximumRows ? $"Showing {MaximumRows:N0} of {assets.Count:N0} assets" : $"{assets.Count:N0} assets");

        return Task.CompletedTask;
    }

    public IEnumerable<string> GetCompletions(CommandLineSession session, IReadOnlyList<string> arguments) => AssetFilter.GetCompletions(session.Manager.Assets);

    private static string FormatName(CommandLineSession session, string name)
    {
        int separatorIndex = name.LastIndexOfAny(['/', '\\']);

        if (separatorIndex < 0)
        {
            return Markup.Escape(name);
        }

        return $"[{session.Theme.Muted}]{Markup.Escape(name[..(separatorIndex + 1)])}[/]{Markup.Escape(name[(separatorIndex + 1)..])}";
    }

}
