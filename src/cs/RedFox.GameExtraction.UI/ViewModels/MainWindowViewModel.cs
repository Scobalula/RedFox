using System.Collections.ObjectModel;
using Avalonia.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using RedFox.GameExtraction;
using RedFox.GameExtraction.UI.Models;

namespace RedFox.GameExtraction.UI.ViewModels;

/// <summary>
/// Coordinates mounted sources, asset rows, and export operations for the main window.
/// </summary>
public partial class MainWindowViewModel : ObservableObject
{
    private readonly AssetManager _assetManager;
    private readonly GameExtractionConfig _config;
    private CancellationTokenSource? _currentCts;

    /// <summary>
    /// Initializes a new instance of the <see cref="MainWindowViewModel"/> class.
    /// </summary>
    /// <param name="config">The application configuration.</param>
    public MainWindowViewModel(GameExtractionConfig config)
    {
        _config = config ?? throw new ArgumentNullException(nameof(config));
        _assetManager = config.AssetManagerFactory();
        _assetManager.OperationFailed += OnOperationFailed;
        _assetManager.AssetExportCompleted += OnAssetExportCompleted;

        AssetList = new AssetListViewModel();
        Sidebar = new SidebarViewModel(config);
        StatusBar = new StatusBarViewModel();

        Sidebar.LoadSourceCommand = new AsyncRelayCommand(RequestLoadFilesAsync, () => !IsLoading && config.SupportsFileSources);
        Sidebar.LoadDirectoryCommand = new AsyncRelayCommand(RequestLoadDirectoryAsync, () => !IsLoading && config.SupportsDirectorySources);
        Sidebar.LoadProcessCommand = new AsyncRelayCommand(LoadProcessAsync, () => !IsLoading && config.SupportsProcessSources);
        Sidebar.ClearAllCommand = new AsyncRelayCommand(ClearAllAsync, () => !IsLoading && LoadedSources.Count > 0);
        Sidebar.ManageSourcesCommand = new RelayCommand(OpenSourceManager, () => LoadedSources.Count > 0);
        Sidebar.ExportAllCommand = new AsyncRelayCommand(ExportAllAsync, () => !IsLoading && AssetList.TotalCount > 0);
        Sidebar.ExportSelectedCommand = new AsyncRelayCommand(ExportSelectedAsync, () => !IsLoading && AssetList.SelectedAssets.Count > 0);
        Sidebar.OpenSettingsCommand = new RelayCommand(OpenSettings);
        Sidebar.OpenAboutCommand = new RelayCommand(OpenAbout);

        LoadedSources.CollectionChanged += (_, _) =>
        {
            Sidebar.SourceCount = LoadedSources.Count;
            RefreshCommands();
        };
    }

    /// <summary>
    /// Gets the asset list view model.
    /// </summary>
    public AssetListViewModel AssetList { get; }

    /// <summary>
    /// Gets the sidebar view model.
    /// </summary>
    public SidebarViewModel Sidebar { get; }

    /// <summary>
    /// Gets the status bar view model.
    /// </summary>
    public StatusBarViewModel StatusBar { get; }

    /// <summary>
    /// Gets the application configuration.
    /// </summary>
    public GameExtractionConfig Config => _config;

    /// <summary>
    /// Gets the mounted source rows.
    /// </summary>
    public ObservableCollection<AssetSourceViewModel> LoadedSources { get; } = [];

    /// <summary>
    /// Gets or sets a value indicating whether an operation is running.
    /// </summary>
    [ObservableProperty]
    public partial bool IsLoading { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether the progress overlay is visible.
    /// </summary>
    [ObservableProperty]
    public partial bool ShowProgressDialog { get; set; }

    /// <summary>
    /// Gets or sets the progress dialog state.
    /// </summary>
    [ObservableProperty]
    public partial ProgressDialogViewModel? ProgressDialog { get; set; }

    /// <summary>
    /// Raised when the settings window should be opened.
    /// </summary>
    public event Action? SettingsRequested;

    /// <summary>
    /// Raised when the about window should be opened.
    /// </summary>
    public event Action? AboutRequested;

    /// <summary>
    /// Raised when the source manager window should be opened.
    /// </summary>
    public event Action? SourceManagerRequested;

    /// <summary>
    /// Raised when selection changes in the asset list.
    /// </summary>
    public event Action? SelectionChanged;

    /// <summary>
    /// Raised when file paths are needed from the view.
    /// </summary>
    public event Func<Task<IReadOnlyList<string>>>? FileDialogRequested;

    /// <summary>
    /// Raised when a directory path is needed from the view.
    /// </summary>
    public event Func<Task<string?>>? FolderDialogRequested;

    /// <summary>
    /// Loads assets from a file path.
    /// </summary>
    /// <param name="filePath">The file path to mount.</param>
    /// <returns>A task that completes when loading finishes.</returns>
    public async Task LoadSourceFromFileAsync(string filePath)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(filePath);

        string fullPath = Path.GetFullPath(filePath);
        if (HasMountedLocation(AssetSourceKind.File, fullPath))
        {
            StatusBar.StatusText = $"Already loaded: {Path.GetFileName(fullPath)}";
            return;
        }

        await MountSourceAsync(
            $"Loading {Path.GetFileName(fullPath)}...",
            (progress, cancellationToken) => _assetManager.MountFileAsync(
                fullPath,
                _config.SourceOptions,
                progress,
                cancellationToken)).ConfigureAwait(false);
    }

    /// <summary>
    /// Loads assets from a directory path.
    /// </summary>
    /// <param name="directoryPath">The directory path to mount.</param>
    /// <returns>A task that completes when loading finishes.</returns>
    public async Task LoadSourceFromDirectoryAsync(string directoryPath)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(directoryPath);

        string fullPath = Path.GetFullPath(directoryPath);
        if (HasMountedLocation(AssetSourceKind.Directory, fullPath))
        {
            StatusBar.StatusText = $"Already loaded: {Path.GetFileName(fullPath)}";
            return;
        }

        await MountSourceAsync(
            $"Loading {Path.GetFileName(fullPath)}...",
            (progress, cancellationToken) => _assetManager.MountDirectoryAsync(
                fullPath,
                _config.SourceOptions,
                progress,
                cancellationToken)).ConfigureAwait(false);
    }

    /// <summary>
    /// Unloads a mounted source.
    /// </summary>
    /// <param name="source">The source row to unload.</param>
    /// <returns>A task that completes when the source is unloaded.</returns>
    public async Task UnloadSourceAsync(AssetSourceViewModel source)
    {
        ArgumentNullException.ThrowIfNull(source);

        if (!LoadedSources.Contains(source))
        {
            return;
        }

        try
        {
            await _assetManager.UnloadAsync(source.Source).ConfigureAwait(false);
            await Dispatcher.UIThread.InvokeAsync(() =>
            {
                AssetList.RemoveSource(source);
                LoadedSources.Remove(source);
                StatusBar.AssetCount = AssetList.TotalCount;
                StatusBar.StatusText = $"Unloaded {source.DisplayName}";
                RefreshCommands();
            });
        }
        catch (Exception exception)
        {
            StatusBar.StatusText = $"Unload error: {exception.Message}";
        }
    }

    /// <summary>
    /// Exports one asset row.
    /// </summary>
    /// <param name="asset">The asset row to export.</param>
    /// <returns>A task that completes when export finishes.</returns>
    public Task ExportSingleAssetAsync(AssetRowViewModel asset)
    {
        ArgumentNullException.ThrowIfNull(asset);
        return ExportAssetsAsync([asset]);
    }

    /// <summary>
    /// Refreshes command state after asset selection changes.
    /// </summary>
    public void NotifySelectionChanged()
    {
        Sidebar.ExportSelectedCommand?.NotifyCanExecuteChanged();
        SelectionChanged?.Invoke();
    }

    partial void OnIsLoadingChanged(bool value)
    {
        RefreshCommands();
    }

    private async Task RequestLoadFilesAsync()
    {
        if (FileDialogRequested is null)
        {
            return;
        }

        IReadOnlyList<string> filePaths = await FileDialogRequested.Invoke().ConfigureAwait(true);
        foreach (string filePath in filePaths)
        {
            if (!string.IsNullOrWhiteSpace(filePath))
            {
                await LoadSourceFromFileAsync(filePath).ConfigureAwait(true);
            }
        }
    }

    private async Task RequestLoadDirectoryAsync()
    {
        if (FolderDialogRequested is null)
        {
            return;
        }

        string? directoryPath = await FolderDialogRequested.Invoke().ConfigureAwait(true);
        if (!string.IsNullOrWhiteSpace(directoryPath))
        {
            await LoadSourceFromDirectoryAsync(directoryPath).ConfigureAwait(true);
        }
    }

    private async Task LoadProcessAsync()
    {
        if (_config.ProcessId is int processId)
        {
            string location = $"PID {processId}";
            if (HasMountedLocation(AssetSourceKind.Process, location))
            {
                StatusBar.StatusText = $"Already loaded: {location}";
                return;
            }

            await MountSourceAsync(
                $"Loading {location}...",
                (progress, cancellationToken) => _assetManager.MountProcessAsync(
                    processId,
                    _config.SourceOptions,
                    progress,
                    cancellationToken)).ConfigureAwait(false);
            return;
        }

        if (!string.IsNullOrWhiteSpace(_config.ProcessName))
        {
            string processName = _config.ProcessName.Trim();
            if (HasMountedLocation(AssetSourceKind.Process, processName))
            {
                StatusBar.StatusText = $"Already loaded: {processName}";
                return;
            }

            await MountSourceAsync(
                $"Loading {processName}...",
                (progress, cancellationToken) => _assetManager.MountProcessAsync(
                    processName,
                    _config.SourceOptions,
                    progress,
                    cancellationToken)).ConfigureAwait(false);
            return;
        }

        StatusBar.StatusText = "No process target is configured.";
    }

    private async Task MountSourceAsync(
        string title,
        Func<IProgress<string>, CancellationToken, Task<IAssetSource>> mountSourceAsync)
    {
        IsLoading = true;
        _currentCts = new CancellationTokenSource();
        CancellationTokenSource cts = _currentCts;
        ProgressDialogViewModel progressVm = CreateProgressDialog(title, cts);
        ProgressDialog = progressVm;
        ShowProgressDialog = true;
        StatusBar.StatusText = title.TrimEnd('.');

        try
        {
            Progress<string> progress = new(message =>
            {
                if (!progressVm.IsCancelling)
                {
                    progressVm.StatusText = message;
                }
            });

            IAssetSource source = await Task.Run(
                () => mountSourceAsync(progress, cts.Token),
                cts.Token).ConfigureAwait(true);

            AddMountedSource(source);
            StatusBar.StatusText = "Ready";
        }
        catch (OperationCanceledException)
        {
            StatusBar.StatusText = "Loading cancelled";
        }
        catch (Exception exception)
        {
            StatusBar.StatusText = $"Load error: {exception.Message}";
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

    private void AddMountedSource(IAssetSource source)
    {
        _assetManager.TryGetSourceRequest(source, out AssetSourceRequest? request);
        AssetSourceViewModel sourceRow = new(source, request);
        LoadedSources.Add(sourceRow);
        AssetList.AddSource(sourceRow);
        StatusBar.AssetCount = AssetList.TotalCount;
    }

    private async Task ExportAllAsync()
    {
        await ExportAssetsAsync(AssetList.AllAssets).ConfigureAwait(false);
    }

    private async Task ExportSelectedAsync()
    {
        await ExportAssetsAsync(AssetList.SelectedAssets).ConfigureAwait(false);
    }

    private async Task ExportAssetsAsync(IEnumerable<AssetRowViewModel> rows)
    {
        List<AssetRowViewModel> rowList = rows.ToList();
        if (rowList.Count == 0)
        {
            return;
        }

        IsLoading = true;
        _currentCts = new CancellationTokenSource();
        CancellationTokenSource cts = _currentCts;
        ProgressDialogViewModel progressVm = CreateProgressDialog($"Exporting {rowList.Count:N0} assets...", cts);
        progressVm.Total = rowList.Count;
        progressVm.IsIndeterminate = false;
        ProgressDialog = progressVm;
        ShowProgressDialog = true;
        StatusBar.StatusText = $"Exporting {rowList.Count:N0} assets...";

        try
        {
            ExportConfiguration configuration = _config.Settings.ToExportConfiguration();
            Progress<string> progress = new(message =>
            {
                if (!progressVm.IsCancelling)
                {
                    progressVm.StatusText = message;
                }
            });

            List<Asset> assets = rowList.Select(row => row.Asset).ToList();
            await Task.Run(
                () => _assetManager.ExportAsync(assets, configuration, progress, cts.Token),
                cts.Token).ConfigureAwait(true);

            StatusBar.StatusText = $"Exported {rowList.Count:N0} assets";
        }
        catch (OperationCanceledException)
        {
            StatusBar.StatusText = "Export cancelled";
        }
        catch (Exception exception)
        {
            StatusBar.StatusText = $"Export error: {exception.Message}";
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

    private async Task ClearAllAsync()
    {
        foreach (AssetSourceViewModel source in LoadedSources.ToArray())
        {
            await UnloadSourceAsync(source).ConfigureAwait(true);
        }

        AssetList.Clear();
        LoadedSources.Clear();
        StatusBar.AssetCount = 0;
        StatusBar.StatusText = "Cleared all sources";
        RefreshCommands();
    }

    private bool HasMountedLocation(AssetSourceKind kind, string location)
    {
        return LoadedSources.Any(source =>
            source.Request?.Kind == kind &&
            string.Equals(source.Location, location, StringComparison.OrdinalIgnoreCase));
    }

    private void OpenSettings()
    {
        SettingsRequested?.Invoke();
    }

    private void OpenAbout()
    {
        AboutRequested?.Invoke();
    }

    private void OpenSourceManager()
    {
        SourceManagerRequested?.Invoke();
    }

    private void RefreshCommands()
    {
        Sidebar.LoadSourceCommand?.NotifyCanExecuteChanged();
        Sidebar.LoadDirectoryCommand?.NotifyCanExecuteChanged();
        Sidebar.LoadProcessCommand?.NotifyCanExecuteChanged();
        Sidebar.ManageSourcesCommand?.NotifyCanExecuteChanged();
        Sidebar.ClearAllCommand?.NotifyCanExecuteChanged();
        Sidebar.ExportAllCommand?.NotifyCanExecuteChanged();
        Sidebar.ExportSelectedCommand?.NotifyCanExecuteChanged();
    }

    private static ProgressDialogViewModel CreateProgressDialog(string title, CancellationTokenSource cts)
    {
        ProgressDialogViewModel progressVm = new(title);
        progressVm.CancelCommand = new RelayCommand(() =>
        {
            progressVm.IsCancelling = true;
            progressVm.StatusText = "Cancelling...";
            progressVm.IsIndeterminate = true;
            cts.Cancel();
        });
        return progressVm;
    }

    private void OnOperationFailed(object? sender, AssetOperationFailedEventArgs args)
    {
        Dispatcher.UIThread.Post(() =>
        {
            StatusBar.StatusText = $"{args.Operation} error: {args.Exception.Message}";
        });
    }

    private void OnAssetExportCompleted(object? sender, AssetExportCompletedEventArgs args)
    {
        ProgressDialogViewModel? progressVm = ProgressDialog;
        if (progressVm is null)
        {
            return;
        }

        Dispatcher.UIThread.Post(() =>
        {
            if (!ReferenceEquals(ProgressDialog, progressVm))
            {
                return;
            }

            progressVm.IsIndeterminate = false;
            progressVm.Current++;
            if (progressVm.Total < progressVm.Current)
            {
                progressVm.Total = progressVm.Current;
            }

            progressVm.ProgressValue = progressVm.Total > 0
                ? Math.Min(100, progressVm.Current / (double)progressVm.Total * 100)
                : 0;
        });
    }
}
