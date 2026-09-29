using Spectre.Console;

namespace RedFox.GameExtraction.CommandLine.Commands;

internal sealed class SetCommand : ICommandLineCommand
{
    public string Name => "set";

    public IReadOnlyList<string> Aliases => ["settings", "config"];

    public string Usage => "[setting] [value]";

    public string Description => "View or change settings";

    public Task ExecuteAsync(CommandLineSession session, IReadOnlyList<string> arguments, CancellationToken cancellationToken)
    {
        if (arguments.Count == 0)
        {
            WriteSettings(session);
            return Task.CompletedTask;
        }

        GameExtractionSetting setting = FindSetting(session, arguments[0]);

        if (arguments.Count == 1)
        {
            WriteSetting(session, setting);
            return Task.CompletedTask;
        }

        string value = SettingValueParser.Parse(setting, string.Join(' ', arguments.Skip(1)));

        session.Settings.SetSettingValue(setting, value);
        session.SaveSettings();
        session.WriteSuccess($"{Markup.Escape(setting.Name)} [{session.Theme.Muted}]→[/] {FormatValue(session, value)}");

        return Task.CompletedTask;
    }

    public IEnumerable<string> GetCompletions(CommandLineSession session, IReadOnlyList<string> arguments)
    {
        if (arguments.Count == 1)
        {
            return session.SettingDefinitions.Select(setting => setting.Name);
        }

        GameExtractionSetting? setting = session.SettingDefinitions.FirstOrDefault(setting => setting.Name.Equals(arguments[0], StringComparison.OrdinalIgnoreCase));

        return arguments.Count == 2 && setting is not null ? SettingValueParser.GetCompletions(setting, arguments[1]) : [];
    }

    internal static GameExtractionSetting FindSetting(CommandLineSession session, string name)
    {
        return session.SettingDefinitions.FirstOrDefault(setting => setting.Name.Equals(name, StringComparison.OrdinalIgnoreCase)) ?? throw new ArgumentException($"Unknown setting {name}");
    }

    internal static string FormatValue(CommandLineSession session, string? value)
    {
        if (string.IsNullOrEmpty(value))
        {
            return $"[{session.Theme.Muted}]not set[/]";
        }

        if (bool.TryParse(value, out bool enabled))
        {
            return enabled ? $"[{session.Theme.Success}]{value}[/]" : $"[{session.Theme.Muted}]{value}[/]";
        }

        return Markup.Escape(value);
    }

    private static void WriteSettings(CommandLineSession session)
    {
        foreach (IGrouping<string, GameExtractionSetting> group in session.SettingDefinitions.GroupBy(setting => setting.Group ?? setting.Category))
        {
            Grid grid = new Grid().AddColumn(new GridColumn().PadLeft(2).PadRight(3)).AddColumn(new GridColumn().PadRight(3)).AddColumn();

            foreach (GameExtractionSetting setting in group)
            {
                grid.AddRow($"[{session.Theme.Accent}]{Markup.Escape(setting.Name)}[/]", FormatValue(session, session.Settings.GetSettingValue(setting)), $"[{session.Theme.Muted}]{Markup.Escape(setting.Label ?? string.Empty)}[/]");
            }

            session.Console.MarkupLine($"[bold]{Markup.Escape(group.Key)}[/]");
            session.Console.Write(grid);
        }
    }

    private static void WriteSetting(CommandLineSession session, GameExtractionSetting setting)
    {
        Grid grid = new Grid().AddColumn(new GridColumn().PadLeft(2).PadRight(3)).AddColumn();

        grid.AddRow($"[{session.Theme.Muted}]Value[/]", FormatValue(session, session.Settings.GetSettingValue(setting)));
        grid.AddRow($"[{session.Theme.Muted}]Default[/]", FormatValue(session, setting.DefaultValue?.ToString()));

        if (setting.Type is GameExtractionSettingType.Boolean or GameExtractionSettingType.Choice)
        {
            grid.AddRow($"[{session.Theme.Muted}]Options[/]", Markup.Escape(string.Join(", ", SettingValueParser.GetCompletions(setting, string.Empty))));
        }

        session.Console.MarkupLine($"[{session.Theme.Accent}]{Markup.Escape(setting.Name)}[/] {Markup.Escape(setting.Label ?? string.Empty)}");

        if (setting.Description is not null)
        {
            session.WriteMuted($"  {Markup.Escape(setting.Description)}");
        }

        session.Console.Write(grid);
    }
}
