using Avalonia.Controls;
using Avalonia.Platform.Storage;
using RedFox.GameExtraction.UI.ViewModels;

namespace RedFox.GameExtraction.UI.Views;

/// <summary>
/// Settings dialog view boundary for export configuration.
/// </summary>
public partial class SettingsWindow : Window
{
    /// <summary>
    /// Initializes a new instance of the <see cref="SettingsWindow"/> class.
    /// </summary>
    public SettingsWindow()
    {
        InitializeComponent();
    }

    /// <summary>
    /// Initializes the settings dialog.
    /// </summary>
    /// <param name="settings">The settings to edit.</param>
    /// <param name="settingDefinitions">The settings to display.</param>
    /// <param name="appName">The application name used for persistence.</param>
    public void Initialize(GameExtractionSettings settings, IReadOnlyList<GameExtractionSetting> settingDefinitions, string appName)
    {
        SettingsWindowViewModel viewModel = new(settings, settingDefinitions, appName);
        DataContext = viewModel;

        viewModel.BrowseSettingRequested += OnBrowseSettingRequested;
        viewModel.SaveRequested += () => Close();
        viewModel.CancelRequested += () => Close();
    }

    private async Task<string?> OnBrowseSettingRequested(GameExtractionSettingViewModel setting)
    {
        return setting.Type switch
        {
            GameExtractionSettingType.FilePath => await OnBrowseFileRequested(setting).ConfigureAwait(true),
            GameExtractionSettingType.DirectoryPath => await OnBrowseDirectoryRequested(setting).ConfigureAwait(true),
            _ => null,
        };
    }

    private async Task<string?> OnBrowseDirectoryRequested(GameExtractionSettingViewModel setting)
    {
        string? startPath = string.IsNullOrWhiteSpace(setting.TextValue) ? null : setting.TextValue;

        IStorageFolder? startFolder = startPath is not null && Directory.Exists(startPath)
            ? await StorageProvider.TryGetFolderFromPathAsync(startPath).ConfigureAwait(true)
            : null;

        IReadOnlyList<IStorageFolder> result = await StorageProvider.OpenFolderPickerAsync(new FolderPickerOpenOptions
        {
            Title = setting.PickerTitle,
            AllowMultiple = false,
            SuggestedStartLocation = startFolder,
        }).ConfigureAwait(true);

        return result.FirstOrDefault()?.TryGetLocalPath();
    }

    private async Task<string?> OnBrowseFileRequested(GameExtractionSettingViewModel setting)
    {
        IStorageFolder? startFolder = await ResolveStartFolderAsync(setting.TextValue).ConfigureAwait(true);
        IReadOnlyList<IStorageFile> result = await StorageProvider.OpenFilePickerAsync(new FilePickerOpenOptions
        {
            Title = setting.PickerTitle,
            AllowMultiple = false,
            FileTypeFilter = ParseFileFilter(setting.FileFilter),
            SuggestedStartLocation = startFolder,
        }).ConfigureAwait(true);

        return result.FirstOrDefault()?.TryGetLocalPath();
    }

    private async Task<IStorageFolder?> ResolveStartFolderAsync(string? path)
    {
        if (string.IsNullOrWhiteSpace(path))
        {
            return null;
        }

        if (Directory.Exists(path))
        {
            return await StorageProvider.TryGetFolderFromPathAsync(path).ConfigureAwait(true);
        }

        string? directory = Path.GetDirectoryName(path);
        return !string.IsNullOrWhiteSpace(directory) && Directory.Exists(directory)
            ? await StorageProvider.TryGetFolderFromPathAsync(directory).ConfigureAwait(true)
            : null;
    }

    private static IReadOnlyList<FilePickerFileType> ParseFileFilter(string? filter)
    {
        List<FilePickerFileType> types = [];
        if (!string.IsNullOrWhiteSpace(filter))
        {
            string[] parts = filter.Split('|', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries);
            for (int index = 0; index + 1 < parts.Length; index += 2)
            {
                string name = parts[index];
                List<string> patterns = [.. parts[index + 1]
                    .Split(';', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries)];

                if (patterns.Count > 0)
                {
                    types.Add(new FilePickerFileType(name) { Patterns = patterns });
                }
            }
        }

        if (types.Count == 0)
        {
            types.Add(new FilePickerFileType("All Files") { Patterns = ["*.*"] });
        }

        return types;
    }
}
