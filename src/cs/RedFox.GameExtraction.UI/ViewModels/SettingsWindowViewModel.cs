using System.Collections.ObjectModel;
using System.Diagnostics;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace RedFox.GameExtraction.UI.ViewModels;

/// <summary>
/// Provides editable settings for the settings window.
/// </summary>
public partial class SettingsWindowViewModel : ObservableObject
{
    private readonly string								_appName;
    private readonly GameExtractionSettings					_settings;
    private readonly IReadOnlyList<GameExtractionSetting>	_settingDefinitions;
    private readonly AssetManager							_assetManager;
    private bool											_removingProfile;

    /// <summary>
    /// Initializes a new instance of the <see cref="SettingsWindowViewModel"/> class.
    /// </summary>
    /// <param name="settings">The settings object to edit.</param>
    /// <param name="settingDefinitions">The settings to display.</param>
    /// <param name="appName">The application name used for persistence.</param>
    /// <param name="assetManager">The asset manager used to resolve setting options.</param>
    public SettingsWindowViewModel(GameExtractionSettings settings, IReadOnlyList<GameExtractionSetting> settingDefinitions, string appName, AssetManager assetManager)
    {
        _settings = settings ?? throw new ArgumentNullException(nameof(settings));
        ArgumentNullException.ThrowIfNull(settingDefinitions);
        ArgumentException.ThrowIfNullOrWhiteSpace(appName);
        _appName			= appName;
        _settingDefinitions	= settingDefinitions;
        _assetManager		= assetManager;

        Profiles		= [.. GameExtractionSettings.GetProfileNames(appName)];
        SelectedProfile	= GameExtractionSettings.GetActiveProfile(appName);
        Groups			= CreateGroups();
        SelectedGroup	= Groups.FirstOrDefault();
    }

    /// <summary>
    /// Gets the setting groups displayed in the window.
    /// </summary>
    [ObservableProperty]
    public partial IReadOnlyList<SettingGroupViewModel> Groups { get; set; }

    [ObservableProperty]
    public partial SettingGroupViewModel? SelectedGroup { get; set; }

    /// <summary>
    /// Gets the names of the settings profiles.
    /// </summary>
    public ObservableCollection<string> Profiles { get; }

    /// <summary>
    /// Gets or sets the active settings profile. Changing it saves the current edits and loads the selected profile.
    /// </summary>
    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(DeleteProfileCommand))]
    public partial string? SelectedProfile { get; set; }

    /// <summary>
    /// Gets or sets the name entered for a new profile.
    /// </summary>
    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(AddProfileCommand))]
    public partial string NewProfileName { get; set; } = string.Empty;

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
    /// Raised when the plugins management window should be opened.
    /// </summary>
    public event Action? OpenPluginsRequested;

    public event Action? ClearThumbnailCacheRequested;

    /// <summary>
    /// Raised when the view should ask the user to confirm an action.
    /// </summary>
    public event Func<ConfirmRequest, Task<bool?>>? ConfirmRequested;

    /// <summary>
    /// Opens the plugins management window.
    /// </summary>
    [RelayCommand]
    public void OpenPlugins()
    {
        OpenPluginsRequested?.Invoke();
    }

    /// <summary>
    /// Saves the current settings.
    /// </summary>
    [RelayCommand]
    public void Save()
    {
        SaveGroups();
        _settings.Save(GameExtractionSettings.GetDefaultSettingsPath(_appName));
        SaveRequested?.Invoke();
    }

    /// <summary>
    /// Creates a profile from the current edits and switches to it.
    /// </summary>
    [RelayCommand(CanExecute = nameof(CanAddProfile))]
    public void AddProfile()
    {
        string profile = NewProfileName;

        SaveGroups();
        _settings.Save(GameExtractionSettings.GetProfileSettingsPath(_appName, profile));
        Profiles.Add(profile);
        NewProfileName	= string.Empty;
        SelectedProfile	= profile;
    }

    /// <summary>
    /// Deletes the active profile after confirmation and switches to the default profile.
    /// </summary>
    [RelayCommand(CanExecute = nameof(CanDeleteProfile))]
    public async Task DeleteProfileAsync()
    {
        string profile = SelectedProfile!;

        if (await ConfirmAsync(new ConfirmRequest("Delete Profile", $"Delete the profile \"{profile}\"? Its settings are removed and the default profile becomes active.", "Delete", null)).ConfigureAwait(true) != true)
        {
            return;
        }

        GameExtractionSettings.DeleteProfile(_appName, profile);
        GameExtractionSettings.SetActiveProfile(_appName, GameExtractionSettings.DefaultProfileName);
        LoadActiveProfile();

        _removingProfile	= true;
        SelectedProfile		= GameExtractionSettings.DefaultProfileName;
        Profiles.Remove(profile);
        _removingProfile	= false;
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
            string logsDirectory = GameExtractionLogging.GetLogDirectory(_appName);

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

    [RelayCommand]
    public async Task ClearThumbnailCacheAsync()
    {
        if (await ConfirmAsync(new ConfirmRequest("Clear Thumbnail Cache", "Delete all cached thumbnails? They will be rendered again when next shown.", "Clear", null)).ConfigureAwait(true) == true)
        {
            ClearThumbnailCacheRequested?.Invoke();
        }
    }

    [RelayCommand]
    public async Task ClearLogsAsync()
    {
        if (await ConfirmAsync(new ConfirmRequest("Clear Logs", "Delete all log files? The log currently being written is kept.", "Clear", null)).ConfigureAwait(true) == true)
        {
            GameExtractionLogging.ClearLogs(_appName);
        }
    }

    partial void OnSelectedProfileChanged(string? oldValue, string? newValue)
    {
        if (_removingProfile || oldValue is null || newValue is null)
        {
            return;
        }

        SaveGroups();
        _settings.Save(GameExtractionSettings.GetProfileSettingsPath(_appName, oldValue));
        GameExtractionSettings.SetActiveProfile(_appName, newValue);
        LoadActiveProfile();
    }

    private bool CanAddProfile()
    {
        return GameExtractionSettings.IsValidProfileName(NewProfileName) && !Profiles.Contains(NewProfileName, StringComparer.OrdinalIgnoreCase);
    }

    private bool CanDeleteProfile()
    {
        return SelectedProfile is not null && !SelectedProfile.Equals(GameExtractionSettings.DefaultProfileName, StringComparison.OrdinalIgnoreCase);
    }

    private void LoadActiveProfile()
    {
        GameExtractionSettingGroup? selectedKind = SelectedGroup?.Kind;

        _settings.Values = new(StringComparer.OrdinalIgnoreCase);
        _settings.LoadFrom(GameExtractionSettings.GetDefaultSettingsPath(_appName));
        Groups			= CreateGroups();
        SelectedGroup	= Groups.FirstOrDefault(group => group.Kind == selectedKind) ?? Groups.FirstOrDefault();
    }

    private void SaveGroups()
    {
        foreach (SettingGroupViewModel group in Groups)
        {
            foreach (GameExtractionSettingViewModel setting in group.Settings)
            {
                setting.SaveTo(_settings);
            }
        }
    }

    private List<SettingGroupViewModel> CreateGroups()
    {
        List<SettingGroupViewModel> groups = [.. _settingDefinitions
            .DistinctBy(setting => setting.Name, StringComparer.OrdinalIgnoreCase)
            .GroupBy(setting => setting.Group)
            .OrderBy(group => GetGroupOrder(group.Key))
            .Select(group => new SettingGroupViewModel(
                group.Key,
                [.. group.Select(setting => new GameExtractionSettingViewModel(setting, _settings, _assetManager, BrowseSettingAsync))]))];

        Dictionary<string, GameExtractionSettingViewModel> settingsByName = groups.SelectMany(group => group.Settings).ToDictionary(setting => setting.Name, StringComparer.OrdinalIgnoreCase);
        foreach (GameExtractionSettingViewModel setting in settingsByName.Values)
        {
            if (setting.DependsOn is not null && settingsByName.TryGetValue(setting.DependsOn, out GameExtractionSettingViewModel? parent))
            {
                setting.Parent = parent;
            }
        }

        return groups;
    }

    private async Task<string?> BrowseSettingAsync(GameExtractionSettingViewModel setting)
    {
        if (BrowseSettingRequested is null)
        {
            return null;
        }

        return await BrowseSettingRequested.Invoke(setting).ConfigureAwait(true);
    }

    private Task<bool?> ConfirmAsync(ConfirmRequest request)
    {
        if (ConfirmRequested is null)
        {
            return Task.FromResult<bool?>(true);
        }

        return ConfirmRequested.Invoke(request);
    }

    private static int GetGroupOrder(GameExtractionSettingGroup group) => group switch
    {
        GameExtractionSettingGroup.General		=> 0,
        GameExtractionSettingGroup.Export		=> 1,
        GameExtractionSettingGroup.Model		=> 2,
        GameExtractionSettingGroup.Animation	=> 3,
        GameExtractionSettingGroup.Image		=> 4,
        GameExtractionSettingGroup.Sound		=> 5,
        GameExtractionSettingGroup.Archive		=> 6,
        GameExtractionSettingGroup.Document		=> 7,
        GameExtractionSettingGroup.Script		=> 8,
        GameExtractionSettingGroup.Table		=> 9,
        GameExtractionSettingGroup.Binary		=> 10,
        GameExtractionSettingGroup.Map			=> 11,
        GameExtractionSettingGroup.Shader		=> 12,
        GameExtractionSettingGroup.Effect		=> 13,
        GameExtractionSettingGroup.Cutscene		=> 14,
        GameExtractionSettingGroup.Preview		=> 15,
        GameExtractionSettingGroup.Other		=> 16,
        _										=> int.MaxValue,
    };

}
