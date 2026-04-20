using RedFox.Graphics3D;

namespace RedFox.Graphics3D.IO;

/// <summary>
/// Carries per-operation file-system context for scene translation.
/// Created via <see cref="SceneTranslator.CreateReadContext"/> or <see cref="SceneTranslator.CreateWriteContext"/>.
/// </summary>
public sealed class SceneTranslationContext
{
    /// <summary>
    /// Initializes a new <see cref="SceneTranslationContext"/> with the given name and options.
    /// </summary>
    /// <param name="name">The logical scene or file name. Falls back to <c>"Scene"</c> when null or whitespace.</param>
    /// <param name="options">The translation options.</param>
    /// <exception cref="ArgumentNullException"><paramref name="options"/> is <see langword="null"/>.</exception>
    public SceneTranslationContext(string name, SceneTranslatorOptions options)
    {
        Name = string.IsNullOrWhiteSpace(name) ? "Scene" : name;
        Options = options ?? throw new ArgumentNullException(nameof(options));
    }

    /// <summary>
    /// Gets the logical scene or file name used by stream-based translators.
    /// </summary>
    public string Name { get; }

    /// <summary>
    /// Gets the translation options for this operation.
    /// </summary>
    public SceneTranslatorOptions Options { get; }

    /// <summary>
    /// Gets the scene selection view associated with this operation, when one has been created.
    /// This is typically populated for write operations.
    /// </summary>
    public SceneTranslationSelection? Selection { get; internal set; }

    /// <summary>
    /// Gets the full source file path for read operations, when available.
    /// </summary>
    public string? SourceFilePath { get; init; }

    /// <summary>
    /// Gets the source directory path for resolving relative imports during read operations.
    /// </summary>
    public string? SourceDirectoryPath { get; init; }

    /// <summary>
    /// Gets the full target file path for write operations, when available.
    /// </summary>
    public string? TargetFilePath { get; init; }

    /// <summary>
    /// Gets the target directory path for emitting portable relative references during write operations.
    /// </summary>
    public string? TargetDirectoryPath { get; init; }

    /// <summary>
    /// Gets the filtered selection view for the specified scene, creating it on demand if needed.
    /// </summary>
    /// <param name="scene">The scene being translated.</param>
    /// <returns>The selection view for this operation and scene.</returns>
    public SceneTranslationSelection GetSelection(Scene scene)
    {
        ArgumentNullException.ThrowIfNull(scene);

        if (Selection is null || !ReferenceEquals(Selection.Scene, scene) || Selection.Filter != Options.Filter)
            Selection = new SceneTranslationSelection(scene, Options.Filter);

        return Selection;
    }
}
