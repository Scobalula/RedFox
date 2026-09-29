using Spectre.Console;

namespace RedFox.GameExtraction.CommandLine.Commands;

internal sealed class HelpCommand : ICommandLineCommand
{
    public string Name => "help";

    public IReadOnlyList<string> Aliases => ["?"];

    public string Usage => "[command]";

    public string Description => "Show available commands";

    public Task ExecuteAsync(CommandLineSession session, IReadOnlyList<string> arguments, CancellationToken cancellationToken)
    {
        if (arguments.Count == 0)
        {
            WriteCommands(session);
        }
        else
        {
            WriteCommand(session, arguments[0].TrimStart('/'));
        }

        return Task.CompletedTask;
    }

    public IEnumerable<string> GetCompletions(CommandLineSession session, IReadOnlyList<string> arguments) => arguments.Count == 1 ? session.Commands.Select(command => command.Name) : [];

    private static void WriteCommands(CommandLineSession session)
    {
        Grid grid = new Grid().AddColumn(new GridColumn().PadLeft(2).PadRight(2)).AddColumn(new GridColumn().PadRight(4)).AddColumn();

        foreach (ICommandLineCommand command in session.Commands)
        {
            grid.AddRow($"[{session.Theme.Accent}]/{Markup.Escape(command.Name)}[/]", $"[{session.Theme.Muted}]{Markup.Escape(command.Usage)}[/]", Markup.Escape(command.Description));
        }

        session.Console.Write(grid);
    }

    private static void WriteCommand(CommandLineSession session, string name)
    {
        ICommandLineCommand command = session.FindCommand(name) ?? throw new ArgumentException($"Unknown command /{name}");

        session.Console.MarkupLine($"[{session.Theme.Accent}]/{Markup.Escape(command.Name)}[/] [{session.Theme.Muted}]{Markup.Escape(command.Usage)}[/]");
        session.Console.MarkupLine($"  {Markup.Escape(command.Description)}");

        if (command.Aliases.Count > 0)
        {
            session.WriteMuted($"  Aliases: {Markup.Escape(string.Join(", ", command.Aliases.Select(alias => "/" + alias)))}");
        }
    }
}
