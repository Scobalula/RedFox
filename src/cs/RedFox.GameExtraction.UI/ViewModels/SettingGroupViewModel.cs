namespace RedFox.GameExtraction.UI.ViewModels;

/// <summary>
/// Groups settings under a shared, user-facing category.
/// </summary>
public sealed class SettingGroupViewModel
{
    /// <summary>
    /// Gets the fixed group identifier.
    /// </summary>
    public GameExtractionSettingGroup Kind { get; }

    /// <summary>
    /// Gets the user-facing group title.
    /// </summary>
    public string Title => Kind switch
    {
        GameExtractionSettingGroup.Export => "Export Settings",
        GameExtractionSettingGroup.Model => "Model Settings",
        GameExtractionSettingGroup.Animation => "Animation Settings",
        GameExtractionSettingGroup.Image => "Image Settings",
        GameExtractionSettingGroup.Sound => "Sound Settings",
        GameExtractionSettingGroup.Archive => "Archive Settings",
        GameExtractionSettingGroup.Document => "Document Settings",
        GameExtractionSettingGroup.General => "General Settings",
        _ => "Other Settings",
    };

    /// <summary>
    /// Gets the settings in the group.
    /// </summary>
    public IReadOnlyList<GameExtractionSettingViewModel> Settings { get; }

    /// <summary>
    /// Initializes a new instance of the <see cref="SettingGroupViewModel"/> class.
    /// </summary>
    /// <param name="kind">The settings group.</param>
    /// <param name="settings">The settings in the group.</param>
    public SettingGroupViewModel(GameExtractionSettingGroup kind, IReadOnlyList<GameExtractionSettingViewModel> settings)
    {
        if (!Enum.IsDefined(kind))
        {
            throw new ArgumentOutOfRangeException(nameof(kind));
        }

        ArgumentNullException.ThrowIfNull(settings);

        Kind = kind;
        Settings = settings;
    }
}
