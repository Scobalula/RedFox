using Spectre.Console;
using Spectre.Console.Rendering;

namespace RedFox.GameExtraction.CommandLine.Commands;

internal sealed class InfoCommand : ICommandLineCommand
{
    private const int MaximumChartTypes = 7;

    private static readonly Color[] ChartColors = [Color.SteelBlue1, Color.MediumPurple1, Color.Gold1, Color.SpringGreen2, Color.LightCoral, Color.Aquamarine1, Color.Orchid];

    public string Name => "info";

    public IReadOnlyList<string> Aliases => ["status"];

    public string Description => "Show application and session details";

    public Task ExecuteAsync(CommandLineSession session, IReadOnlyList<string> arguments, CancellationToken cancellationToken)
    {
        GameExtractionCommandLineConfig config = session.Config;
        IReadOnlyList<Asset> assets = session.Manager.Assets;
        Grid grid = new Grid().AddColumn(new GridColumn().PadRight(3)).AddColumn();

        AddRow(session, grid, "Version", config.Version);

        if (config.Author is not null)
        {
            AddRow(session, grid, "Author", config.Author);
        }

        AddRow(session, grid, "Settings", session.SettingsPath);
        AddRow(session, grid, "Output", Path.GetFullPath(session.CreateExportConfiguration().OutputDirectory));
        AddRow(session, grid, "Readers", string.Join(", ", session.Manager.SourceReaders.Select(reader => reader.GetType().Name)));
        AddRow(session, grid, "Handlers", string.Join(", ", session.Manager.Handlers.Select(handler => handler.GetType().Name)));
        AddRow(session, grid, "Sources", $"{session.Manager.Sources.Count:N0}");
        AddRow(session, grid, "Assets", $"{assets.Count:N0}");

        IRenderable content = assets.Count == 0 ? grid : new Rows(grid, Text.Empty, CreateTypeChart(assets));
        Panel panel = new Panel(content).Header($" {Markup.Escape(config.Title)} ").Border(BoxBorder.Rounded).BorderColor(Style.Parse(session.Theme.Accent).Foreground).Padding(2, 1);

        session.Console.Write(panel);

        return Task.CompletedTask;
    }

    private static void AddRow(CommandLineSession session, Grid grid, string label, string value)
    {
        grid.AddRow($"[{session.Theme.Muted}]{label}[/]", Markup.Escape(value));
    }

    private static BreakdownChart CreateTypeChart(IReadOnlyList<Asset> assets)
    {
        List<(string Type, int Count)> types = [.. assets.GroupBy(asset => asset.Type, StringComparer.OrdinalIgnoreCase).Select(group => (group.Key, group.Count())).OrderByDescending(type => type.Item2)];
        BreakdownChart chart = new BreakdownChart().FullSize();

        for (int index = 0; index < Math.Min(types.Count, MaximumChartTypes); index++)
        {
            chart.AddItem(Markup.Escape(types[index].Type), types[index].Count, ChartColors[index]);
        }

        if (types.Count > MaximumChartTypes)
        {
            chart.AddItem("Other", types.Skip(MaximumChartTypes).Sum(type => type.Count), Color.Grey);
        }

        return chart;
    }
}
