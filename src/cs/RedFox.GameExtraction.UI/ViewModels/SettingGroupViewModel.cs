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
        GameExtractionSettingGroup.Export		=> "Export",
        GameExtractionSettingGroup.Model		=> "Models",
        GameExtractionSettingGroup.Animation	=> "Animations",
        GameExtractionSettingGroup.Image		=> "Textures",
        GameExtractionSettingGroup.Sound		=> "Audio",
        GameExtractionSettingGroup.Archive		=> "Archives",
        GameExtractionSettingGroup.Document		=> "Documents",
        GameExtractionSettingGroup.Script		=> "Scripts",
        GameExtractionSettingGroup.Table		=> "Tables",
        GameExtractionSettingGroup.Binary		=> "Binary Files",
        GameExtractionSettingGroup.Map			=> "Maps / Levels",
        GameExtractionSettingGroup.Shader		=> "Shaders",
        GameExtractionSettingGroup.Effect		=> "Effects",
        GameExtractionSettingGroup.Cutscene		=> "Cutscenes",
        GameExtractionSettingGroup.General		=> "General",
        GameExtractionSettingGroup.Preview		=> "Preview",
        _										=> "Other",
    };

    public string Description => Kind switch
    {
        GameExtractionSettingGroup.Export		=> "Output and file handling",
        GameExtractionSettingGroup.Model		=> "Model formats and assets",
        GameExtractionSettingGroup.Animation	=> "Animation export options",
        GameExtractionSettingGroup.Image		=> "Image export options",
        GameExtractionSettingGroup.Sound		=> "Audio conversion settings",
        GameExtractionSettingGroup.Archive		=> "Archive and container options",
        GameExtractionSettingGroup.Document		=> "Text and document options",
        GameExtractionSettingGroup.Script		=> "Script asset options",
        GameExtractionSettingGroup.Table		=> "Table asset options",
        GameExtractionSettingGroup.Binary		=> "Binary file options",
        GameExtractionSettingGroup.Map			=> "Map and level asset options",
        GameExtractionSettingGroup.Shader		=> "Shader asset options",
        GameExtractionSettingGroup.Effect		=> "FX and effect asset options",
        GameExtractionSettingGroup.Cutscene		=> "Cutscene asset options",
        GameExtractionSettingGroup.General		=> "App behaviour and logging",
        GameExtractionSettingGroup.Preview		=> "Rendering and viewport",
        _										=> "Additional settings",
    };

    /// <summary>
    /// Gets the settings in the group.
    /// </summary>
    public IReadOnlyList<GameExtractionSettingViewModel> Settings { get; }

    public IReadOnlyList<SettingSectionViewModel> Sections { get; }

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
        Sections = [.. settings.GroupBy(setting => setting.Section).Select(section => new SettingSectionViewModel(section.Key, [.. section]))];
    }
}
