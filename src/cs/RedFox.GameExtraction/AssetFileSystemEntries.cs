using RedFox.IO.FileSystem;

namespace RedFox.GameExtraction;

internal sealed class AssetFileSystemEntries
{
    public List<VirtualFile> Files { get; } = [];

    public HashSet<VirtualDirectory> TopLevelDirectories { get; } = [];
}
