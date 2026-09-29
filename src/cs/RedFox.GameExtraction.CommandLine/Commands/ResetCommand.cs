using Spectre.Console;

namespace RedFox.GameExtraction.CommandLine.Commands;

internal sealed class ResetCommand : ICommandLineCommand
{
    private const string AllKeyword = "all";

    public string Name => "reset";

    public string Usage => "<setting|all>";

    public string Description => "Restore settings to their defaults";

    public Task ExecuteAsync(CommandLineSession session, IReadOnlyList<string> arguments, CancellationToken cancellationToken)
    {
        if (arguments.Count == 0)
        {
            throw new ArgumentException("Specify a setting or all");
        }

        IReadOnlyList<GameExtractionSetting> settings = arguments[0].Equals(AllKeyword, StringComparison.OrdinalIgnoreCase) ? session.SettingDefinitions : [.. arguments.Select(argument => SetCommand.FindSetting(session, argument))];

        foreach (GameExtractionSetting setting in settings)
        {
            string? value = setting.DefaultValue?.ToString();

            session.Settings.SetSettingValue(setting, value);
            session.WriteSuccess($"{Markup.Escape(setting.Name)} [{session.Theme.Muted}]→[/] {SetCommand.FormatValue(session, value)}");
        }

        session.SaveSettings();

        return Task.CompletedTask;
    }

    public IEnumerable<string> GetCompletions(CommandLineSession session, IReadOnlyList<string> arguments) => session.SettingDefinitions.Select(setting => setting.Name).Prepend(AllKeyword);
}
