namespace RedFox.GameExtraction;

/// <summary>
/// Represents a single asset entry within a game source (archive or memory scan).
/// Carries only the metadata needed for handler dispatch and identification;
/// actual data is loaded on demand via an <see cref="IAssetHandler"/>.
/// </summary>
public interface IAssetEntry
{
    /// <summary>File name of the asset (no directory component).</summary>
    string Name { get; }

    /// <summary>Full virtual path within the source (e.g., <c>models/characters/hero.xmodel</c>).</summary>
    string FullPath { get; }

    /// <summary>Size of the raw asset data in bytes, if known.</summary>
    long? Size { get; }

    /// <summary>
    /// Arbitrary format-specific metadata (codec name, dimensions, flags, etc.).
    /// <see langword="null"/> when the source provides no additional fields.
    /// </summary>
    IReadOnlyDictionary<string, object?>? Metadata { get; }
}
