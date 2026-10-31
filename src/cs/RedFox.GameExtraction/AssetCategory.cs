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
    /// The asset describes a surface material, previewed as a scene that shows it.
    /// </summary>
    Material,

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

    /// <summary>
    /// The asset contains script code.
    /// </summary>
    Script,

    /// <summary>
    /// The asset contains tabular data.
    /// </summary>
    Table,

    /// <summary>
    /// The asset contains binary data.
    /// </summary>
    Binary,

    /// <summary>
    /// The asset contains a map or level.
    /// </summary>
    Map,

    /// <summary>
    /// The asset contains shader data.
    /// </summary>
    Shader,

    /// <summary>
    /// The asset contains effects data.
    /// </summary>
    Effect,

    /// <summary>
    /// The asset contains a cutscene.
    /// </summary>
    Cutscene,
}
