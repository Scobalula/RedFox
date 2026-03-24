
namespace RedFox.GameExtraction.UI;

/// <summary>
/// Holds preview data for display in the preview window.
/// </summary>
public class PreviewData
{
    public PreviewType Type { get; init; }

    public byte[]? RawData { get; init; }

    public string? TextContent { get; init; }

    public string? TextLanguage { get; init; }

    public byte[]? ImageData { get; init; }

    public string? Title { get; init; }

    public IReadOnlyDictionary<string, string>? Attributes { get; init; }
}