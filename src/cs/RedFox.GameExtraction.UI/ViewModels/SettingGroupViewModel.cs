namespace RedFox.GameExtraction.UI.ViewModels;

/// <summary>
/// Groups settings under a shared group tag.
/// </summary>
public sealed class SettingGroupViewModel
{
    /// <summary>
    /// Initializes a new instance of the <see cref="SettingGroupViewModel"/> class.
    /// </summary>
    /// <param name="name">The group name.</param>
    /// <param name="settings">The settings in the group.</param>
    public SettingGroupViewModel(string name, IReadOnlyList<GameExtractionSettingViewModel> settings)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        ArgumentNullException.ThrowIfNull(settings);

        Name = name;
        Settings = settings;
    }

    /// <summary>
    /// Gets the group name.
    /// </summary>
    public string Name { get; }

    /// <summary>
    /// Gets the settings in the group.
    /// </summary>
    public IReadOnlyList<GameExtractionSettingViewModel> Settings { get; }
}