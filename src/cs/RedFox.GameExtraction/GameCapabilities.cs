namespace RedFox.GameExtraction;

/// <summary>
/// Defines what features a game source supports.
/// The UI adapts based on these capabilities.
/// </summary>
public class GameCapabilities
{
    public bool SupportsPreview { get; init; }

    public bool SupportsLoadFromMemory { get; init; }

    public bool SupportsLoadFromFile { get; init; }

    public bool SupportsModel3DPreview { get; init; }

    public bool SupportsTools => SupportsPreview;

    public IReadOnlyList<string> SupportedExportFormats { get; init; } = [];
}