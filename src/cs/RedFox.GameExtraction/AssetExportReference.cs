namespace RedFox.GameExtraction;

/// <summary>
/// Describes a referenced asset that should be exported recursively.
/// </summary>
public sealed class AssetExportReference
{
    /// <summary>
    /// Gets the referenced asset.
    /// </summary>
    public Asset Asset { get; }

    /// <summary>
    /// Gets the relative output directory that should be applied when exporting the reference.
    /// </summary>
    public string RelativeOutputDirectory { get; }

    /// <summary>
    /// Initializes a new instance of the <see cref="AssetExportReference"/> class.
    /// </summary>
    /// <param name="asset">The referenced asset.</param>
    public AssetExportReference(Asset asset)
        : this(asset, string.Empty)
    {
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="AssetExportReference"/> class.
    /// </summary>
    /// <param name="asset">The referenced asset.</param>
    /// <param name="relativeOutputDirectory">The relative output directory that should be applied when exporting the reference.</param>
    public AssetExportReference(Asset asset, string relativeOutputDirectory)
    {
        Asset = asset ?? throw new ArgumentNullException(nameof(asset));
        RelativeOutputDirectory = NormalizeRelativeOutputDirectory(relativeOutputDirectory);
    }

    private static string NormalizeRelativeOutputDirectory(string relativeOutputDirectory)
    {
        if (string.IsNullOrWhiteSpace(relativeOutputDirectory))
        {
            return string.Empty;
        }

        if (Path.IsPathRooted(relativeOutputDirectory))
        {
            throw new ArgumentException("Reference output directories must be relative.", nameof(relativeOutputDirectory));
        }

        string[] parts = relativeOutputDirectory.Split(
            ['\\', '/'],
            StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

        return parts.Length == 0 ? string.Empty : Path.Combine(parts);
    }
}
