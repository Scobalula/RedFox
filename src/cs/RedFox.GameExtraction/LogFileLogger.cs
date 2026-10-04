using Microsoft.Extensions.Logging;

namespace RedFox.GameExtraction;

/// <summary>
/// Writes log entries to the shared rolling file writer.
/// </summary>
internal sealed class LogFileLogger(string categoryName, LogFileWriter writer, LogLevel minimumLevel, bool useUtcTimestamp) : ILogger
{
    public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;

    public bool IsEnabled(LogLevel logLevel) => logLevel != LogLevel.None && logLevel >= minimumLevel;

    public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter)
    {
        ArgumentNullException.ThrowIfNull(formatter);

        if (!IsEnabled(logLevel))
            return;

        writer.Write(logLevel, categoryName, formatter(state, exception), exception, useUtcTimestamp);
    }
}
