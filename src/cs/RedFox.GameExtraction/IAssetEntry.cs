namespace RedFox.GameExtraction;

/// <summary>
/// Represents a single asset entry exposed by a game source.
/// </summary>
public interface IAssetEntry
{
    string Name { get; }

    string FullPath { get; }

    string Type { get; }

    long? Size { get; }

    string? Icon { get; }

    string? Information => null;

    IReadOnlyDictionary<string, object>? Metadata { get; }
}