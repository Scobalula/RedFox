namespace RedFox.Imaging.Processing;

/// <summary>
/// Common options for custom converter engines.
/// </summary>
public sealed class ConverterEngineOptions
{

    /// <summary>
    /// When true, conversion callers should continue with built-in CPU codecs when an engine cannot convert.
    /// </summary>
    public bool AllowCpuFallback { get; init; } = true;

    /// <summary>
    /// Initializes a new instance of the <see cref="ConverterEngineOptions"/> class.
    /// </summary>
    public ConverterEngineOptions()
    {
    }
}
