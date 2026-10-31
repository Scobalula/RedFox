namespace RedFox.GameExtraction;

/// <summary>
/// Identifies a supported group for a GameExtraction setting.
/// </summary>
public enum GameExtractionSettingGroup
{
    /// <summary>
    /// Settings controlling asset output.
    /// </summary>
    Export,

    /// <summary>
    /// Settings for model and mesh assets.
    /// </summary>
    Model,

    /// <summary>
    /// Settings for image and texture assets.
    /// </summary>
    Image,

    /// <summary>
    /// Settings for audio assets.
    /// </summary>
    Sound,

    /// <summary>
    /// Settings for archive and container assets.
    /// </summary>
    Archive,

    /// <summary>
    /// Settings that apply generally to the application.
    /// </summary>
    General,

    /// <summary>
    /// Settings that do not fit another supported group.
    /// </summary>
    Other,

    /// <summary>
    /// Settings for animation and skeletal motion assets.
    /// </summary>
    Animation,

    /// <summary>
    /// Settings for text and structured document assets.
    /// </summary>
    Document,

    /// <summary>
    /// Settings for preview controls.
    /// </summary>
    Preview,

    /// <summary>
    /// Settings for script assets.
    /// </summary>
    Script,

    /// <summary>
    /// Settings for table assets.
    /// </summary>
    Table,

    /// <summary>
    /// Settings for binary files.
    /// </summary>
    Binary,

    /// <summary>
    /// Settings for map and level assets.
    /// </summary>
    Map,

    /// <summary>
    /// Settings for shader assets.
    /// </summary>
    Shader,

    /// <summary>
    /// Settings for effects assets.
    /// </summary>
    Effect,

    /// <summary>
    /// Settings for cutscene assets.
    /// </summary>
    Cutscene,
}
