namespace RedFox.GameExtraction.UI.Models;

/// <summary>
/// Represents a single row shown in the Explorer-style directory view. Each
/// entry is either a folder (which the user can navigate into) or a file
/// (which maps back to an <see cref="AssetRowViewModel"/>).
/// </summary>
public sealed class AssetExplorerEntry
{
    private AssetExplorerEntry(
        bool isFolder,
        string name,
        string typeDisplay,
        string information,
        AssetDirectoryNode? directory,
        AssetRowViewModel? row)
    {
        IsFolder = isFolder;
        Name = name;
        TypeDisplay = typeDisplay;
        Information = information;
        Directory = directory;
        Row = row;
    }

    /// <summary>
    /// Gets a value indicating whether this entry is a folder.
    /// </summary>
    public bool IsFolder { get; }

    /// <summary>
    /// Gets a value indicating whether this entry is a file.
    /// </summary>
    public bool IsFile => !IsFolder;

    /// <summary>
    /// Gets the display name (leaf folder name or asset name).
    /// </summary>
    public string Name { get; }

    /// <summary>
    /// Gets the column text shown for the asset/folder type.
    /// </summary>
    public string TypeDisplay { get; }

    /// <summary>
    /// Gets the column text shown in the information column.
    /// </summary>
    public string Information { get; }

    /// <summary>
    /// Gets the folder node represented by this entry, or <c>null</c> when this is a file.
    /// </summary>
    public AssetDirectoryNode? Directory { get; }

    /// <summary>
    /// Gets the asset row represented by this entry, or <c>null</c> when this is a folder.
    /// </summary>
    public AssetRowViewModel? Row { get; }

    /// <summary>
    /// Creates a folder entry for the supplied directory node.
    /// </summary>
    /// <param name="node">The folder node to wrap.</param>
    public static AssetExplorerEntry ForFolder(AssetDirectoryNode node)
    {
        ArgumentNullException.ThrowIfNull(node);

        string name = string.IsNullOrEmpty(node.Name) ? "(root)" : node.Name;
        int folderCount = node.Children.Count;
        int fileCount = node.RecursiveFileCount;
        string information = $"{folderCount:N0} folder{(folderCount == 1 ? string.Empty : "s")}, {fileCount:N0} file{(fileCount == 1 ? string.Empty : "s")}";
        return new AssetExplorerEntry(
            isFolder: true,
            name: name,
            typeDisplay: "Folder",
            information: information,
            directory: node,
            row: null);
    }

    /// <summary>
    /// Creates a file entry for the supplied asset row.
    /// </summary>
    /// <param name="row">The asset row to wrap.</param>
    public static AssetExplorerEntry ForFile(AssetRowViewModel row)
    {
        ArgumentNullException.ThrowIfNull(row);

        string name = LeafName(row.Asset.Name);
        return new AssetExplorerEntry(
            isFolder: false,
            name: name,
            typeDisplay: row.Type,
            information: row.Information,
            directory: null,
            row: row);
    }

    private static string LeafName(string? assetName)
    {
        if (string.IsNullOrEmpty(assetName))
        {
            return string.Empty;
        }

        int lastSlash = assetName.LastIndexOfAny(['/', '\\']);
        return lastSlash < 0 ? assetName : assetName[(lastSlash + 1)..];
    }
}
