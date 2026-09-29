using System.Diagnostics;
using Spectre.Console;

namespace RedFox.GameExtraction.CommandLine;

/// <summary>
/// Provides the state and shared operations of a running command line shell to commands and callbacks.
/// </summary>
/// <param name="config">The application configuration.</param>
/// <param name="manager">The asset manager powering the shell.</param>
/// <param name="console">The console used for output.</param>
/// <param name="commands">The commands available in the shell.</param>
public sealed class CommandLineSession(GameExtractionCommandLineConfig config, AssetManager manager, IAnsiConsole console, IReadOnlyList<ICommandLineCommand> commands)
{
    private const int MaximumFailuresShown = 20;

    /// <summary>
    /// Gets the application configuration.
    /// </summary>
    public GameExtractionCommandLineConfig Config { get; } = config;

    /// <summary>
    /// Gets the asset manager powering the shell.
    /// </summary>
    public AssetManager Manager { get; } = manager;

    /// <summary>
    /// Gets the console used for output.
    /// </summary>
    public IAnsiConsole Console { get; } = console;

    /// <summary>
    /// Gets the commands available in the shell.
    /// </summary>
    public IReadOnlyList<ICommandLineCommand> Commands { get; } = commands;

    /// <summary>
    /// Gets the color scheme used by the shell.
    /// </summary>
    public CommandLineTheme Theme => Config.Theme;

    /// <summary>
    /// Gets the persisted settings.
    /// </summary>
    public GameExtractionSettings Settings => Config.Settings;

    /// <summary>
    /// Gets the path the settings are persisted to.
    /// </summary>
    public string SettingsPath { get; } = GameExtractionSettings.GetDefaultSettingsPath(config.AppName);

    /// <summary>
    /// Gets a value indicating whether the shell is reading commands from the user rather than running a script.
    /// </summary>
    public bool IsInteractive { get; internal set; }

    /// <summary>
    /// Gets a value indicating whether the shell will exit after the current command.
    /// </summary>
    public bool IsExitRequested { get; private set; }

    /// <summary>
    /// Finds a command by name or alias.
    /// </summary>
    /// <param name="name">The command name or alias, without the leading slash.</param>
    /// <returns>The command, or <see langword="null"/> when none matches.</returns>
    public ICommandLineCommand? FindCommand(string name)
    {
        return Commands.FirstOrDefault(command => command.Name.Equals(name, StringComparison.OrdinalIgnoreCase)) ?? Commands.FirstOrDefault(command => command.Aliases.Contains(name, StringComparer.OrdinalIgnoreCase));
    }

    /// <summary>
    /// Requests the shell to exit after the current command.
    /// </summary>
    public void RequestExit() => IsExitRequested = true;

    /// <summary>
    /// Builds an export configuration from the current settings.
    /// </summary>
    /// <returns>The export configuration.</returns>
    public ExportConfiguration CreateExportConfiguration() => Config.ExportConfigurationFactory(Config.Settings);

    /// <summary>
    /// Persists the current settings.
    /// </summary>
    public void SaveSettings() => Config.Settings.Save(SettingsPath);

    /// <summary>
    /// Mounts a file or directory. Wildcards in the file name mount every matching file.
    /// </summary>
    /// <param name="path">The path to mount.</param>
    /// <param name="cancellationToken">The cancellation token for the operation.</param>
    /// <returns>The mounted sources.</returns>
    public async Task<IReadOnlyList<IAssetSource>> MountAsync(string path, CancellationToken cancellationToken)
    {
        List<IAssetSource> sources = [];

        foreach (string resolvedPath in ResolvePaths(path))
        {
            sources.Add(await MountAsync(CreateRequest(resolvedPath), cancellationToken).ConfigureAwait(false));
        }

        return sources;
    }

    /// <summary>
    /// Mounts a source request while displaying its progress, then invokes <see cref="GameExtractionCommandLineConfig.SourceMounted"/>.
    /// </summary>
    /// <param name="request">The source request.</param>
    /// <param name="cancellationToken">The cancellation token for the operation.</param>
    /// <returns>The mounted source.</returns>
    public async Task<IAssetSource> MountAsync(AssetSourceRequest request, CancellationToken cancellationToken)
    {
        Stopwatch stopwatch = Stopwatch.StartNew();

        IAssetSource source = await Console.Status().SpinnerStyle(Style.Parse(Theme.Accent)).StartAsync($"Mounting {Markup.Escape(request.DisplayName)}", context => Manager.MountAsync(request, new Progress<string>(message => context.Status = Markup.Escape(message)), cancellationToken)).ConfigureAwait(false);

        if (Config.SourceMounted is not null)
        {
            await Config.SourceMounted(this, source, cancellationToken).ConfigureAwait(false);
        }

        WriteSuccess($"{Markup.Escape(source.Name)} [{Theme.Muted}]· {source.Assets.Count:N0} assets · {stopwatch.Elapsed.TotalSeconds:0.0}s[/]");

        return source;
    }

    /// <summary>
    /// Unloads and mounts every source again, for example after name tables used during mounting have changed.
    /// </summary>
    /// <param name="cancellationToken">The cancellation token for the operation.</param>
    /// <returns>A task representing the operation.</returns>
    public async Task RemountAsync(CancellationToken cancellationToken)
    {
        IReadOnlyList<IAssetSource> sources = Manager.Sources;
        List<AssetSourceRequest> requests = [.. sources.Select(Manager.GetRequiredSourceRequest)];

        foreach (IAssetSource source in sources)
        {
            await Manager.UnloadAsync(source, cancellationToken).ConfigureAwait(false);
        }

        foreach (AssetSourceRequest request in requests)
        {
            await MountAsync(request, cancellationToken).ConfigureAwait(false);
        }
    }

    /// <summary>
    /// Exports assets using the current settings while displaying a progress bar, then writes a summary.
    /// Failed assets are reported without stopping the export.
    /// </summary>
    /// <param name="assets">The assets to export.</param>
    /// <param name="cancellationToken">The cancellation token for the operation.</param>
    /// <returns>A task representing the operation.</returns>
    public async Task ExportAsync(IReadOnlyList<Asset> assets, CancellationToken cancellationToken)
    {
        ExportConfiguration configuration = CreateExportConfiguration();
        List<(Asset Asset, Exception Exception)> failures = [];
        Stopwatch stopwatch = Stopwatch.StartNew();
        Asset? currentAsset = null;
        int skipped = 0;

        void OnAssetExportCompleted(object? sender, AssetExportCompletedEventArgs eventArgs)
        {
            if (eventArgs.Skipped && ReferenceEquals(eventArgs.Asset, currentAsset))
            {
                skipped++;
            }
        }

        Manager.AssetExportCompleted += OnAssetExportCompleted;

        try
        {
            await CreateProgress().StartAsync(async context =>
            {
                ProgressTask task = context.AddTask("Exporting", maxValue: assets.Count);

                foreach (Asset asset in assets)
                {
                    currentAsset = asset;
                    task.Description = Markup.Escape(Path.GetFileName(asset.Name));

                    try
                    {
                        await Manager.ExportAsync(asset, configuration, cancellationToken).ConfigureAwait(false);
                    }
                    catch (Exception exception) when (exception is not OperationCanceledException)
                    {
                        failures.Add((asset, exception));
                    }

                    task.Increment(1);
                }
            }).ConfigureAwait(false);
        }
        finally
        {
            Manager.AssetExportCompleted -= OnAssetExportCompleted;
        }

        WriteExportSummary(assets.Count - skipped - failures.Count, skipped, failures, stopwatch.Elapsed, configuration.OutputDirectory);
    }

    /// <summary>
    /// Creates a progress display styled with the current theme.
    /// </summary>
    /// <returns>The progress display.</returns>
    public Progress CreateProgress()
    {
        ProgressBarColumn progressBar = new()
        {
            CompletedStyle = Style.Parse(Theme.Accent),
            FinishedStyle = Style.Parse(Theme.Success),
            RemainingStyle = Style.Parse(Theme.Muted),
        };

        return Console.Progress().AutoClear(true).Columns(new TaskDescriptionColumn { Alignment = Justify.Left }, progressBar, new PercentageColumn(), new RemainingTimeColumn(), new SpinnerColumn { Style = Style.Parse(Theme.Accent) });
    }

    /// <summary>
    /// Writes a line prefixed with a success mark.
    /// </summary>
    /// <param name="markup">The markup to write.</param>
    public void WriteSuccess(string markup) => Console.MarkupLine($"[{Theme.Success}]✔[/] {markup}");

    /// <summary>
    /// Writes a line prefixed with a warning mark.
    /// </summary>
    /// <param name="markup">The markup to write.</param>
    public void WriteWarning(string markup) => Console.MarkupLine($"[{Theme.Warning}]▲ {markup}[/]");

    /// <summary>
    /// Writes a line prefixed with an error mark.
    /// </summary>
    /// <param name="markup">The markup to write.</param>
    public void WriteError(string markup) => Console.MarkupLine($"[{Theme.Error}]✖ {markup}[/]");

    /// <summary>
    /// Writes a line in the muted color.
    /// </summary>
    /// <param name="markup">The markup to write.</param>
    public void WriteMuted(string markup) => Console.MarkupLine($"[{Theme.Muted}]{markup}[/]");

    private AssetSourceRequest CreateRequest(string path)
    {
        if (Directory.Exists(path))
        {
            if (!Config.SupportsDirectorySources)
            {
                throw new NotSupportedException($"Directory sources are not supported: {path}");
            }

            return AssetSourceRequest.ForDirectory(path, Config.SourceOptions);
        }

        if (!File.Exists(path))
        {
            throw new FileNotFoundException($"Path not found: {path}");
        }

        if (!Config.SupportsFileSources)
        {
            throw new NotSupportedException($"File sources are not supported: {path}");
        }

        return AssetSourceRequest.ForFile(path, Config.SourceOptions);
    }

    private void WriteExportSummary(int exported, int skipped, List<(Asset Asset, Exception Exception)> failures, TimeSpan elapsed, string outputDirectory)
    {
        string skippedText = skipped == 0 ? string.Empty : $" [{Theme.Muted}]· {skipped:N0} skipped[/]";
        string failedText = failures.Count == 0 ? string.Empty : $" · [{Theme.Error}]{failures.Count:N0} failed[/]";
        string fullOutputDirectory = Path.GetFullPath(outputDirectory);

        WriteSuccess($"Exported {exported:N0}{skippedText}{failedText} [{Theme.Muted}]· {elapsed.TotalSeconds:0.0}s[/]");
        Console.MarkupLine($"  [{Theme.Muted}]→[/] [link={new Uri(fullOutputDirectory).AbsoluteUri}]{Markup.Escape(fullOutputDirectory)}[/]");

        if (failures.Count == 0)
        {
            return;
        }

        Table table = new Table().Border(TableBorder.Simple).BorderColor(Style.Parse(Theme.Muted).Foreground).AddColumn("Asset").AddColumn("Error");

        foreach ((Asset asset, Exception exception) in failures.Take(MaximumFailuresShown))
        {
            table.AddRow(Markup.Escape(asset.Name), $"[{Theme.Error}]{Markup.Escape(exception.Message)}[/]");
        }

        if (failures.Count > MaximumFailuresShown)
        {
            table.AddRow($"[{Theme.Muted}]… {failures.Count - MaximumFailuresShown:N0} more[/]", string.Empty);
        }

        Console.Write(table);
    }

    private static IEnumerable<string> ResolvePaths(string path)
    {
        string fullPath = Path.GetFullPath(path);
        string fileName = Path.GetFileName(fullPath);

        if (fileName.AsSpan().IndexOfAny('*', '?') < 0)
        {
            return [fullPath];
        }

        string directory = Path.GetDirectoryName(fullPath) ?? Directory.GetCurrentDirectory();
        string[] matches = Directory.Exists(directory) ? Directory.GetFiles(directory, fileName) : [];

        if (matches.Length == 0)
        {
            throw new FileNotFoundException($"No files match {path}");
        }

        Array.Sort(matches, StringComparer.OrdinalIgnoreCase);

        return matches;
    }
}
