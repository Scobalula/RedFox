using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace RedFox.GameExtraction.UI.ViewModels;

/// <summary>
/// Provides an editable setting value for the settings window.
/// </summary>
public partial class GameExtractionSettingViewModel : ObservableObject
{
    private readonly Func<GameExtractionSettingViewModel, Task<string?>> _browseRequested;
    private readonly GameExtractionSetting _setting;

    /// <summary>
    /// Initializes a new instance of the <see cref="GameExtractionSettingViewModel"/> class.
    /// </summary>
    /// <param name="setting">The setting definition.</param>
    /// <param name="settings">The persisted settings object.</param>
    /// <param name="browseRequested">The browse callback.</param>
    public GameExtractionSettingViewModel(
        GameExtractionSetting setting,
        GameExtractionSettings settings,
        Func<GameExtractionSettingViewModel, Task<string?>> browseRequested)
    {
        _setting = setting ?? throw new ArgumentNullException(nameof(setting));
        ArgumentNullException.ThrowIfNull(settings);
        _browseRequested = browseRequested ?? throw new ArgumentNullException(nameof(browseRequested));

        Options = setting.Options;
        TextValue = settings.GetSettingValue(setting) ?? string.Empty;
        BooleanValue = bool.TryParse(TextValue, out bool booleanValue) ? booleanValue : setting.DefaultValue is true;
        SelectedOption = ResolveSelectedOption(TextValue);
    }

    /// <summary>
    /// Gets the setting name.
    /// </summary>
    public string Name => _setting.Name;

    /// <summary>
    /// Gets the setting label.
    /// </summary>
    public string Label => string.IsNullOrWhiteSpace(_setting.Label) ? _setting.Name : _setting.Label;

    /// <summary>
    /// Gets the setting description.
    /// </summary>
    public string? Description => _setting.Description;

    /// <summary>
    /// Gets the setting editor type.
    /// </summary>
    public GameExtractionSettingType Type => _setting.Type;

    /// <summary>
    /// Gets the available combo box options.
    /// </summary>
    public IReadOnlyList<string> Options { get; }

    /// <summary>
    /// Gets the file picker filter string.
    /// </summary>
    public string? FileFilter => _setting.FileFilter;

    /// <summary>
    /// Gets the picker title.
    /// </summary>
    public string PickerTitle => string.IsNullOrWhiteSpace(_setting.PickerTitle) ? $"Select {Label}" : _setting.PickerTitle;

    /// <summary>
    /// Gets a value indicating whether the setting description has content.
    /// </summary>
    public bool HasDescription => !string.IsNullOrWhiteSpace(Description);

    /// <summary>
    /// Gets a value indicating whether a label should be shown above the setting editor.
    /// </summary>
    public bool ShowLabel => !IsCheckBox;

    /// <summary>
    /// Gets a value indicating whether the setting uses a text box editor.
    /// </summary>
    public bool IsTextBox => Type == GameExtractionSettingType.TextBox;

    /// <summary>
    /// Gets a value indicating whether the setting uses a browseable path editor.
    /// </summary>
    public bool IsPathSetting => Type is GameExtractionSettingType.FilePath or GameExtractionSettingType.DirectoryPath;

    /// <summary>
    /// Gets a value indicating whether the setting uses a file path editor.
    /// </summary>
    public bool IsFilePath => Type == GameExtractionSettingType.FilePath;

    /// <summary>
    /// Gets a value indicating whether the setting uses a combo box editor.
    /// </summary>
    public bool IsComboBox => Type == GameExtractionSettingType.ComboBox;

    /// <summary>
    /// Gets a value indicating whether the setting uses a check box editor.
    /// </summary>
    public bool IsCheckBox => Type == GameExtractionSettingType.CheckBox;

    /// <summary>
    /// Gets or sets the text value.
    /// </summary>
    [ObservableProperty]
    public partial string TextValue { get; set; }

    /// <summary>
    /// Gets or sets the Boolean value.
    /// </summary>
    [ObservableProperty]
    public partial bool BooleanValue { get; set; }

    /// <summary>
    /// Gets or sets the selected combo box option.
    /// </summary>
    [ObservableProperty]
    public partial string? SelectedOption { get; set; }

    /// <summary>
    /// Saves this value into the settings object.
    /// </summary>
    /// <param name="settings">The settings object to update.</param>
    public void SaveTo(GameExtractionSettings settings)
    {
        ArgumentNullException.ThrowIfNull(settings);

        settings.SetSettingValue(_setting, GetValueForSave());
    }

    /// <summary>
    /// Opens a picker for path settings.
    /// </summary>
    /// <returns>A task that completes when browsing finishes.</returns>
    [RelayCommand]
    public async Task BrowseAsync()
    {
        string? selectedPath = await _browseRequested.Invoke(this).ConfigureAwait(true);
        if (!string.IsNullOrWhiteSpace(selectedPath))
        {
            TextValue = selectedPath;
        }
    }

    private string? ResolveSelectedOption(string value)
    {
        if (!string.IsNullOrWhiteSpace(value) && Options.Contains(value))
        {
            return value;
        }

        return Options.Count > 0 ? Options[0] : null;
    }

    private string? GetValueForSave()
    {
        return Type switch
        {
            GameExtractionSettingType.CheckBox => BooleanValue.ToString(),
            GameExtractionSettingType.ComboBox => SelectedOption,
            _ => TextValue,
        };
    }
}