namespace RedFox.GameExtraction.UI;

/// <summary>
/// Describes a setting displayed by the GameExtraction settings window.
/// </summary>
public sealed class GameExtractionSetting
{
    /// <summary>
    /// Gets the persisted setting key.
    /// </summary>
    public required string Name { get; init; }

    /// <summary>
    /// Gets the group tag used to display settings together.
    /// </summary>
    public string? Group { get; init; }

    /// <summary>
    /// Gets the legacy category shown above the setting when no group is set.
    /// </summary>
    public string Category { get; init; } = "General";

    /// <summary>
    /// Gets the label shown next to the setting.
    /// </summary>
    public string? Label { get; init; }

    /// <summary>
    /// Gets the optional descriptive text shown below the setting.
    /// </summary>
    public string? Description { get; init; }

    /// <summary>
    /// Gets the setting editor type.
    /// </summary>
    public GameExtractionSettingType Type { get; init; } = GameExtractionSettingType.TextBox;

    /// <summary>
    /// Gets the default value used when no persisted value exists.
    /// </summary>
    public object? DefaultValue { get; init; }

    /// <summary>
    /// Gets the available values for combo box settings.
    /// </summary>
    public IReadOnlyList<string> Options { get; init; } = [];

    /// <summary>
    /// Gets the file picker filter string for file path settings.
    /// </summary>
    public string? FileFilter { get; init; }

    /// <summary>
    /// Gets the picker dialog title for file or directory path settings.
    /// </summary>
    public string? PickerTitle { get; init; }
}
