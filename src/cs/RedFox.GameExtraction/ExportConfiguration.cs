namespace RedFox.GameExtraction;

/// <summary>
/// Configuration passed to <see cref="IAssetHandler.WriteAsync"/> to control how and where
/// assets are exported.
/// </summary>
public sealed class ExportConfiguration
{
    /// <summary>Root output directory for all exported files.</summary>
    public required string OutputDirectory { get; init; }

    /// <summary>
    /// Whether to overwrite files that already exist on disk.
    /// Defaults to <see langword="false"/>.
    /// </summary>
    public bool Overwrite { get; init; }

    /// <summary>
    /// Whether handlers should skip expensive read operations when the destination
    /// output file already exists and <see cref="Overwrite"/> is false.
    /// Defaults to <see langword="true"/>.
    /// </summary>
    public bool SkipReadIfOutputExists { get; init; } = true;

    /// <summary>
    /// Whether handlers should export discovered references (materials, textures, etc.)
    /// alongside primary assets. Defaults to <see langword="false"/>.
    /// </summary>
    public bool ExportReferences { get; init; }

    /// <summary>
    /// Optional handler-specific feature flags and parameters.
    /// </summary>
    public IReadOnlyDictionary<string, object?> Flags { get; init; } = new Dictionary<string, object?>();

    /// <summary>
    /// Whether to recreate the asset's virtual directory structure under <see cref="OutputDirectory"/>.
    /// When <see langword="true"/>, an entry at <c>models/characters/hero.xmodel</c> is exported to
    /// <c>{OutputDirectory}/models/characters/hero.ext</c>.
    /// When <see langword="false"/>, only the file name is used and all exports land flat in
    /// <see cref="OutputDirectory"/>. Defaults to <see langword="true"/>.
    /// </summary>
    public bool PreserveDirectoryStructure { get; init; } = true;

    /// <summary>
    /// Optional delegate for fully customising the output file path.
    /// Receives the <see cref="IAssetEntry"/> and the desired output extension (including the
    /// leading dot, e.g. <c>".png"</c>), and returns an absolute file path.
    /// Return <see langword="null"/> to fall back to the default path resolution.
    /// </summary>
    public Func<IAssetEntry, string, string?, string?>? OutputPathResolver { get; init; }

    /// <summary>
    /// Optional delegate controlling existence checks for output paths.
    /// Receives the entry and the resolved output path.
    /// </summary>
    public Func<IAssetEntry, string, bool>? ExistsResolver { get; init; }

    /// <summary>
    /// Optional delegate controlling overwrite policy for output paths.
    /// Receives the entry and the resolved output path.
    /// </summary>
    public Func<IAssetEntry, string, bool>? OverwriteResolver { get; init; }

    /// <summary>
    /// Resolves the absolute output file path for the given entry and extension.
    /// Applies <see cref="OutputPathResolver"/> if set; otherwise constructs a path from
    /// <see cref="OutputDirectory"/>, the entry's virtual path, and <paramref name="extension"/>.
    /// </summary>
    /// <param name="entry">The asset entry to resolve a path for.</param>
    /// <param name="extension">The desired output file extension, including the leading dot (e.g., <c>".png"</c>).</param>
    /// <returns>The absolute output file path.</returns>
    public string ResolvePath(IAssetEntry entry, string extension)
        => ResolvePath(entry, extension, outputDirectoryOverride: null);

    /// <summary>
    /// Resolves the absolute output file path for the given entry and extension
    /// using an optional output root override.
    /// </summary>
    /// <param name="entry">The asset entry to resolve a path for.</param>
    /// <param name="extension">The desired output extension, including the leading dot.</param>
    /// <param name="outputDirectoryOverride">An optional output root override.</param>
    /// <returns>The absolute output file path.</returns>
    public string ResolvePath(IAssetEntry entry, string extension, string? outputDirectoryOverride)
    {
        if (OutputPathResolver?.Invoke(entry, extension, outputDirectoryOverride) is { } custom)
            return custom;

        var outputRoot = outputDirectoryOverride ?? OutputDirectory;
        var relativePath = PreserveDirectoryStructure ? entry.FullPath : entry.Name;
        var withoutExtension = Path.Combine(outputRoot, Path.ChangeExtension(relativePath, null));

        return $"{withoutExtension}{extension}";
    }

    /// <summary>
    /// Checks whether an output file exists for the given asset/extension.
    /// </summary>
    public bool Exists(IAssetEntry entry, string extension, string? outputDirectoryOverride = null)
    {
        var outputPath = ResolvePath(entry, extension, outputDirectoryOverride);
        return ExistsResolver?.Invoke(entry, outputPath) ?? File.Exists(outputPath);
    }

    /// <summary>
    /// Determines whether an output file should be overwritten for the given asset/extension.
    /// </summary>
    public bool ShouldOverwrite(IAssetEntry entry, string extension, string? outputDirectoryOverride = null)
    {
        var outputPath = ResolvePath(entry, extension, outputDirectoryOverride);
        return OverwriteResolver?.Invoke(entry, outputPath) ?? Overwrite;
    }
}
