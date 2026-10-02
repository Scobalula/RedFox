namespace RedFox.GameExtraction;

/// <summary>
/// Defines the kind of value held by a game extraction setting.
/// </summary>
public enum GameExtractionSettingType
{
    /// <summary>
    /// A free-form text value.
    /// </summary>
    Text,

    /// <summary>
    /// A delimited text value exposed as an array of strings to handlers.
    /// </summary>
    TextArray,

    /// <summary>
    /// A path to a file.
    /// </summary>
    FilePath,

    /// <summary>
    /// A path to a directory.
    /// </summary>
    DirectoryPath,

    /// <summary>
    /// A single value chosen from the setting options.
    /// </summary>
    Choice,

    /// <summary>
    /// A true or false value.
    /// </summary>
    Boolean,
}
