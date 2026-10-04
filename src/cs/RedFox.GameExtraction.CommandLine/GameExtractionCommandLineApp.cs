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
    public static Task<int> RunAsync(GameExtractionCommandLineConfig config, string[] args) => RunAsync(config, args, null);

    /// <summary>
    /// Runs the shell. Leading path arguments are mounted before the prompt opens. When the arguments
    /// contain slash commands, they are run in order and the application exits without prompting.
    /// The <c>--verbose</c> (<c>-v</c>) flag enables debug-level console and file logging, while
    /// <c>--quiet</c> (<c>-q</c>) limits logging to warnings and errors.
    /// </summary>
    /// <param name="config">The application configuration.</param>
    /// <param name="args">The command line arguments.</param>
    /// <param name="configureLogging">A callback that customizes the shared file logging behavior.</param>
    /// <returns>The process exit code: 0 on success, 1 on failure, and 130 when cancelled.</returns>
    public static async Task<int> RunAsync(GameExtractionCommandLineConfig config, string[] args, Action<GameExtractionLogOptions>? configureLogging)
    {
        ArgumentNullException.ThrowIfNull(config);
        ArgumentNullException.ThrowIfNull(args);

        config.Settings.LoadFrom(GameExtractionSettings.GetDefaultSettingsPath(config.AppName));

        GameExtractionLogOptions logOptions = new();
        string[] effectiveArgs = GameExtractionLogging.ApplyArguments(args, logOptions);
        configureLogging?.Invoke(logOptions);
        GameExtractionLogging.Configure(config.AppName, logOptions);
        GameExtractionLogging.ApplySettings(config.Settings);

        if (effectiveArgs is ["--mcp"])
        {
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

        return await new CommandLineShell(session).RunAsync(effectiveArgs).ConfigureAwait(false);
    }

    private static IReadOnlyList<ICommandLineCommand> CreateCommands(GameExtractionCommandLineConfig config)
    {
        ICommandLineCommand[] builtInCommands =
        [
            new HelpCommand(),
            new MountCommand(),
            new MountDirCommand(),
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
