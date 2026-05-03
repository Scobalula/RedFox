using System.Diagnostics;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace RedFox.GameExtraction.UI.ViewModels;

/// <summary>
/// Provides editable settings for the settings window.
/// </summary>
public partial class SettingsWindowViewModel : ObservableObject
{
    private readonly string _appName;
    private readonly GameExtractionSettings _settings;

    /// <summary>
    /// Initializes a new instance of the <see cref="SettingsWindowViewModel"/> class.
    /// </summary>
    /// <param name="settings">The settings object to edit.</param>
    /// <param name="settingDefinitions">The settings to display.</param>
    /// <param name="appName">The application name used for persistence.</param>
    public SettingsWindowViewModel(
        GameExtractionSettings settings,
        IReadOnlyList<GameExtractionSetting> settingDefinitions,
        string appName)
    {
        _settings = settings ?? throw new ArgumentNullException(nameof(settings));
        ArgumentNullException.ThrowIfNull(settingDefinitions);
        ArgumentException.ThrowIfNullOrWhiteSpace(appName);
        _appName = appName;

        Groups = [.. settingDefinitions
            .GroupBy(GetGroupName)
            .Select(group => new SettingGroupViewModel(
                group.Key,
                [.. group.Select(setting => new GameExtractionSettingViewModel(setting, settings, BrowseSettingAsync))]))];
    }

    /// <summary>
    /// Gets the setting groups displayed in the window.
    /// </summary>
    public IReadOnlyList<SettingGroupViewModel> Groups { get; }

    /// <summary>
    /// Raised when the view model should be saved and closed.
    /// </summary>
    public event Action? SaveRequested;

    /// <summary>
    /// Raised when the settings window should close without saving.
    /// </summary>
    public event Action? CancelRequested;

    /// <summary>
    /// Raised when the view should browse for a setting value.
    /// </summary>
    public event Func<GameExtractionSettingViewModel, Task<string?>>? BrowseSettingRequested;

    /// <summary>
    /// Saves the current settings.
    /// </summary>
    [RelayCommand]
    public void Save()
    {
        foreach (SettingGroupViewModel group in Groups)
        {
            foreach (GameExtractionSettingViewModel setting in group.Settings)
            {
                setting.SaveTo(_settings);
            }
        }

        string path = GameExtractionSettings.GetDefaultSettingsPath(_appName);
        _settings.Save(path);
        SaveRequested?.Invoke();
    }

    /// <summary>
    /// Cancels editing.
    /// </summary>
    [RelayCommand]
    public void Cancel()
    {
        CancelRequested?.Invoke();
    }


    /// <summary>
    /// Opens the application's log directory in the default file explorer.
    /// </summary>
    [RelayCommand]
    public void OpenLog()
    {
        try
        {
            var logsDirectory = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), _appName, "logs");

            if (!Directory.Exists(logsDirectory))
                Directory.CreateDirectory(logsDirectory);

            Process.Start(new System.Diagnostics.ProcessStartInfo()
            {
                FileName = logsDirectory,
                UseShellExecute = true
            });
        }
        catch
        {
        }
    }

    private async Task<string?> BrowseSettingAsync(GameExtractionSettingViewModel setting)
    {
        if (BrowseSettingRequested is null)
        {
            return null;
        }

        return await BrowseSettingRequested.Invoke(setting).ConfigureAwait(true);
    }

    private static string GetGroupName(GameExtractionSetting setting)
    {
        string? groupName = string.IsNullOrWhiteSpace(setting.Group) ? setting.Category : setting.Group;
        return string.IsNullOrWhiteSpace(groupName) ? "General" : groupName.Trim();
    }
}
