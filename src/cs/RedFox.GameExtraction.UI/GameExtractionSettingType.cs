namespace RedFox.GameExtraction.UI;

/// <summary>
/// Defines the editor used for a game extraction setting.
/// </summary>
public enum GameExtractionSettingType
{
    /// <summary>
    /// Renders a plain text box.
    /// </summary>
    TextBox,

    /// <summary>
    /// Renders a text box with a file picker button.
    /// </summary>
    FilePath,

    /// <summary>
    /// Renders a text box with a directory picker button.
    /// </summary>
    DirectoryPath,

    /// <summary>
    /// Renders a combo box populated from the setting options.
    /// </summary>
    ComboBox,

    /// <summary>
    /// Renders a check box.
    /// </summary>
    CheckBox,
}
