using Avalonia.Controls;
using Avalonia.Platform.Storage;
using RedFox.GameExtraction;
using RedFox.GameExtraction.UI.ViewModels;

namespace RedFox.GameExtraction.UI.Views;

public partial class MainWindow : Window
{
    private PreviewWindow? _previewWindow;

    public MainWindow()
    {
        InitializeComponent();
    }

    public void Initialize(GameExtractionConfig config)
    {
        var vm = new MainWindowViewModel(config);
        DataContext = vm;

        vm.SettingsRequested += OnSettingsRequested;
        vm.AboutRequested += OnAboutRequested;
        vm.PreviewRequested += OnPreviewRequested;
        vm.SourceManagerRequested += OnSourceManagerRequested;
        vm.FileDialogRequested += OnFileDialogRequested;
        vm.SelectionChanged += OnSelectionChanged;
    }

    private async Task<IReadOnlyList<string>> OnFileDialogRequested()
    {
        if (DataContext is not MainWindowViewModel vm) return [];

        var filters = ParseFileFilter(vm.Config.GameSource.FileFilter);

        var result = await StorageProvider.OpenFilePickerAsync(new FilePickerOpenOptions
        {
            Title = "Select file(s) to load",
            AllowMultiple = true,
            FileTypeFilter = filters
        });

        return result
            .Select(f => f.TryGetLocalPath())
            .Where(p => p is not null)
            .Cast<string>()
            .ToList();
    }

    private void OnSettingsRequested()
    {
        if (DataContext is not MainWindowViewModel vm) return;

        var settingsWindow = new SettingsWindow();
        settingsWindow.Initialize(vm.Config.Settings, vm.Config.AppName);
        settingsWindow.ShowDialog(this);
    }

    private void OnAboutRequested()
    {
        if (DataContext is not MainWindowViewModel vm) return;

        var aboutWindow = new AboutWindow();
        aboutWindow.Initialize(vm.Config);
        aboutWindow.ShowDialog(this);
    }

    private void OnPreviewRequested(IAssetEntry asset)
    {
        // Open the preview window or bring it to front
        EnsurePreviewWindow();
        if (DataContext is MainWindowViewModel vm)
        {
            var selected = vm.AssetList.SelectedAssets
                .Select(a => a.Entry)
                .ToList();
            if (selected.Count == 0)
                selected.Add(asset);
            _ = UpdatePreviewAsync(selected);
        }
    }

    private async void OnSelectionChanged()
    {
        if (_previewWindow is null || !_previewWindow.IsVisible) return;
        if (DataContext is not MainWindowViewModel vm) return;

        var selected = vm.AssetList.SelectedAssets
            .Select(a => a.Entry)
            .ToList();
        await UpdatePreviewAsync(selected);
    }

    private void EnsurePreviewWindow()
    {
        if (_previewWindow is null || !_previewWindow.IsVisible)
        {
            _previewWindow = new PreviewWindow();
            if (DataContext is MainWindowViewModel vm)
                _previewWindow.ApplySettings(vm.Config.Settings);
            _previewWindow.Closed += (_, _) => _previewWindow = null;
            _previewWindow.Show(this);
        }
        else
        {
            _previewWindow.Activate();
        }
    }

    private async Task UpdatePreviewAsync(IReadOnlyList<IAssetEntry> assets)
    {
        if (_previewWindow is null) return;
        if (DataContext is not MainWindowViewModel vm) return;
        if (vm.Config.PreviewHandler is null) return;

        await _previewWindow.UpdatePreviewAsync(assets, vm.Config.PreviewHandler);
    }

    private void OnSourceManagerRequested()
    {
        if (DataContext is not MainWindowViewModel vm) return;

        var managerWindow = new SourceManagerWindow();
        managerWindow.Initialize(vm);
        managerWindow.ShowDialog(this);
    }

    /// <summary>
    /// Parses a filter string like "ZIP Archives|*.zip|All Files|*.*"
    /// into Avalonia FilePickerFileType list.
    /// </summary>
    private static List<FilePickerFileType> ParseFileFilter(string filter)
    {
        var types = new List<FilePickerFileType>();
        var parts = filter.Split('|');

        for (int i = 0; i + 1 < parts.Length; i += 2)
        {
            var name = parts[i].Trim();
            var patterns = parts[i + 1].Trim().Split(';')
                .Select(p => p.Trim())
                .ToList();

            types.Add(new FilePickerFileType(name) { Patterns = patterns });
        }

        if (types.Count == 0)
            types.Add(new FilePickerFileType("All Files") { Patterns = ["*.*"] });

        return types;
    }
}
