using System.Text;
using RedFox.GameExtraction.CommandLine.Commands;
using RedFox.GameExtraction.Mcp;
using Spectre.Console;

namespace RedFox.GameExtraction.CommandLine;

/// <summary>
/// Runs a GameExtraction interactive command line application.
/// </summary>
public static class GameExtractionCommandLineApp
{
    /// <summary>
    /// Runs the shell. Leading path arguments are mounted before the prompt opens. When the arguments
    /// contain slash commands, they are run in order and the application exits without prompting.
    /// </summary>
    /// <param name="config">The application configuration.</param>
    /// <param name="args">The command line arguments.</param>
    /// <returns>The process exit code: 0 on success, 1 on failure, and 130 when cancelled.</returns>
    public static async Task<int> RunAsync(GameExtractionCommandLineConfig config, string[] args)
    {
        ArgumentNullException.ThrowIfNull(config);
        ArgumentNullException.ThrowIfNull(args);

        if (args is ["--mcp"])
        {
            config.Settings.LoadFrom(GameExtractionSettings.GetDefaultSettingsPath(config.AppName));

            await GameExtractionMcpServer.RunAsync(new GameExtractionMcpConfig
            {
                AssetManagerFactory = config.AssetManagerFactory,
                Configuration = CreateConfiguration(config),
                Name = config.AppName,
                Version = config.Version,
            }).ConfigureAwait(false);

            return 0;
        }

        Console.OutputEncoding = Encoding.UTF8;

        CommandLineSession session = new(config, config.AssetManagerFactory(), AnsiConsole.Console, CreateCommands(config));
        config.Settings.LoadFrom(session.SettingsPath);

        return await new CommandLineShell(session).RunAsync(args).ConfigureAwait(false);
    }

    private static IReadOnlyList<ICommandLineCommand> CreateCommands(GameExtractionCommandLineConfig config)
    {
        ICommandLineCommand[] builtInCommands =
        [
            new HelpCommand(),
            new MountCommand(),
            new UnmountCommand(),
            new SourcesCommand(),
            new ListCommand(),
            new ExtractCommand(),
            new SetCommand(),
            new ResetCommand(),
            new InfoCommand(),
            new OpenCommand(),
            new ClearCommand(),
            new ExitCommand(),
        ];

        HashSet<string> customNames = new(config.Commands.SelectMany(command => command.Aliases.Prepend(command.Name)), StringComparer.OrdinalIgnoreCase);

        return [.. builtInCommands.Where(command => !customNames.Contains(command.Name)), .. config.Commands];
    }

    internal static GameExtractionConfiguration CreateConfiguration(GameExtractionCommandLineConfig config)
    {
        GameExtractionConfiguration configuration = new();
        configuration.ApplySettings(config.Settings, config.SettingDefinitions);
        return configuration;
    }
}
