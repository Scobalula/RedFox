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
}
