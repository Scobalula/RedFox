namespace RedFox.Graphics3D;

/// <summary>
/// Represents a placed instance of an external scene file, positioned by the node's transform.
/// </summary>
/// <param name="name">The name of the node.</param>
/// <param name="filePath">The path of the referenced file, as stored in the source.</param>
public class SceneReference(string name, string filePath) : SceneNode(name)
{
    /// <summary>
    /// Gets or sets the path of the referenced file, as stored in the source.
    /// </summary>
    public string FilePath { get; set; } = filePath;

    /// <summary>
    /// Gets or sets the absolute path of the referenced file, or <see langword="null"/> if it could not be resolved.
    /// </summary>
    public string? ResolvedFilePath { get; set; }
}
