namespace RedFox.GameExtraction;

/// <summary>
/// Describes a user-configurable setting exposed by GameExtraction frontends.
/// </summary>
public sealed class GameExtractionSetting
{
    /// <summary>
    /// Gets the persisted setting key.
    /// </summary>
    public required string Name { get; init; }

    /// <summary>
    /// Gets the supported group used to display this setting.
    /// </summary>
    public GameExtractionSettingGroup Group { get; init; } = GameExtractionSettingGroup.General;

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
    public GameExtractionSettingType Type { get; init; } = GameExtractionSettingType.Text;

    /// <summary>
    /// Gets the default value used when no persisted value exists.
    /// </summary>
    public object? DefaultValue { get; init; }

    /// <summary>
    /// Gets the available values for choice and text array settings.
    /// </summary>
    public IReadOnlyList<string> Options { get; init; } = [];

    /// <summary>
    /// Gets the source that resolves the available values in place of <see cref="Options"/>.
    /// </summary>
    public IGameExtractionOptionsSource? OptionsSource { get; init; }

    /// <summary>
    /// Gets the file picker filter string for file path settings.
    /// </summary>
    public string? FileFilter { get; init; }

    /// <summary>
    /// Gets the picker dialog title for file or directory path settings.
    /// </summary>
    public string? PickerTitle { get; init; }

    public double Minimum { get; init; }

    public double Maximum { get; init; } = 1.0;

    public string? Section { get; init; }

    public string? DependsOn { get; init; }
}
