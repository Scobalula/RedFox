namespace RedFox.GameExtraction;

/// <summary>
/// Represents a source from which game assets can be loaded.
/// </summary>
public interface IGameSource
{
    string Title { get; }

    string Description { get; }

    GameCapabilities Capabilities { get; }

    string FileFilter { get; }

    IReadOnlyList<string> MetadataColumns { get; }

    Task<LoadedSource> LoadAssetsAsync(string filePath, IProgress<ProgressInfo> progress, CancellationToken cancellationToken);

    Task<LoadedSource> LoadAssetsFromMemoryAsync(IProgress<ProgressInfo> progress, CancellationToken cancellationToken)
    {
        throw new NotSupportedException("This game source does not support loading from memory.");
    }

    void UnloadSource(LoadedSource source)
    {
    }
}