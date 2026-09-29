using Spectre.Console;

namespace RedFox.GameExtraction.CommandLine;

internal static class WelcomeView
{
    private static readonly string[] FeaturedCommandNames = ["mount", "list", "extract", "set"];

    public static void Render(CommandLineSession session)
    {
        IAnsiConsole console = session.Console;
        CommandLineTheme theme = session.Theme;
        GameExtractionCommandLineConfig config = session.Config;
        string author = config.Author is null ? string.Empty : $" · {Markup.Escape(config.Author)}";

        console.WriteLine();

        if (config.Banner is null)
        {
            console.Write(new FigletText(config.Title) { Color = Style.Parse(theme.Accent).Foreground });
        }
        else
        {
            RenderBanner(console, config.Banner, theme);
            console.WriteLine();
        }

        console.MarkupLine($"[bold]{Markup.Escape(config.Title)}[/] [{theme.Muted}]v{Markup.Escape(config.Version)}{author}[/]");
        console.MarkupLine($"[{theme.Muted}]{Markup.Escape(config.Description)}[/]");
        console.WriteLine();

        Grid grid = new Grid().AddColumn(new GridColumn().PadLeft(2).PadRight(4)).AddColumn();

        foreach (ICommandLineCommand command in GetFeaturedCommands(session))
        {
            grid.AddRow($"[{theme.Accent}]/{Markup.Escape(command.Name)}[/]", $"[{theme.Muted}]{Markup.Escape(command.Description)}[/]");
        }

        console.Write(grid);
        console.WriteLine();
    }

    private static IEnumerable<ICommandLineCommand> GetFeaturedCommands(CommandLineSession session)
    {
        IEnumerable<ICommandLineCommand?> commands = [.. FeaturedCommandNames.Select(session.FindCommand), .. session.Config.Commands, session.FindCommand("help")];

        return commands.OfType<ICommandLineCommand>().Distinct();
    }

    private static void RenderBanner(IAnsiConsole console, string banner, CommandLineTheme theme)
    {
        string[] lines = banner.ReplaceLineEndings("\n").Trim('\n').Split('\n');
        Color start = Style.Parse(theme.Accent).Foreground;
        Color end = theme.BannerGradient is null ? start : Style.Parse(theme.BannerGradient).Foreground;

        for (int index = 0; index < lines.Length; index++)
        {
            float factor = lines.Length == 1 ? 0 : (float)index / (lines.Length - 1);
            console.WriteLine(lines[index], new Style(start.Blend(end, factor)));
        }
    }
}
