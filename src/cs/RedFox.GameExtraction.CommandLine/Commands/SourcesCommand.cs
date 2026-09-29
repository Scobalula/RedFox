using Spectre.Console;

namespace RedFox.GameExtraction.CommandLine.Commands;

internal sealed class SourcesCommand : ICommandLineCommand
{
    public string Name => "sources";

    public string Description => "List mounted sources";

    public Task ExecuteAsync(CommandLineSession session, IReadOnlyList<string> arguments, CancellationToken cancellationToken)
    {
        IReadOnlyList<IAssetSource> sources = session.Manager.Sources;

        if (sources.Count == 0)
        {
            session.WriteMuted("No sources mounted");
            return Task.CompletedTask;
        }

        Table table = new Table().Border(TableBorder.Simple).BorderColor(Style.Parse(session.Theme.Muted).Foreground).AddColumn("#").AddColumn("Name").AddColumn("Kind").AddColumn("Location").AddColumn(new TableColumn("Assets").RightAligned());

        for (int index = 0; index < sources.Count; index++)
        {
            IAssetSource source = sources[index];
            session.Manager.TryGetSourceRequest(source, out AssetSourceRequest? request);

            table.AddRow($"[{session.Theme.Muted}]{index + 1}[/]", Markup.Escape(source.Name), request?.Kind.ToString() ?? string.Empty, Markup.Escape(request?.Location ?? request?.DisplayName ?? string.Empty), $"{source.Assets.Count:N0}");
        }

        session.Console.Write(table);

        return Task.CompletedTask;
    }
}
