namespace RedFox.Samples.Examples;

internal sealed class FrameStatsReporter
{
    private const double ReportIntervalSeconds = 1.0;

    private readonly bool _printLiveStats;

    private double _lastReportSeconds;
    private double _maximumRenderMilliseconds;
    private double _minimumRenderMilliseconds = double.MaxValue;
    private double _totalElapsedSeconds;
    private double _totalRenderMilliseconds;
    private int _totalFrameCount;
    private double _windowMaximumRenderMilliseconds;
    private double _windowMinimumRenderMilliseconds = double.MaxValue;
    private double _windowRenderMilliseconds;
    private int _windowFrameCount;

    public FrameStatsReporter(bool printLiveStats)
    {
        _printLiveStats = printLiveStats;
    }

    public void Record(double renderMilliseconds, double elapsedSeconds)
    {
        _totalElapsedSeconds = Math.Max(_totalElapsedSeconds, elapsedSeconds);
        _totalFrameCount++;
        _totalRenderMilliseconds += renderMilliseconds;
        _minimumRenderMilliseconds = Math.Min(_minimumRenderMilliseconds, renderMilliseconds);
        _maximumRenderMilliseconds = Math.Max(_maximumRenderMilliseconds, renderMilliseconds);

        _windowFrameCount++;
        _windowRenderMilliseconds += renderMilliseconds;
        _windowMinimumRenderMilliseconds = Math.Min(_windowMinimumRenderMilliseconds, renderMilliseconds);
        _windowMaximumRenderMilliseconds = Math.Max(_windowMaximumRenderMilliseconds, renderMilliseconds);

        if (!_printLiveStats || elapsedSeconds - _lastReportSeconds < ReportIntervalSeconds)
        {
            return;
        }

        PrintWindow(elapsedSeconds);
        ResetWindow(elapsedSeconds);
    }

    public void PrintFinal()
    {
        if (_totalFrameCount == 0)
        {
            return;
        }

        double averageRenderMilliseconds = _totalRenderMilliseconds / _totalFrameCount;
        double deliveredFps = GetDeliveredFramesPerSecond(_totalFrameCount, _totalElapsedSeconds);
        Console.WriteLine($"[FrameTiming] final elapsed={_totalElapsedSeconds:F2}s frames={_totalFrameCount} fps={deliveredFps:F1} renderAvg={averageRenderMilliseconds:F2}ms renderMin={_minimumRenderMilliseconds:F2}ms renderMax={_maximumRenderMilliseconds:F2}ms");
    }

    private void PrintWindow(double elapsedSeconds)
    {
        if (_windowFrameCount == 0)
        {
            return;
        }

        double windowElapsedSeconds = elapsedSeconds - _lastReportSeconds;
        double averageRenderMilliseconds = _windowRenderMilliseconds / _windowFrameCount;
        double deliveredFps = GetDeliveredFramesPerSecond(_windowFrameCount, windowElapsedSeconds);
        Console.WriteLine($"[FrameTiming] t={elapsedSeconds:F1}s fps={deliveredFps:F1} renderAvg={averageRenderMilliseconds:F2}ms renderMin={_windowMinimumRenderMilliseconds:F2}ms renderMax={_windowMaximumRenderMilliseconds:F2}ms frames={_windowFrameCount}");
    }

    private void ResetWindow(double elapsedSeconds)
    {
        _lastReportSeconds = elapsedSeconds;
        _windowFrameCount = 0;
        _windowRenderMilliseconds = 0.0;
        _windowMinimumRenderMilliseconds = double.MaxValue;
        _windowMaximumRenderMilliseconds = 0.0;
    }

    private static double GetDeliveredFramesPerSecond(int frameCount, double elapsedSeconds)
    {
        return elapsedSeconds > 0.0 ? frameCount / elapsedSeconds : 0.0;
    }
}