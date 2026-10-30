using System.Globalization;
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
    /// <param name="assetManager">The asset manager used to resolve setting options.</param>
    /// <param name="browseRequested">The browse callback.</param>
    public GameExtractionSettingViewModel(GameExtractionSetting setting, GameExtractionSettings settings, AssetManager assetManager, Func<GameExtractionSettingViewModel, Task<string?>> browseRequested)
    {
        _setting = setting ?? throw new ArgumentNullException(nameof(setting));
        ArgumentNullException.ThrowIfNull(settings);
        _browseRequested = browseRequested ?? throw new ArgumentNullException(nameof(browseRequested));

        Options = setting.OptionsSource?.GetOptions(assetManager) ?? setting.Options;
        TextValue = settings.GetSettingValue(setting) ?? string.Empty;
        BooleanValue = bool.TryParse(TextValue, out bool booleanValue) ? booleanValue : setting.DefaultValue is true;
        SliderValue = double.TryParse(TextValue, NumberStyles.Float, CultureInfo.InvariantCulture, out double sliderValue) ? Math.Clamp(sliderValue, setting.Minimum, setting.Maximum) : setting.Minimum;
        SelectedOption = ResolveSelectedOption(TextValue);

        if (setting.Type == GameExtractionSettingType.TextArray)
        {
            HashSet<string> values = [.. SplitValues(TextValue)];
            OptionToggles = [.. Options.Select(option => new SettingOptionViewModel(option, values.Contains(option)))];
        }
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
    public bool ShowLabel => !IsBoolean;

    /// <summary>
    /// Gets a value indicating whether the setting holds a text value.
    /// </summary>
    public bool IsText => Type == GameExtractionSettingType.Text || (Type == GameExtractionSettingType.TextArray && OptionToggles.Count == 0);

    public bool IsOptionList => OptionToggles.Count > 0;

    public IReadOnlyList<SettingOptionViewModel> OptionToggles { get; } = [];

    public string? Section => _setting.Section;

    public string? DependsOn => _setting.DependsOn;

    public GameExtractionSettingViewModel? Parent { get; set; }

    /// <summary>
    /// Gets a value indicating whether the setting uses a browseable path editor.
    /// </summary>
    public bool IsPathSetting => Type is GameExtractionSettingType.FilePath or GameExtractionSettingType.DirectoryPath;

    /// <summary>
    /// Gets a value indicating whether the setting uses a file path editor.
    /// </summary>
    public bool IsFilePath => Type == GameExtractionSettingType.FilePath;

    /// <summary>
    /// Gets a value indicating whether the setting holds a choice from its options.
    /// </summary>
    public bool IsChoice => Type == GameExtractionSettingType.Choice;

    /// <summary>
    /// Gets a value indicating whether the setting holds a boolean value.
    /// </summary>
    public bool IsBoolean => Type == GameExtractionSettingType.Boolean;

    public bool IsSlider => Type == GameExtractionSettingType.Slider;

    public double Minimum => _setting.Minimum;

    public double Maximum => _setting.Maximum;

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

    [ObservableProperty]
    public partial double SliderValue { get; set; }

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
            GameExtractionSettingType.Boolean => BooleanValue.ToString(),
            GameExtractionSettingType.Choice => SelectedOption,
            GameExtractionSettingType.Slider => SliderValue.ToString(CultureInfo.InvariantCulture),
            GameExtractionSettingType.TextArray when IsOptionList => GetOptionListValue(),
            _ => TextValue,
        };
    }

    private string GetOptionListValue()
    {
        IEnumerable<string> selected	= OptionToggles.Where(option => option.IsSelected).Select(option => option.Name);
        IEnumerable<string> unknown		= SplitValues(TextValue).Where(value => !Options.Contains(value));

        return string.Join(",", selected.Concat(unknown));
    }

    private static string[] SplitValues(string value)
    {
        return value.Split([',', ';', ' ', '\t', '\r', '\n'], StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries);
    }
}
