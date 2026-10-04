using System.Text;
using Microsoft.Extensions.Logging;

namespace RedFox.GameExtraction;

/// <summary>
/// Serializes log entries to daily rolling files inside the configured directory.
/// </summary>
internal sealed class LogFileWriter : IDisposable
{
    private readonly GameExtractionLogOptions _options;
    private readonly object _gate = new();
    private StreamWriter? _writer;
    private DateOnly _date;
    private long _size;
    private bool _disposed;

    public LogFileWriter(GameExtractionLogOptions options)
    {
        _options = options;
    }

    public void Write(LogLevel level, string category, string message, Exception? exception, bool useUtcTimestamp)
    {
        string line = FormatLine(level, category, message, exception, useUtcTimestamp);
        long length = Encoding.UTF8.GetByteCount(line) + Environment.NewLine.Length;

        lock (_gate)
        {
            if (_disposed)
                return;

            DateOnly date = DateOnly.FromDateTime(useUtcTimestamp ? DateTime.UtcNow : DateTime.Now);
            if (_writer is null || date != _date || IsFull(length))
                Open(date);

            _writer!.WriteLine(line);
            _size += length;
        }
    }

    public void Dispose()
    {
        lock (_gate)
        {
            _writer?.Dispose();
            _writer = null;
            _disposed = true;
        }
    }

    private bool IsFull(long length) => _options.FileSizeLimitBytes > 0 && _size + length > _options.FileSizeLimitBytes;

    private void Open(DateOnly date)
    {
        _writer?.Dispose();
        _date = date;

        string path = ResolvePath(date);
        _writer = new StreamWriter(new FileStream(path, FileMode.Append, FileAccess.Write, FileShare.ReadWrite), new UTF8Encoding(encoderShouldEmitUTF8Identifier: false)) { AutoFlush = true };
        _size = _writer.BaseStream.Length;

        Prune();
    }

    private string ResolvePath(DateOnly date)
    {
        string directory = _options.Directory!;
        string baseName = date.ToString("yyyyMMdd");
        string basePath = Path.Combine(directory, $"{baseName}.log");

        if (_options.FileSizeLimitBytes <= 0 || GetLength(basePath) < _options.FileSizeLimitBytes)
            return basePath;

        for (int sequence = 1; ; sequence++)
        {
            string path = Path.Combine(directory, $"{baseName}.{sequence}.log");
            if (GetLength(path) < _options.FileSizeLimitBytes)
                return path;
        }
    }

    private void Prune()
    {
        if (_options.RetainedFileCount <= 0)
            return;

        string[] files = Directory.GetFiles(_options.Directory!, "*.log");
        if (files.Length <= _options.RetainedFileCount)
            return;

        foreach (string file in files.OrderByDescending(File.GetLastWriteTimeUtc).Skip(_options.RetainedFileCount))
        {
            try
            {
                File.Delete(file);
            }
            catch (IOException)
            {
            }
            catch (UnauthorizedAccessException)
            {
            }
        }
    }

    private static long GetLength(string path) => File.Exists(path) ? new FileInfo(path).Length : 0;

    private static string FormatLine(LogLevel level, string category, string message, Exception? exception, bool useUtcTimestamp)
    {
        DateTime timestamp = useUtcTimestamp ? DateTime.UtcNow : DateTime.Now;
        StringBuilder builder = new();

        builder.Append(timestamp.ToString("yyyy-MM-dd HH:mm:ss.fff")).Append(" [").Append(GetLevelText(level)).Append("] ").Append(category).Append(": ").Append(message);

        if (exception is not null)
            builder.Append(Environment.NewLine).Append(exception);

        return builder.ToString();
    }

    private static string GetLevelText(LogLevel level) => level switch
    {
        LogLevel.Trace => "TRACE",
        LogLevel.Debug => "DEBUG",
        LogLevel.Information => "INFO",
        LogLevel.Warning => "WARN",
        LogLevel.Error => "ERROR",
        LogLevel.Critical => "CRIT",
        _ => "NONE",
    };
}
