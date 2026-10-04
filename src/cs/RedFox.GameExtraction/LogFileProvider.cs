using Microsoft.Extensions.Logging;

namespace RedFox.GameExtraction;

/// <summary>
/// Creates file loggers that share a single rolling writer.
/// </summary>
internal sealed class LogFileProvider : ILoggerProvider
{
    private readonly LogFileWriter _writer;
    private readonly bool _useUtcTimestamp;

    public LogFileProvider(GameExtractionLogOptions options)
    {
        _writer = new LogFileWriter(options);
        _useUtcTimestamp = options.UseUtcTimestamp;
    }

    public ILogger CreateLogger(string categoryName) => new LogFileLogger(categoryName, _writer, _useUtcTimestamp);

    public void Dispose() => _writer.Dispose();
}
