namespace RedFox.Graphics3D.IO;

/// <summary>
/// Carries per-operation file-system context for scene translation without forcing
/// future translator API changes whenever more metadata is needed.
/// </summary>
public sealed class SceneTranslationContext
{
    public SceneTranslationContext(string name, SceneTranslatorOptions options)
    {
        Name = string.IsNullOrWhiteSpace(name) ? "Scene" : name;
        Options = options ?? throw new ArgumentNullException(nameof(options));
    }

    /// <summary>
    /// Gets the logical scene/file name used by stream-based translators.
    /// </summary>
    public string Name { get; }

    /// <summary>
    /// Gets the translation options for this operation.
    /// </summary>
    public SceneTranslatorOptions Options { get; }

    /// <summary>
    /// Gets or sets the full source file path for read operations when available.
    /// </summary>
    public string? SourceFilePath { get; init; }

    /// <summary>
    /// Gets or sets the source directory for resolving relative imports.
    /// </summary>
    public string? SourceDirectoryPath { get; init; }

    /// <summary>
    /// Gets or sets the full output file path for write operations when available.
    /// </summary>
    public string? TargetFilePath { get; init; }

    /// <summary>
    /// Gets or sets the output directory for emitting portable relative references.
    /// </summary>
    public string? TargetDirectoryPath { get; init; }
}
