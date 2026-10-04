using Microsoft.Extensions.Logging;

namespace RedFox.GameExtraction;

/// <summary>
/// Configures the file, console, and debugger output used by GameExtraction logging.
/// </summary>
public sealed class GameExtractionLogOptions
{
    /// <summary>
    /// Gets or sets the directory log files are written to. Defaults to <see cref="GameExtractionLogging.GetLogDirectory"/> for the application name.
    /// </summary>
    public string? Directory { get; set; }

    /// <summary>
    /// Gets or sets the minimum level that is logged.
    /// </summary>
    public LogLevel MinimumLevel { get; set; } = LogLevel.Information;

    /// <summary>
    /// Gets or sets a value indicating whether log entries are written to the console.
    /// </summary>
    public bool WriteToConsole { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether log entries are written to the attached debugger.
    /// </summary>
    public bool WriteToDebug { get; set; } = true;

    /// <summary>
    /// Gets or sets the number of log files retained on disk. Set to zero to disable pruning.
    /// </summary>
    public int RetainedFileCount { get; set; } = 7;

    /// <summary>
    /// Gets or sets the maximum size of an individual log file in bytes before it rolls over. Set to zero to disable size-based rolling.
    /// </summary>
    public long FileSizeLimitBytes { get; set; } = 16 * 1024 * 1024;

    /// <summary>
    /// Gets or sets a value indicating whether timestamps are written in UTC rather than local time.
    /// </summary>
    public bool UseUtcTimestamp { get; set; }
}
