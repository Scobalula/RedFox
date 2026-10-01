namespace RedFox.GameExtraction;

/// <summary>
/// Describes the broad content category of an asset independently of its source-specific type.
/// </summary>
public enum AssetCategory
{
    /// <summary>
    /// The asset contains a 3D model or geometry.
    /// </summary>
    Model,

    /// <summary>
    /// The asset contains animation data.
    /// </summary>
    Animation,

    /// <summary>
    /// The asset contains image or texture data.
    /// </summary>
    Image,

    /// <summary>
    /// The asset contains sound or music data.
    /// </summary>
    Sound,

    /// <summary>
    /// The asset is an archive or package.
    /// </summary>
    Archive,

    /// <summary>
    /// The asset contains text or structured document data.
    /// </summary>
    Document,

    /// <summary>
    /// The asset does not fit another broad category.
    /// </summary>
    Other,
}
