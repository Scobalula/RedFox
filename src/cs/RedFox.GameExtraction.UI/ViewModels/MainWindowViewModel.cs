using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using RedFox.GameExtraction;
using RedFox.GameExtraction.UI.Models;

namespace RedFox.GameExtraction.UI.ViewModels;

public partial class MainWindowViewModel : ObservableObject
{
    private readonly GameExtractionConfig _config;
    private CancellationTokenSource? _currentCts;

    public MainWindowViewModel(GameExtractionConfig config)
    {
        _config = config;
        AssetList = new AssetListViewModel(config.GameSource.MetadataColumns);
        Sidebar = new SidebarViewModel(config);
        StatusBar = new StatusBarViewModel();

        // Wire sidebar commands
        Sidebar.LoadSourceCommand = new AsyncRelayCommand(
            RequestLoadFilesAsync,
            () => !IsLoading);
        Sidebar.LoadGameCommand = new AsyncRelayCommand(
            LoadGameAsync,
            () => !IsLoading && Capabilities.SupportsLoadFromMemory);
        Sidebar.ClearAllCommand = new RelayCommand(
            ClearAll,
            () => LoadedSources.Count > 0);
        Sidebar.ManageSourcesCommand = new RelayCommand(
            OpenSourceManager,
            () => LoadedSources.Count > 0);
        Sidebar.ExportAllCommand = new AsyncRelayCommand(
            ExportAllAsync,
            () => !IsLoading && AssetList.Assets.Count > 0);
        Sidebar.ExportSelectedCommand = new AsyncRelayCommand(
            ExportSelectedAsync,
            () => !IsLoading && AssetList.SelectedAssets.Count > 0);
        Sidebar.OpenPreviewCommand = new RelayCommand(
            OpenPreview,
            () => AssetList.SelectedAssets.Count == 1 && Capabilities.SupportsPreview);
        Sidebar.OpenSettingsCommand = new RelayCommand(OpenSettings);
        Sidebar.OpenAboutCommand = new RelayCommand(OpenAbout);

        LoadedSources.CollectionChanged += (_, _) =>
        {
            Sidebar.SourceCount = LoadedSources.Count;
            Sidebar.ManageSourcesCommand?.NotifyCanExecuteChanged();
        };
    }

    public AssetListViewModel AssetList { get; }
    public SidebarViewModel Sidebar { get; }
    public StatusBarViewModel StatusBar { get; }
    public GameCapabilities Capabilities => _config.GameSource.Capabilities;
    public GameExtractionConfig Config => _config;

    /// <summary>All currently loaded sources.</summary>
    public ObservableCollection<LoadedSource> LoadedSources { get; } = [];

    [ObservableProperty]
    public partial bool IsLoading { get; set; }

    [ObservableProperty]
    public partial bool ShowProgressDialog { get; set; }

    [ObservableProperty]
    public partial ProgressDialogViewModel? ProgressDialog { get; set; }

    // Events for the view to handle window/dialog creation
    public event Action? SettingsRequested;
    public event Action? AboutRequested;
    public event Action<IAssetEntry>? PreviewRequested;
    public event Action? SourceManagerRequested;
    public event Action? SelectionChanged;

    /// <summary>
    /// Raised when the VM needs the view to open a file dialog.
    /// The view should return the chosen file paths (supports multi-select).
    /// </summary>
    public event Func<Task<IReadOnlyList<string>>>? FileDialogRequested;

    private async Task RequestLoadFilesAsync()
    {
        if (FileDialogRequested is null) return;

        var filePaths = await FileDialogRequested.Invoke();
        foreach (var filePath in filePaths)
        {
            if (!string.IsNullOrEmpty(filePath))
                await LoadSourceFromFileAsync(filePath);
        }
    }

    private async Task LoadGameAsync()
    {
        IsLoading = true;
        _currentCts = new CancellationTokenSource();
        var cts = _currentCts;
        var progressVm = new ProgressDialogViewModel("Loading from game...");
        progressVm.CancelCommand = new RelayCommand(() =>
        {
            progressVm.IsCancelling = true;
            progressVm.StatusText = "Cancelling...";
            progressVm.IsIndeterminate = true;
            cts.Cancel();
        });
        ProgressDialog = progressVm;
        ShowProgressDialog = true;

        StatusBar.StatusText = "Loading from game...";

        try
        {
            var progress = new Progress<ProgressInfo>(info =>
            {
                if (progressVm.IsCancelling) return;
                progressVm.StatusText = info.Status;
                progressVm.Current = info.Current;
                progressVm.Total = info.Total;
                progressVm.IsIndeterminate = info.Total <= 0;
                if (info.Percentage.HasValue)
                    progressVm.ProgressValue = info.Percentage.Value * 100;
            });

            var source = await Task.Run(async () => await _config.GameSource.LoadAssetsFromMemoryAsync(
                progress, cts.Token));

            // Clear any previously loaded game sources (reload scenario)
            for (var i = LoadedSources.Count - 1; i >= 0; i--)
            {
                if (string.IsNullOrEmpty(LoadedSources[i].Location))
                {
                    _config.GameSource.UnloadSource(LoadedSources[i]);
                    AssetList.RemoveAssets(LoadedSources[i].Assets);
                    LoadedSources.RemoveAt(i);
                }
            }

            LoadedSources.Add(source);
            AssetList.AddAssets(source.Assets);
            StatusBar.StatusText = "Ready";
            StatusBar.AssetCount = AssetList.TotalCount;
        }
        catch (OperationCanceledException)
        {
            StatusBar.StatusText = "Loading cancelled";
        }
        catch (Exception ex)
        {
            StatusBar.StatusText = $"Error: {ex.Message}";
        }
        finally
        {
            ShowProgressDialog = false;
            ProgressDialog = null;
            IsLoading = false;
            _currentCts?.Dispose();
            _currentCts = null;
            RefreshCommands();
        }
    }

    /// <summary>
    /// Load assets from the given file path and add them as a new source.
    /// Skips if the file is already loaded.
    /// </summary>
    public async Task LoadSourceFromFileAsync(string filePath)
    {
        // Prevent duplicate file loads
        if (LoadedSources.Any(s => string.Equals(s.Location, filePath, StringComparison.OrdinalIgnoreCase)))
        {
            StatusBar.StatusText = $"Already loaded: {Path.GetFileName(filePath)}";
            return;
        }

        IsLoading = true;
        _currentCts = new CancellationTokenSource();
        var cts = _currentCts;
        var fileName = Path.GetFileName(filePath);
        var progressVm = new ProgressDialogViewModel($"Loading {fileName}...");
        progressVm.CancelCommand = new RelayCommand(() =>
        {
            progressVm.IsCancelling = true;
            progressVm.StatusText = "Cancelling...";
            progressVm.IsIndeterminate = true;
            cts.Cancel();
        });
        ProgressDialog = progressVm;
        ShowProgressDialog = true;

        StatusBar.StatusText = $"Loading {fileName}...";

        try
        {
            var progress = new Progress<ProgressInfo>(info =>
            {
                if (progressVm.IsCancelling) return;
                progressVm.StatusText = info.Status;
                progressVm.Current = info.Current;
                progressVm.Total = info.Total;
                progressVm.IsIndeterminate = info.Total <= 0;
                if (info.Percentage.HasValue)
                    progressVm.ProgressValue = info.Percentage.Value * 100;
            });

            var source = await Task.Run(async () => await _config.GameSource.LoadAssetsAsync(
                filePath, progress, cts.Token));

            LoadedSources.Add(source);
            AssetList.AddAssets(source.Assets);
            StatusBar.StatusText = "Ready";
            StatusBar.AssetCount = AssetList.TotalCount;
        }
        catch (OperationCanceledException)
        {
            StatusBar.StatusText = "Loading cancelled";
        }
        catch (Exception ex)
        {
            StatusBar.StatusText = $"Error: {ex.Message}";
        }
        finally
        {
            ShowProgressDialog = false;
            ProgressDialog = null;
            IsLoading = false;
            _currentCts?.Dispose();
            _currentCts = null;
            RefreshCommands();
        }
    }

    /// <summary>
    /// Unload a source, removing its assets from the list.
    /// </summary>
    public void UnloadSource(LoadedSource source)
    {
        _config.GameSource.UnloadSource(source);
        AssetList.RemoveAssets(source.Assets);
        LoadedSources.Remove(source);
        StatusBar.AssetCount = AssetList.TotalCount;
        StatusBar.StatusText = $"Unloaded {source.DisplayName}";
        RefreshCommands();
    }

    private async Task ExportAllAsync()
    {
        await ExportAssetsAsync(AssetList.Assets.Select(a => a.Entry));
    }

    private async Task ExportSelectedAsync()
    {
        await ExportAssetsAsync(AssetList.SelectedAssets.Select(a => a.Entry));
    }

    /// <summary>
    /// Export a single asset entry (e.g., triggered by double-click).
    /// </summary>
    public async Task ExportSingleEntryAsync(IAssetEntry entry)
    {
        await ExportAssetsAsync([entry]);
    }

    private async Task ExportAssetsAsync(IEnumerable<IAssetEntry> assets)
    {
        var assetList = assets.ToList();
        if (assetList.Count == 0) return;

        IsLoading = true;
        _currentCts = new CancellationTokenSource();
        var cts = _currentCts;
        var progressVm = new ProgressDialogViewModel($"Exporting {assetList.Count} assets...");
        progressVm.CancelCommand = new RelayCommand(() =>
        {
            progressVm.IsCancelling = true;
            progressVm.StatusText = "Cancelling...";
            progressVm.IsIndeterminate = true;
            cts.Cancel();
        });
        ProgressDialog = progressVm;
        ShowProgressDialog = true;

        StatusBar.StatusText = $"Exporting {assetList.Count} assets...";

        try
        {
            var progress = new Progress<ProgressInfo>(info =>
            {
                if (progressVm.IsCancelling) return;
                progressVm.StatusText = info.Status;
                progressVm.Current = info.Current;
                progressVm.Total = info.Total;
                progressVm.IsIndeterminate = info.Total <= 0;
                if (info.Percentage.HasValue)
                    progressVm.ProgressValue = info.Percentage.Value * 100;
            });

            await Task.Run(async () => await _config.Exporter.ExportAsync(
                assetList,
                _config.Settings.OutputDirectory,
                _config.Settings,
                progress,
                cts.Token));

            StatusBar.StatusText = $"Exported {assetList.Count} assets successfully";
        }
        catch (OperationCanceledException)
        {
            StatusBar.StatusText = "Export cancelled";
        }
        catch (Exception ex)
        {
            StatusBar.StatusText = $"Export error: {ex.Message}";
        }
        finally
        {
            ShowProgressDialog = false;
            ProgressDialog = null;
            IsLoading = false;
            _currentCts?.Dispose();
            _currentCts = null;
            RefreshCommands();
        }
    }

    private void OpenPreview()
    {
        var selected = AssetList.SelectedAssets.FirstOrDefault();
        if (selected is not null)
            PreviewRequested?.Invoke(selected.Entry);
    }

    private void OpenSettings() => SettingsRequested?.Invoke();
    private void OpenAbout() => AboutRequested?.Invoke();
    private void OpenSourceManager() => SourceManagerRequested?.Invoke();

    public void ClearAll()
    {
        foreach (var source in LoadedSources)
        {
            _config.GameSource.UnloadSource(source);
        }

        AssetList.Clear();
        LoadedSources.Clear();
        StatusBar.AssetCount = 0;
        StatusBar.StatusText = "Cleared all sources";
    }


    private void RefreshCommands()
    {
        Sidebar.LoadSourceCommand?.NotifyCanExecuteChanged();
        Sidebar.LoadGameCommand?.NotifyCanExecuteChanged();
        Sidebar.ManageSourcesCommand?.NotifyCanExecuteChanged();
        Sidebar.ClearAllCommand?.NotifyCanExecuteChanged();
        Sidebar.ExportAllCommand?.NotifyCanExecuteChanged();
        Sidebar.ExportSelectedCommand?.NotifyCanExecuteChanged();
        Sidebar.OpenPreviewCommand?.NotifyCanExecuteChanged();
    }

    public void NotifySelectionChanged()
    {
        Sidebar.ExportSelectedCommand?.NotifyCanExecuteChanged();
        Sidebar.OpenPreviewCommand?.NotifyCanExecuteChanged();
        SelectionChanged?.Invoke();
    }
}
