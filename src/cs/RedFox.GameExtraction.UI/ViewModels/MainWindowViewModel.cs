using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO.Enumeration;
using Avalonia.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using RedFox.GameExtraction;
using RedFox.GameExtraction.UI.Models;
using RedFox.Graphics3D.IO;
using RedFox.Plugins;
using RedFox.Plugins.Python;

namespace RedFox.GameExtraction.UI.ViewModels;

/// <summary>
/// Coordinates mounted sources, asset rows, and export operations for the main window.
/// </summary>
public partial class MainWindowViewModel : ObservableObject, IDisposable
{
    private readonly AssetManager _assetManager;
    private readonly GameExtractionConfig _config;
    private readonly List<AssetRowViewModel> _allAssets = [];
    private readonly RangeObservableCollection<AssetRowViewModel> _assetsView = [];
    private readonly SynchronizationContext? _uiSynchronizationContext;
    private readonly Timer _searchFilterTimer;
    private readonly Timer _previewSelectionTimer;
    private readonly object _progressUpdateLock = new();
    private readonly object _filterCancellationLock = new();
    private string _assetNameFilter = string.Empty;
    private CancellationTokenSource? _currentCts;
    private CancellationTokenSource? _filterCancellationSource;
    private ProgressDialogViewModel? _queuedProgressDialog;
    private string? _queuedProgressStatus;
    private int _queuedCompletedOperations;
    private int _filterGeneration;
    private int _filterOptionsGeneration;
    private bool _progressUpdatePosted;
    private bool _isPreviewWindowOpen;
    private bool _isReplacingAssetsView;
    private volatile bool _isDisposed;
    private AssetRowViewModel[]? _pendingPreviewSelection;
    private AssetRowViewModel[]? _debouncedPreviewSelection;
    private int _previewDebounceGeneration;
    private int _exportFailureCount;
    private bool _isAllAssetTypesSelected = true;
    private bool _isAllAssetSourcesSelected = true;
    private bool _isUpdatingFilterSelection;

    /// <summary>
    /// Gets the application configuration.
    /// </summary>
    public GameExtractionConfig Config => _config;

    /// <summary>
    /// Gets the plugin service exposed by the asset manager.
    /// </summary>
    public PluginsService Plugins { get; }

    /// <summary>
    /// Gets the preview state bound to the preview window.
    /// </summary>
    public PreviewViewModel Preview { get; }

    /// <summary>
    /// Gets the filtered asset view bound to the asset grid.
    /// </summary>
    public ObservableCollection<AssetRowViewModel> AssetsView => _assetsView;

    /// <summary>
    /// Gets the mounted source rows.
    /// </summary>
    public ObservableCollection<AssetSourceViewModel> LoadedSources { get; } = [];

    /// <summary>
    /// Gets all loaded asset rows before search filtering.
    /// </summary>
    public IReadOnlyList<AssetRowViewModel> AllAssets => _allAssets;

    /// <summary>
    /// Gets currently selected asset rows.
    /// </summary>
    public ObservableCollection<AssetRowViewModel> SelectedAssets { get; } = [];

    /// <summary>
    /// Gets a value indicating whether any assets are selected.
    /// </summary>
    public bool HasSelectedAssets => SelectedAssets.Count > 0;

    /// <summary>
    /// Gets a summary of the current asset selection.
    /// </summary>
    public string SelectedAssetsSummary => $"{SelectedAssets.Count:N0} selected";

    /// <summary>
    /// Gets or sets the selected asset row used by the preview command.
    /// </summary>
    [ObservableProperty]
    public partial AssetRowViewModel? SelectedAsset { get; set; }

    /// <summary>
    /// Gets or sets the asset search text.
    /// </summary>
    [ObservableProperty]
    public partial string SearchText { get; set; } = string.Empty;

    /// <summary>
    /// Gets the available asset sort fields.
    /// </summary>
    public IReadOnlyList<string> SortOptions { get; } = ["Name", "Type", "Information"];

    /// <summary>
    /// Gets the asset types available for filtering.
    /// </summary>
    public ObservableCollection<AssetFilterOptionViewModel> AssetTypeFilterOptions { get; } = [];

    /// <summary>
    /// Gets the sources available for filtering.
    /// </summary>
    public ObservableCollection<AssetFilterOptionViewModel> AssetSourceFilterOptions { get; } = [];

    /// <summary>
    /// Gets or sets whether all asset types are included in the filter.
    /// </summary>
    public bool IsAllAssetTypesSelected
    {
        get => _isAllAssetTypesSelected;
        set => SetAllFilterOptionsSelected(value, isTypeFilter: true);
    }

    /// <summary>
    /// Gets or sets whether all sources are included in the filter.
    /// </summary>
    public bool IsAllAssetSourcesSelected
    {
        get => _isAllAssetSourcesSelected;
        set => SetAllFilterOptionsSelected(value, isTypeFilter: false);
    }

    /// <summary>
    /// Gets the text shown by the asset type filter button.
    /// </summary>
    public string AssetTypeFilterSummary => GetFilterSummary("All types", "type", AssetTypeFilterOptions, _isAllAssetTypesSelected);

    /// <summary>
    /// Gets the text shown by the source filter button.
    /// </summary>
    public string AssetSourceFilterSummary => GetFilterSummary("All sources", "source", AssetSourceFilterOptions, _isAllAssetSourcesSelected);

    /// <summary>
    /// Gets or sets the asset sort field.
    /// </summary>
    [ObservableProperty]
    public partial string SortBy { get; set; } = "Name";

    /// <summary>
    /// Gets or sets the total loaded asset count.
    /// </summary>
    [ObservableProperty]
    public partial int TotalCount { get; set; }

    /// <summary>
    /// Gets a value indicating whether the flat asset list has no loaded assets.
    /// </summary>
    public bool IsAssetListEmpty => TotalCount == 0;

    /// <summary>
    /// Gets or sets the displayed asset count after filtering.
    /// </summary>
    [ObservableProperty]
    public partial int FilteredCount { get; set; }

    /// <summary>
    /// Gets a value indicating whether the current search has no matches.
    /// </summary>
    public bool NoMatchingAssets => TotalCount > 0 && FilteredCount == 0;

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
    /// Gets the sidebar title.
    /// </summary>
    public string SidebarTitle { get; private set; } = string.Empty;

    /// <summary>
    /// Gets the sidebar description.
    /// </summary>
    public string SidebarDescription { get; private set; } = string.Empty;

    /// <summary>
    /// Gets the loaded sidebar icon.
    /// </summary>
    public string? SidebarIconPath { get; private set; }

    /// <summary>
    /// Gets a value indicating whether an icon is available to display.
    /// </summary>
    public bool HasSidebarIcon => SidebarIconPath is not null;

    /// <summary>
    /// Gets a value indicating whether file sources can be loaded.
    /// </summary>
    public bool CanLoadFiles { get; private set; }

    /// <summary>
    /// Gets a value indicating whether directory sources can be loaded.
    /// </summary>
    public bool CanLoadDirectories { get; private set; }

    /// <summary>
    /// Gets a value indicating whether process-backed sources can be loaded.
    /// </summary>
    public bool CanLoadProcess { get; private set; }

    /// <summary>
    /// Gets a value indicating whether any source loader is available.
    /// </summary>
    public bool CanLoadAnySource => CanLoadFiles || CanLoadDirectories || CanLoadProcess;

    /// <summary>
    /// Gets or sets the loaded source count.
    /// </summary>
    [ObservableProperty]
    public partial int SourceCount { get; set; }

    /// <summary>
    /// Gets the manage sources button text.
    /// </summary>
    public string SourceCountDisplay => SourceCount > 0 ? $"Manage Sources ({SourceCount})" : "Manage Sources";

    /// <summary>
    /// Gets or sets the current status text.
    /// </summary>
    [ObservableProperty]
    public partial string StatusText { get; set; } = "Ready";

    /// <summary>
    /// Raised when the settings window should be opened.
    /// </summary>
    public event Action? SettingsRequested;

    /// <summary>
    /// Raised when the about window should be opened.
    /// </summary>
    public event Action? AboutRequested;

    /// <summary>
    /// Raised when the preview window should be opened.
    /// </summary>
    public event Action? PreviewRequested;

    /// <summary>
    /// Raised when the source manager window should be opened.
    /// </summary>
    public event Action? SourceManagerRequested;

    /// <summary>
    /// Raised after the filtered asset view is rebuilt, with the indexes of the selected assets that are visible in it in selection order, so the view can restore its selection.
    /// </summary>
    public event Action<IReadOnlyList<int>>? AssetSelectionRestoreRequested;

    /// <summary>
    /// Raised when file paths are needed from the view.
    /// </summary>
    public event Func<Task<IReadOnlyList<string>>>? FileDialogRequested;

    /// <summary>
    /// Raised when a directory path is needed from the view.
    /// </summary>
    public event Func<Task<string?>>? FolderDialogRequested;

    /// <summary>
    /// Raised when the view should ask the user to select a process.
    /// </summary>
    public event Func<IReadOnlyList<ProcessCandidateViewModel>, Task<ProcessSelectionResult?>>? ProcessSelectionRequested;

    /// <summary>
    /// Initializes a new instance of the <see cref="MainWindowViewModel"/> class.
    /// </summary>
    /// <param name="config">The application configuration.</param>
    public MainWindowViewModel(GameExtractionConfig config)
    {
        _uiSynchronizationContext = SynchronizationContext.Current;
        _searchFilterTimer = new Timer(OnSearchFilterTimerElapsed, null, Timeout.Infinite, Timeout.Infinite);
        _previewSelectionTimer = new Timer(OnPreviewSelectionTimerElapsed, null, Timeout.Infinite, Timeout.Infinite);
        _config = config;
        _assetManager = config.AssetManagerFactory();
        Preview = new PreviewViewModel(_assetManager, CreateConfiguration, config.Previewers, config.PreviewSettings);
        _assetManager.OperationFailed += OnOperationFailed;
        _assetManager.AssetExportCompleted += OnAssetExportCompleted;
        _assetManager.AssetExportFailed += OnAssetExportFailed;

        if (!_assetManager.TryGetService(out PluginsService? plugins))
        {
            string pluginsDirectory = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), config.AppName, "plugins");
            plugins = new PluginsService(pluginsDirectory);
            PythonPluginHost? pythonHost = null;
            try
            {
                pythonHost = new PythonPluginHost();
                plugins.Manager.RegisterHost(pythonHost);
            }
            catch
            {
                pythonHost?.Dispose();
            }
            _assetManager.RegisterService(plugins);
        }

        Plugins = plugins;

        // Expose the shared SceneTranslatorManager (if any) to plugins under the well-known
        // "scene-translators" key so scripts can register custom translators on load.
        if (!plugins.Manager.Services.ContainsKey("scene-translators"))
        {
            if (!_assetManager.TryGetService(out SceneTranslatorManager? translators))
            {
                translators = new SceneTranslatorManager();
                _assetManager.RegisterService(translators);
            }
            plugins.Manager.Services["scene-translators"] = translators;
        }

        try
        {
            plugins.LoadAutoLoaded();
        }
        catch
        {
            // Auto-load failures are recorded per descriptor; never crash startup.
        }

        InitializeShellState(config);

        LoadedSources.CollectionChanged += (_, _) =>
        {
            SourceCount = LoadedSources.Count;
        };
    }

    /// <summary>
    /// Loads assets from a file path.
    /// </summary>
    /// <param name="filePath">The file path to mount.</param>
    /// <returns>A task that completes when loading finishes.</returns>
    public async Task LoadSourceFromFileAsync(string filePath)
    {
        string fullPath = Path.GetFullPath(filePath);
        if (HasMountedLocation(AssetSourceKind.File, fullPath))
        {
            StatusText = $"Already loaded: {Path.GetFileName(fullPath)}";
            return;
        }

        await MountSourceAsync($"Loading {Path.GetFileName(fullPath)}...", (progress, cancellationToken) => _assetManager.MountFileAsync(fullPath, _config.SourceOptions, progress, cancellationToken)).ConfigureAwait(false);
    }

    /// <summary>
    /// Loads assets from a directory path.
    /// </summary>
    /// <param name="directoryPath">The directory path to mount.</param>
    /// <returns>A task that completes when loading finishes.</returns>
    public async Task LoadSourceFromDirectoryAsync(string directoryPath)
    {
        string fullPath = Path.GetFullPath(directoryPath);
        if (HasMountedLocation(AssetSourceKind.Directory, fullPath))
        {
            StatusText = $"Already loaded: {Path.GetFileName(fullPath)}";
            return;
        }

        await MountSourceAsync($"Loading {Path.GetFileName(fullPath)}...", (progress, cancellationToken) => _assetManager.MountDirectoryAsync(fullPath, _config.SourceOptions, progress, cancellationToken)).ConfigureAwait(false);
    }

    /// <summary>
    /// Unloads a mounted source.
    /// </summary>
    /// <param name="source">The source row to unload.</param>
    /// <returns>A task that completes when the source is unloaded.</returns>
    public async Task UnloadSourceAsync(AssetSourceViewModel source)
    {
        if (!LoadedSources.Contains(source))
        {
            return;
        }

        IsLoading = true;
        CancellationTokenSource cancellationSource = new();
        _currentCts = cancellationSource;
        ProgressDialogViewModel progressVm = CreateProgressDialog($"Unloading {source.DisplayName}...", cancellationSource);
        ProgressDialog = progressVm;
        ShowProgressDialog = true;

        try
        {
            progressVm.StatusText = $"Releasing {source.DisplayName}...";
            await Task.Run(() => _assetManager.UnloadAsync(source.Source, cancellationSource.Token), cancellationSource.Token).ConfigureAwait(true);
            await InvokeOnUiThreadAsync(() =>
            {
                RemoveSource(source);
                LoadedSources.Remove(source);
                StatusText = $"Unloaded {source.DisplayName}";
            });
        }
        catch (OperationCanceledException)
        {
            StatusText = "Unload cancelled";
        }
        catch (Exception exception)
        {
            StatusText = $"Unload error: {exception.Message}";
        }
        finally
        {
            ShowProgressDialog = false;
            ProgressDialog = null;
            IsLoading = false;
            if (ReferenceEquals(_currentCts, cancellationSource))
                _currentCts = null;
            cancellationSource.Dispose();
        }
    }

    /// <summary>
    /// Adds assets from a mounted source.
    /// </summary>
    /// <param name="source">The source row to add.</param>
    public void AddSource(AssetSourceViewModel source)
    {
        AddSource(source, [.. source.Source.Assets.Select(asset => new AssetRowViewModel(asset, source))]);
    }

    /// <summary>
    /// Removes assets from a mounted source.
    /// </summary>
    /// <param name="source">The source row to remove.</param>
    public void RemoveSource(AssetSourceViewModel source)
    {
        _allAssets.RemoveAll(row => ReferenceEquals(row.Source, source));
        for (int index = SelectedAssets.Count - 1; index >= 0; index--)
        {
            if (ReferenceEquals(SelectedAssets[index].Source, source))
            {
                SelectedAssets.RemoveAt(index);
            }
        }

        SelectedAsset = SelectedAssets.LastOrDefault();
        NotifySelectionChanged();
        TotalCount = _allAssets.Count;
        RefreshAssetFilterOptions();
        ApplyFilter();
        RefreshPreview([.. SelectedAssets]);
    }

    /// <summary>
    /// Updates the selected asset rows from the asset grid.
    /// </summary>
    /// <param name="selectedAssets">The selected asset rows.</param>
    public void SetSelectedAssets(IEnumerable<AssetRowViewModel> selectedAssets)
    {
        if (_isReplacingAssetsView)
        {
            return;
        }

        AssetRowViewModel[] selection = [.. selectedAssets];
        if (selection.SequenceEqual(SelectedAssets) || (selection.Length == 0 && SelectedAssets.Any(asset => !_assetsView.Contains(asset))))
        {
            return;
        }

        SelectedAssets.Clear();
        foreach (AssetRowViewModel asset in selection)
        {
            SelectedAssets.Add(asset);
        }

        SelectedAsset = SelectedAssets.LastOrDefault();
        NotifySelectionChanged();
        RefreshPreview([.. SelectedAssets]);
    }

    /// <summary>
    /// Clears all loaded assets and selection state.
    /// </summary>
    public void ClearAssets()
    {
        _allAssets.Clear();
        SelectedAssets.Clear();
        SelectedAsset = null;
        NotifySelectionChanged();
        TotalCount = 0;
        RefreshAssetFilterOptions();
        ApplyFilter();
        RefreshPreview([]);
    }

    /// <summary>
    /// Opens the preview window for the specified asset row.
    /// </summary>
    /// <param name="asset">The asset row to preview.</param>
    public void OpenPreview(AssetRowViewModel asset)
    {
        SelectedAsset = asset;
        RequestPreview([asset]);
    }

    /// <summary>
    /// Updates whether the preview window is open.
    /// </summary>
    /// <param name="value">Whether the preview window is open.</param>
    public void SetPreviewWindowOpen(bool value)
    {
        if (_isPreviewWindowOpen == value)
        {
            return;
        }

        _isPreviewWindowOpen = value;
        AssetRowViewModel[] selection = _pendingPreviewSelection ?? [.. SelectedAssets];
        _pendingPreviewSelection = null;

        if (value)
        {
            CancelScheduledPreviewRefresh();
            _ = Preview.UpdateSelectionAsync(selection);
        }
        else
        {
            CancelScheduledPreviewRefresh();
            Preview.Clear();
        }
    }

    /// <inheritdoc/>
    public void Dispose()
    {
        _isDisposed = true;
        _searchFilterTimer.Dispose();
        CancelScheduledPreviewRefresh();
        _previewSelectionTimer.Dispose();
        lock (_filterCancellationLock)
        {
            _filterCancellationSource?.Cancel();
            _filterCancellationSource = null;
        }

        _filterGeneration++;
        _filterOptionsGeneration++;
        _assetManager.OperationFailed -= OnOperationFailed;
        _assetManager.AssetExportCompleted -= OnAssetExportCompleted;
        _assetManager.AssetExportFailed -= OnAssetExportFailed;
        Plugins.Dispose();
        _currentCts?.Cancel();
        _currentCts = null;
        Preview.Clear();
    }

    partial void OnSearchTextChanged(string value)
    {
        _assetNameFilter = value.Trim();
        _searchFilterTimer.Change(TimeSpan.FromMilliseconds(180), Timeout.InfiniteTimeSpan);
    }

    partial void OnTotalCountChanged(int value)
    {
        OnPropertyChanged(nameof(IsAssetListEmpty));
        OnPropertyChanged(nameof(NoMatchingAssets));
    }

    partial void OnFilteredCountChanged(int value) => OnPropertyChanged(nameof(NoMatchingAssets));

    partial void OnSortByChanged(string value) => ApplyFilter();

    [RelayCommand]
    private Task LoadSource()
    {
        return RequestLoadFilesAsync();
    }

    [RelayCommand]
    private Task LoadDirectory()
    {
        return RequestLoadDirectoryAsync();
    }

    [RelayCommand]
    private async Task LoadProcess()
    {
        IReadOnlyList<ProcessCandidateViewModel> processes = await DiscoverProcessCandidatesAsync(applyConfiguredFilter: true).ConfigureAwait(true);

        if (processes.Count == 0)
        {
            StatusText = "No compatible processes were found.";
            return;
        }

        if (processes is [ProcessCandidateViewModel process])
        {
            await MountProcessAsync(process).ConfigureAwait(true);
            return;
        }

        ProcessSelectionResult? selection = await RequestProcessSelectionAsync(processes).ConfigureAwait(true);
        if (selection is not null)
        {
            await MountProcessAsync(selection.Process).ConfigureAwait(true);
        }
    }

    [RelayCommand]
    private Task BrowseProcess()
    {
        return RequestProcessSelectionAsync();
    }

    [RelayCommand]
    private void ManageSources()
    {
        OpenSourceManager();
    }

    [RelayCommand]
    private void PreviewSelected()
    {
        RequestPreview([.. SelectedAssets]);
    }

    private void RequestPreview(AssetRowViewModel[] selection)
    {
        if (_isPreviewWindowOpen)
        {
            CancelScheduledPreviewRefresh();
            _ = Preview.UpdateSelectionAsync(selection);
        }
        else
        {
            _pendingPreviewSelection = selection;
        }

        PreviewRequested?.Invoke();
    }

    private void RefreshPreview(AssetRowViewModel[] selection)
    {
        if (_isPreviewWindowOpen)
        {
            _debouncedPreviewSelection = selection;
            Interlocked.Increment(ref _previewDebounceGeneration);
            Preview.Cancel();
            _previewSelectionTimer.Change(TimeSpan.FromMilliseconds(120), Timeout.InfiniteTimeSpan);
        }
    }

    private void OnPreviewSelectionTimerElapsed(object? state)
    {
        if (_isDisposed)
        {
            return;
        }

        int generation = Volatile.Read(ref _previewDebounceGeneration);
        Dispatcher.UIThread.Post(() =>
        {
            if (_isDisposed || generation != Volatile.Read(ref _previewDebounceGeneration) || !_isPreviewWindowOpen)
            {
                return;
            }

            AssetRowViewModel[]? selection = _debouncedPreviewSelection;
            _debouncedPreviewSelection = null;
            if (selection is not null)
            {
                _ = Preview.UpdateSelectionAsync(selection);
            }
        });
    }

    private void CancelScheduledPreviewRefresh()
    {
        Interlocked.Increment(ref _previewDebounceGeneration);
        _previewSelectionTimer.Change(Timeout.InfiniteTimeSpan, Timeout.InfiniteTimeSpan);
        _debouncedPreviewSelection = null;
    }

    [RelayCommand]
    private void OpenSettings()
    {
        SettingsRequested?.Invoke();
    }

    [RelayCommand]
    private void OpenAbout()
    {
        AboutRequested?.Invoke();
    }

    partial void OnSourceCountChanged(int value)
    {
        OnPropertyChanged(nameof(SourceCountDisplay));
    }

    private async Task RequestLoadFilesAsync()
    {
        if (FileDialogRequested is null)
        {
            return;
        }

        IReadOnlyList<string> filePaths = await FileDialogRequested.Invoke().ConfigureAwait(true);
        string[] selectedPaths = [.. filePaths.Where(filePath => !string.IsNullOrWhiteSpace(filePath))];
        if (selectedPaths.Length == 0)
        {
            return;
        }

        await MountFilesAsync(selectedPaths).ConfigureAwait(true);
    }

    private async Task MountFilesAsync(IReadOnlyList<string> filePaths)
    {
        IsLoading = true;
        CancellationTokenSource cancellationSource = new();
        _currentCts = cancellationSource;
        ProgressDialogViewModel progressVm = CreateProgressDialog($"Loading {filePaths.Count:N0} sources...", cancellationSource);
        progressVm.Total = filePaths.Count;
        progressVm.IsIndeterminate = false;
        ProgressDialog = progressVm;
        ShowProgressDialog = true;
        StatusText = progressVm.Title;

        try
        {
            IProgress<string> progress = new CallbackProgress<string>(message => QueueProgressUpdate(progressVm, message));

            foreach (string filePath in filePaths)
            {
                cancellationSource.Token.ThrowIfCancellationRequested();
                string fullPath = Path.GetFullPath(filePath);
                if (HasMountedLocation(AssetSourceKind.File, fullPath))
                {
                    StatusText = $"Already loaded: {Path.GetFileName(fullPath)}";
                }
                else
                {
                    progressVm.StatusText = $"Loading {Path.GetFileName(fullPath)}...";
                    try
                    {
                        IAssetSource source = await Task.Run(() => _assetManager.MountFileAsync(fullPath, _config.SourceOptions, progress, cancellationSource.Token), cancellationSource.Token).ConfigureAwait(true);
                        await AddMountedSourceAsync(source).ConfigureAwait(true);
                        StatusText = "Ready";
                    }
                    catch (OperationCanceledException)
                    {
                        throw;
                    }
                    catch (Exception exception)
                    {
                        StatusText = $"Load error: {exception.Message}";
                    }
                }

                progressVm.Current++;
                progressVm.ProgressValue = progressVm.Current / (double)progressVm.Total * 100;
            }
        }
        catch (OperationCanceledException)
        {
            StatusText = "Loading cancelled";
        }
        finally
        {
            ShowProgressDialog = false;
            ProgressDialog = null;
            IsLoading = false;
            if (ReferenceEquals(_currentCts, cancellationSource))
                _currentCts = null;
            cancellationSource.Dispose();
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

    private async Task RequestProcessSelectionAsync()
    {
        IReadOnlyList<ProcessCandidateViewModel> processes = await DiscoverProcessCandidatesAsync(applyConfiguredFilter: false).ConfigureAwait(true);

        if (processes.Count == 0)
        {
            StatusText = "No compatible processes were found.";
            return;
        }

        ProcessSelectionResult? selection = await RequestProcessSelectionAsync(processes).ConfigureAwait(true);
        if (selection is not null)
        {
            await MountProcessAsync(selection.Process).ConfigureAwait(true);
        }
    }

    private async Task<ProcessSelectionResult?> RequestProcessSelectionAsync(IReadOnlyList<ProcessCandidateViewModel> processes)
    {
        if (ProcessSelectionRequested is null)
        {
            return processes
                .Select(process => new ProcessSelectionResult(process))
                .FirstOrDefault();
        }

        return await ProcessSelectionRequested.Invoke(processes).ConfigureAwait(true);
    }

    private async Task MountProcessAsync(ProcessCandidateViewModel process)
    {
        string location = $"PID {process.ProcessId}";
        if (HasMountedLocation(AssetSourceKind.Process, location))
        {
            StatusText = $"Already loaded: {process.DisplayName}";
            return;
        }

        await MountSourceAsync($"Loading {process.DisplayName}...", (progress, cancellationToken) => _assetManager.MountProcessAsync(process.ProcessId, _config.SourceOptions, progress, cancellationToken)).ConfigureAwait(true);
    }

    private async Task<IReadOnlyList<ProcessCandidateViewModel>> DiscoverProcessCandidatesAsync(bool applyConfiguredFilter)
    {
        if (IsLoading)
        {
            return [];
        }

        IsLoading = true;
        CancellationTokenSource cancellationSource = new();
        _currentCts = cancellationSource;
        ProgressDialogViewModel progressVm = CreateProgressDialog("Scanning processes...", cancellationSource);
        ProgressDialog = progressVm;
        ShowProgressDialog = true;
        StatusText = "Scanning processes";

        try
        {
            IProgress<string> progress = new CallbackProgress<string>(message => QueueProgressUpdate(progressVm, message));

            return await Task.Run<IReadOnlyList<ProcessCandidateViewModel>>(() =>
            {
                if (_assetManager.SourceReaders.Count == 0)
                    return [];

                List<ProcessCandidateViewModel> candidates = [];
                foreach (Process process in Process.GetProcesses().OrderBy(process => GetProcessName(process), StringComparer.OrdinalIgnoreCase))
                {
                    using var candidate = process;
                    cancellationSource.Token.ThrowIfCancellationRequested();

                    try
                    {
                        int processId = process.Id;
                        string processName = process.ProcessName;

                        if (processId <= 0)
                            continue;

                        string windowTitle = GetProcessWindowTitle(process);
                        progress.Report($"Checking {processName} ({processId})");
                        AssetSourceRequest request = AssetSourceRequest.ForProcess(processId, _config.SourceOptions);
                        List<ProcessReaderViewModel> matchingReaders = [.. FindMatchingProcessReaders(request)];

                        if (matchingReaders.Count > 0)
                            candidates.Add(new ProcessCandidateViewModel(request, processId, processName, windowTitle, matchingReaders));
                    }
                    catch
                    {
                    }
                }

                return candidates;
            }, cancellationSource.Token).ConfigureAwait(true);
        }
        catch (OperationCanceledException)
        {
            StatusText = "Process scan cancelled";
            return [];
        }
        catch (Exception exception)
        {
            StatusText = $"Process scan error: {exception.Message}";
            return [];
        }
        finally
        {
            ShowProgressDialog = false;
            ProgressDialog = null;
            IsLoading = false;
            if (ReferenceEquals(_currentCts, cancellationSource))
                _currentCts = null;
            cancellationSource.Dispose();
        }
    }

    private IEnumerable<ProcessReaderViewModel> FindMatchingProcessReaders(AssetSourceRequest request)
    {
        foreach (IAssetSourceReader reader in _assetManager.SourceReaders)
        {
            bool canOpen;
            try
            {
                canOpen = reader.CanOpen(request);
            }
            catch
            {
                canOpen = false;
            }

            if (canOpen)
            {
                yield return new ProcessReaderViewModel(reader);
            }
        }
    }

    private static string GetProcessName(Process process)
    {
        try
        {
            return process.ProcessName;
        }
        catch
        {
            return string.Empty;
        }
    }

    private static string GetProcessWindowTitle(Process process)
    {
        try
        {
            return process.MainWindowTitle;
        }
        catch
        {
            return string.Empty;
        }
    }

    private async Task MountSourceAsync(string title, Func<IProgress<string>, CancellationToken, Task<IAssetSource>> mountSourceAsync)
    {
        IsLoading = true;
        CancellationTokenSource cancellationSource = new();
        _currentCts = cancellationSource;
        ProgressDialogViewModel progressVm = CreateProgressDialog(title, cancellationSource);
        ProgressDialog = progressVm;
        ShowProgressDialog = true;
        StatusText = title.TrimEnd('.');

        try
        {
            IProgress<string> progress = new CallbackProgress<string>(message => QueueProgressUpdate(progressVm, message));

            IAssetSource source = await Task.Run(() => mountSourceAsync(progress, cancellationSource.Token), cancellationSource.Token).ConfigureAwait(true);

            await AddMountedSourceAsync(source).ConfigureAwait(true);
            StatusText = "Ready";
        }
        catch (OperationCanceledException)
        {
            StatusText = "Loading cancelled";
        }
        catch (Exception exception)
        {
            StatusText = $"Load error: {exception.Message}";
        }
        finally
        {
            ShowProgressDialog = false;
            ProgressDialog = null;
            IsLoading = false;
            if (ReferenceEquals(_currentCts, cancellationSource))
                _currentCts = null;
            cancellationSource.Dispose();
        }
    }

    private async Task AddMountedSourceAsync(IAssetSource source)
    {
        _assetManager.TryGetSourceRequest(source, out AssetSourceRequest? request);
        AssetSourceViewModel sourceRow = new(source, request);
        LoadedSources.Add(sourceRow);
        AssetRowViewModel[] rows = await Task.Run(() => sourceRow.Source.Assets.Select(asset => new AssetRowViewModel(asset, sourceRow)).ToArray()).ConfigureAwait(true);
        AddSource(sourceRow, rows);
    }

    private void AddSource(AssetSourceViewModel source, IEnumerable<AssetRowViewModel> rows)
    {
        _allAssets.AddRange(rows);
        TotalCount = _allAssets.Count;
        RefreshAssetFilterOptions();
        ApplyFilter();
    }

    private void InitializeShellState(GameExtractionConfig config)
    {
        SidebarTitle = config.SidebarTitle;
        SidebarDescription = config.Description;
        CanLoadFiles = config.SupportsFileSources;
        CanLoadDirectories = config.SupportsDirectorySources;
        CanLoadProcess = config.SupportsProcessSources;
        SidebarIconPath = ResolveIconPath(config.SidebarIconPath ?? config.IconPath);
    }

    [RelayCommand]
    private async Task ExportAll()
    {
        await ExportAssetsAsync(AssetsView.ToArray()).ConfigureAwait(false);
    }

    [RelayCommand]
    private async Task ExportSelected()
    {
        await ExportAssetsAsync(SelectedAssets).ConfigureAwait(false);
    }

    /// <summary>
    /// Exports the specified asset row.
    /// </summary>
    /// <param name="asset">The asset row to export.</param>
    /// <returns>A task that completes when the export finishes.</returns>
    public Task ExportAssetAsync(AssetRowViewModel asset)
    {
        ArgumentNullException.ThrowIfNull(asset);
        return ExportAssetsAsync([asset]);
    }

    private GameExtractionConfiguration CreateConfiguration()
    {
        GameExtractionConfiguration configuration = new();
        configuration.ApplySettings(_config.Settings, _config.SettingDefinitions);
        return configuration;
    }

    private async Task ExportAssetsAsync(IEnumerable<AssetRowViewModel> rows)
    {
        List<AssetRowViewModel> rowList = [.. rows];
        if (rowList.Count == 0)
        {
            return;
        }

        IsLoading = true;
        CancellationTokenSource cancellationSource = new();
        _currentCts = cancellationSource;
        ProgressDialogViewModel progressVm = CreateProgressDialog($"Exporting {rowList.Count:N0} assets...", cancellationSource);
        progressVm.Total = rowList.Count;
        progressVm.IsIndeterminate = false;
        ProgressDialog = progressVm;
        ShowProgressDialog = true;
        StatusText = $"Exporting {rowList.Count:N0} assets...";
        Interlocked.Exchange(ref _exportFailureCount, 0);
        bool completed = false;

        try
        {
            GameExtractionConfiguration configuration = CreateConfiguration();
            IProgress<string> progress = new CallbackProgress<string>(message => QueueProgressUpdate(progressVm, message));
            await Task.Run(() => _assetManager.ExportAsync([.. rowList.Select(row => row.Asset)], configuration, progress, cancellationSource.Token), cancellationSource.Token).ConfigureAwait(true);

            int failureCount = Volatile.Read(ref _exportFailureCount);
            string summary = failureCount == 0
                ? $"Exported {rowList.Count:N0} assets"
                : $"Completed {rowList.Count - failureCount:N0} of {rowList.Count:N0} assets; {failureCount:N0} failed";
            progressVm.StatusText = summary;
            progressVm.Current = rowList.Count;
            progressVm.ProgressValue = 100;
            progressVm.IsIndeterminate = false;
            progressVm.IsCompleted = true;
            progressVm.OpenExportFolderCommand = new RelayCommand(() => OpenExportFolder(progressVm, configuration.GetOption("OutputDirectory", GameExtractionSettings.GetDefaultOutputDirectory())));
            StatusText = summary;
            completed = true;
        }
        catch (OperationCanceledException)
        {
            StatusText = "Export cancelled";
        }
        catch (Exception exception)
        {
            StatusText = $"Export error: {exception.Message}";
        }
        finally
        {
            if (!completed)
            {
                ShowProgressDialog = false;
                ProgressDialog = null;
            }
            IsLoading = false;
            if (ReferenceEquals(_currentCts, cancellationSource))
                _currentCts = null;
            cancellationSource.Dispose();
        }
    }

    [RelayCommand]
    private async Task ClearAll()
    {
        AssetSourceViewModel[] sources = [.. LoadedSources];
        if (sources.Length == 0)
        {
            ClearAssets();
            StatusText = "Cleared all sources";
            return;
        }

        IsLoading = true;
        CancellationTokenSource cancellationSource = new();
        _currentCts = cancellationSource;
        ProgressDialogViewModel progressVm = CreateProgressDialog($"Unloading {sources.Length:N0} sources...", cancellationSource);
        progressVm.Total = sources.Length;
        progressVm.IsIndeterminate = false;
        ProgressDialog = progressVm;
        ShowProgressDialog = true;
        StatusText = progressVm.Title;

        try
        {
            (AssetSourceViewModel Source, bool Unloaded, Exception? Error)[] results = await Task.Run(async () =>
            {
                List<(AssetSourceViewModel Source, bool Unloaded, Exception? Error)> unloadResults = [];
                foreach (AssetSourceViewModel source in sources)
                {
                    if (cancellationSource.IsCancellationRequested)
                    {
                        break;
                    }

                    QueueProgressUpdate(progressVm, $"Unloading {source.DisplayName}...");
                    try
                    {
                        await _assetManager.UnloadAsync(source.Source, cancellationSource.Token).ConfigureAwait(false);
                        unloadResults.Add((source, true, null));
                        QueueProgressUpdate(progressVm, $"Unloaded {source.DisplayName}", completedOperation: true);
                    }
                    catch (OperationCanceledException) when (cancellationSource.IsCancellationRequested)
                    {
                        break;
                    }
                    catch (Exception exception)
                    {
                        unloadResults.Add((source, false, exception));
                        QueueProgressUpdate(progressVm, $"Failed to unload {source.DisplayName}", completedOperation: true);
                    }
                }

                return unloadResults.ToArray();
            }).ConfigureAwait(true);

            HashSet<AssetSourceViewModel> unloadedSources = new(results.Where(result => result.Unloaded).Select(result => result.Source), ReferenceEqualityComparer.Instance);
            foreach (AssetSourceViewModel source in unloadedSources)
            {
                LoadedSources.Remove(source);
            }

            _allAssets.RemoveAll(row => unloadedSources.Contains(row.Source));
            for (int index = SelectedAssets.Count - 1; index >= 0; index--)
            {
                if (unloadedSources.Contains(SelectedAssets[index].Source))
                {
                    SelectedAssets.RemoveAt(index);
                }
            }

            SelectedAsset = SelectedAssets.LastOrDefault();
            NotifySelectionChanged();
            TotalCount = _allAssets.Count;
            RefreshAssetFilterOptions();
            ApplyFilter();
            RefreshPreview([.. SelectedAssets]);

            int failedCount = results.Count(result => result.Error is not null);
            string summary = failedCount > 0
                ? $"Unloaded {unloadedSources.Count:N0} sources; {failedCount:N0} could not be unloaded"
                : unloadedSources.Count == sources.Length ? "Cleared all sources" : $"Unloaded {unloadedSources.Count:N0} sources";
            StatusText = cancellationSource.IsCancellationRequested && unloadedSources.Count < sources.Length
                ? $"Unloading cancelled; {summary}"
                : summary;
        }
        finally
        {
            ShowProgressDialog = false;
            ProgressDialog = null;
            IsLoading = false;
            if (ReferenceEquals(_currentCts, cancellationSource))
                _currentCts = null;
            cancellationSource.Dispose();
        }
    }

    private bool HasMountedLocation(AssetSourceKind kind, string location)
    {
        return LoadedSources.Any(source => source.Request?.Kind == kind && string.Equals(source.Location, location, StringComparison.OrdinalIgnoreCase));
    }

    private void OpenSourceManager()
    {
        SourceManagerRequested?.Invoke();
    }

    private ProgressDialogViewModel CreateProgressDialog(string title, CancellationTokenSource cancellationSource)
    {
        ProgressDialogViewModel progressVm = new(title);
        progressVm.CancelCommand = new RelayCommand(() =>
        {
            progressVm.IsCancelling = true;
            progressVm.StatusText = "Cancelling...";
            progressVm.IsIndeterminate = true;
            cancellationSource.Cancel();
        });
        progressVm.CloseCommand = new RelayCommand(() =>
        {
            if (ReferenceEquals(ProgressDialog, progressVm))
            {
                ShowProgressDialog = false;
                ProgressDialog = null;
            }
        });
        return progressVm;
    }

    private static void OpenExportFolder(ProgressDialogViewModel progressVm, string outputDirectory)
    {
        try
        {
            string fullPath = Path.GetFullPath(outputDirectory);
            Directory.CreateDirectory(fullPath);
            Process.Start(new ProcessStartInfo(fullPath) { UseShellExecute = true });
        }
        catch (Exception exception)
        {
            progressVm.StatusText = $"Unable to open export folder: {exception.Message}";
        }
    }

    private void OnOperationFailed(object? sender, AssetOperationFailedEventArgs args)
    {
        PostToUiThread(() =>
        {
            StatusText = $"{args.Operation} error: {args.Exception.Message}";
        });
    }

    private void OnAssetExportCompleted(object? sender, AssetExportCompletedEventArgs args)
    {
        ProgressDialogViewModel? progressVm = ProgressDialog;
        if (progressVm is null)
        {
            return;
        }

        string status = args.Skipped ? $"Skipped {args.Asset.Name}" : $"Exported {args.Asset.Name}";
        QueueProgressUpdate(progressVm, status, completedOperation: true);
    }

    private void OnAssetExportFailed(object? sender, AssetExportFailedEventArgs args)
    {
        Interlocked.Increment(ref _exportFailureCount);
        ProgressDialogViewModel? progressVm = ProgressDialog;
        if (progressVm is null)
        {
            return;
        }

        QueueProgressUpdate(progressVm, $"Failed {args.Asset.Name}", completedOperation: true);
    }

    private void QueueProgressUpdate(ProgressDialogViewModel progressVm, string status, bool completedOperation = false)
    {
        if (!ReferenceEquals(ProgressDialog, progressVm))
        {
            return;
        }

        bool postUpdate;
        lock (_progressUpdateLock)
        {
            if (!ReferenceEquals(_queuedProgressDialog, progressVm))
            {
                _queuedProgressDialog = progressVm;
                _queuedProgressStatus = null;
                _queuedCompletedOperations = 0;
            }

            _queuedProgressStatus = status;
            if (completedOperation)
            {
                _queuedCompletedOperations++;
            }

            postUpdate = !_progressUpdatePosted;
            _progressUpdatePosted = true;
        }

        if (postUpdate)
        {
            PostToUiThread(FlushProgressUpdate);
        }
    }

    private void FlushProgressUpdate()
    {
        ProgressDialogViewModel? progressVm;
        string? status;
        int completedCount;
        lock (_progressUpdateLock)
        {
            progressVm = _queuedProgressDialog;
            status = _queuedProgressStatus;
            completedCount = _queuedCompletedOperations;
            _queuedProgressDialog = null;
            _queuedProgressStatus = null;
            _queuedCompletedOperations = 0;
            _progressUpdatePosted = false;
        }

        if (progressVm is null || !ReferenceEquals(ProgressDialog, progressVm) || progressVm.IsCompleted)
        {
            return;
        }

        if (status is not null && !progressVm.IsCancelling)
        {
            progressVm.StatusText = status;
        }

        if (completedCount > 0)
        {
            progressVm.IsIndeterminate = false;
            progressVm.Current += completedCount;
            progressVm.Total = Math.Max(progressVm.Total, progressVm.Current);
            progressVm.ProgressValue = progressVm.Total > 0 ? Math.Min(100, progressVm.Current / (double)progressVm.Total * 100) : 0;
        }
    }

    private void OnSearchFilterTimerElapsed(object? state) => PostToUiThread(ApplyFilter);

    private void PostToUiThread(Action action)
    {
        if (_uiSynchronizationContext is null || ReferenceEquals(SynchronizationContext.Current, _uiSynchronizationContext))
        {
            action();
            return;
        }

        _uiSynchronizationContext.Post(static state => ((Action)state!).Invoke(), action);
    }

    private Task InvokeOnUiThreadAsync(Action action)
    {
        if (_uiSynchronizationContext is null || ReferenceEquals(SynchronizationContext.Current, _uiSynchronizationContext))
        {
            action();
            return Task.CompletedTask;
        }

        TaskCompletionSource<bool> completion = new(TaskCreationOptions.RunContinuationsAsynchronously);
        _uiSynchronizationContext.Post(static state =>
        {
            var request = ((Action Action, TaskCompletionSource<bool> Completion))state!;
            try
            {
                request.Action();
                request.Completion.SetResult(true);
            }
            catch (Exception exception)
            {
                request.Completion.SetException(exception);
            }
        }, (action, completion));
        return completion.Task;
    }

    private void ReplaceAssetsView(AssetRowViewModel[] visibleAssets)
    {
        HashSet<AssetRowViewModel> selected = [.. SelectedAssets];
        Dictionary<AssetRowViewModel, int> visibleIndexes = [];

        for (int index = 0; index < visibleAssets.Length && visibleIndexes.Count < selected.Count; index++)
        {
            if (selected.Contains(visibleAssets[index]))
            {
                visibleIndexes[visibleAssets[index]] = index;
            }
        }

        int[] visibleSelection = [.. SelectedAssets.Where(visibleIndexes.ContainsKey).Select(asset => visibleIndexes[asset])];

        _isReplacingAssetsView = true;

        try
        {
            _assetsView.ReplaceAll(visibleAssets);
            AssetSelectionRestoreRequested?.Invoke(visibleSelection);
        }
        finally
        {
            _isReplacingAssetsView = false;
        }
    }

    private void ApplyFilter()
    {
        int generation = ++_filterGeneration;
        CancellationTokenSource cancellationSource = new();
        lock (_filterCancellationLock)
        {
            CancellationTokenSource? previousCancellationSource = _filterCancellationSource;
            _filterCancellationSource = cancellationSource;
            previousCancellationSource?.Cancel();
        }

        AssetRowViewModel[] allAssets = [.. _allAssets];
        string[] nameFilters = _assetNameFilter.Contains('*') || _assetNameFilter.Contains('?') ? _assetNameFilter.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries) : [_assetNameFilter];
        string[] selectedTypes = [.. AssetTypeFilterOptions.Where(option => option.IsSelected).Select(option => option.Name)];
        string[] selectedSources = [.. AssetSourceFilterOptions.Where(option => option.IsSelected).Select(option => option.Name)];
        bool includeAllTypes = _isAllAssetTypesSelected;
        bool includeAllSources = _isAllAssetSourcesSelected;
        string sortBy = SortBy;
        IComparer<Asset>? comparer = sortBy == "Information" ? GetAssetInformationComparer() : null;
        _ = UpdateFilteredAssetsAsync(generation, allAssets, nameFilters, selectedTypes, includeAllTypes, selectedSources, includeAllSources, sortBy, comparer, cancellationSource);
    }

    private async Task UpdateFilteredAssetsAsync(int generation, AssetRowViewModel[] allAssets, string[] nameFilters, string[] selectedTypes, bool includeAllTypes, string[] selectedSources, bool includeAllSources, string sortBy, IComparer<Asset>? comparer, CancellationTokenSource cancellationSource)
    {
        try
        {
            CancellationToken cancellationToken = cancellationSource.Token;
            AssetRowViewModel[]? visibleAssets = await Task.Run(() =>
            {
                IEnumerable<AssetRowViewModel> filteredAssets = FilterVisibleAssets(allAssets, nameFilters, selectedTypes, includeAllTypes, selectedSources, includeAllSources, cancellationToken);
                IOrderedEnumerable<AssetRowViewModel> sortedAssets = sortBy switch
                {
                    "Type" => filteredAssets.OrderBy(asset => asset.Type, StringComparer.OrdinalIgnoreCase).ThenBy(asset => asset.Name, StringComparer.OrdinalIgnoreCase),
                    "Information" => filteredAssets.OrderBy(asset => asset.Asset, comparer).ThenBy(asset => asset.Name, StringComparer.OrdinalIgnoreCase),
                    _ => filteredAssets.OrderBy(asset => asset.Name, StringComparer.OrdinalIgnoreCase),
                };
                AssetRowViewModel[] results = sortedAssets.ToArray();
                return cancellationToken.IsCancellationRequested ? null : results;
            }).ConfigureAwait(false);

            if (visibleAssets is null)
            {
                return;
            }

            PostToUiThread(() =>
            {
                if (generation != _filterGeneration)
                {
                    return;
                }

                ReplaceAssetsView(visibleAssets);
                FilteredCount = visibleAssets.Length;
            });
        }
        catch (OperationCanceledException) when (cancellationSource.IsCancellationRequested)
        {
        }
        catch (Exception exception)
        {
            PostToUiThread(() =>
            {
                if (generation == _filterGeneration)
                {
                    StatusText = $"Asset filter error: {exception.Message}";
                }
            });
        }
        finally
        {
            lock (_filterCancellationLock)
            {
                if (ReferenceEquals(_filterCancellationSource, cancellationSource))
                {
                    _filterCancellationSource = null;
                }

                cancellationSource.Dispose();
            }
        }
    }

    private static IEnumerable<AssetRowViewModel> FilterVisibleAssets(AssetRowViewModel[] assets, string[] nameFilters, string[] selectedTypes, bool includeAllTypes, string[] selectedSources, bool includeAllSources, CancellationToken cancellationToken)
    {
        foreach (AssetRowViewModel asset in assets)
        {
            if (cancellationToken.IsCancellationRequested)
            {
                yield break;
            }

            if (IsVisibleAssetRow(asset, nameFilters, selectedTypes, includeAllTypes, selectedSources, includeAllSources))
            {
                yield return asset;
            }
        }
    }

    private static bool IsVisibleAssetRow(AssetRowViewModel asset, string[] nameFilters, string[] selectedTypes, bool includeAllTypes, string[] selectedSources, bool includeAllSources)
    {
        bool matchesName = MatchesNameFilter(asset.Name, nameFilters);
        bool matchesType = includeAllTypes || selectedTypes.Contains(asset.Type, StringComparer.OrdinalIgnoreCase);
        bool matchesSource = includeAllSources || selectedSources.Contains(asset.SourceName, StringComparer.OrdinalIgnoreCase);
        return matchesName && matchesType && matchesSource;
    }

    private static bool MatchesNameFilter(string name, string[] filters)
    {
        foreach (string filter in filters)
        {
            bool matches = filter.Contains('*') || filter.Contains('?') ? FileSystemName.MatchesSimpleExpression(filter, name, ignoreCase: true) : name.Contains(filter, StringComparison.OrdinalIgnoreCase);
            if (!matches)
            {
                return false;
            }
        }

        return true;
    }

    private IComparer<Asset> GetAssetInformationComparer()
    {
        if (_assetManager.TryGetService(out IAssetComparer? assetComparer) && assetComparer is not null)
        {
            return assetComparer;
        }

        return AssetInformationComparer.Instance;
    }

    private sealed class AssetInformationComparer : IComparer<Asset>
    {
        public static AssetInformationComparer Instance { get; } = new();

        public int Compare(Asset? left, Asset? right)
        {
            int leftIsEmpty = string.IsNullOrWhiteSpace(left?.Information) ? 1 : 0;
            int rightIsEmpty = string.IsNullOrWhiteSpace(right?.Information) ? 1 : 0;
            int emptyComparison = leftIsEmpty.CompareTo(rightIsEmpty);
            return emptyComparison != 0
                ? emptyComparison
                : StringComparer.OrdinalIgnoreCase.Compare(left?.Information, right?.Information);
        }
    }

    private void NotifySelectionChanged()
    {
        OnPropertyChanged(nameof(HasSelectedAssets));
        OnPropertyChanged(nameof(SelectedAssetsSummary));
    }

    private void RefreshAssetFilterOptions()
    {
        int generation = ++_filterOptionsGeneration;
        (string Type, string Source)[] values = [.. _allAssets.Select(asset => (asset.Type, asset.SourceName))];
        _ = UpdateAssetFilterOptionsAsync(generation, values);
    }

    private async Task UpdateAssetFilterOptionsAsync(int generation, (string Type, string Source)[] values)
    {
        (string[] types, string[] sources) = await Task.Run(() => (
            values.Select(value => value.Type).Distinct(StringComparer.OrdinalIgnoreCase).OrderBy(type => type, StringComparer.OrdinalIgnoreCase).ToArray(),
            values.Select(value => value.Source).Distinct(StringComparer.OrdinalIgnoreCase).OrderBy(source => source, StringComparer.OrdinalIgnoreCase).ToArray())).ConfigureAwait(false);

        PostToUiThread(() =>
        {
            if (generation != _filterOptionsGeneration)
            {
                return;
            }

            string[] selectedTypes = [.. AssetTypeFilterOptions.Where(option => option.IsSelected).Select(option => option.Name)];
            string[] selectedSources = [.. AssetSourceFilterOptions.Where(option => option.IsSelected).Select(option => option.Name)];
            ReplaceFilterOptions(AssetTypeFilterOptions, types, selectedTypes, isTypeFilter: true);
            ReplaceFilterOptions(AssetSourceFilterOptions, sources, selectedSources, isTypeFilter: false);

            if (selectedTypes.Length > 0 && !AssetTypeFilterOptions.Any(option => option.IsSelected))
            {
                _isAllAssetTypesSelected = true;
            }

            if (selectedSources.Length > 0 && !AssetSourceFilterOptions.Any(option => option.IsSelected))
            {
                _isAllAssetSourcesSelected = true;
            }

            NotifyAssetTypeFilterChanged();
            NotifyAssetSourceFilterChanged();
            ApplyFilter();
        });
    }

    private void ReplaceFilterOptions(ObservableCollection<AssetFilterOptionViewModel> options, string[] names, string[] selectedNames, bool isTypeFilter)
    {
        HashSet<string> selected = new(selectedNames, StringComparer.OrdinalIgnoreCase);
        options.Clear();
        foreach (string name in names)
        {
            options.Add(new AssetFilterOptionViewModel(name, selected.Contains(name), () => OnFilterOptionSelectionChanged(isTypeFilter)));
        }
    }

    private void SetAllFilterOptionsSelected(bool value, bool isTypeFilter)
    {
        if (_isUpdatingFilterSelection)
        {
            return;
        }

        bool currentValue = isTypeFilter ? _isAllAssetTypesSelected : _isAllAssetSourcesSelected;
        if (currentValue == value)
        {
            return;
        }

        _isUpdatingFilterSelection = true;
        try
        {
            if (isTypeFilter)
            {
                _isAllAssetTypesSelected = value;
                if (value)
                {
                    foreach (AssetFilterOptionViewModel option in AssetTypeFilterOptions)
                    {
                        option.IsSelected = false;
                    }
                }
            }
            else
            {
                _isAllAssetSourcesSelected = value;
                if (value)
                {
                    foreach (AssetFilterOptionViewModel option in AssetSourceFilterOptions)
                    {
                        option.IsSelected = false;
                    }
                }
            }
        }
        finally
        {
            _isUpdatingFilterSelection = false;
        }

        if (isTypeFilter)
        {
            NotifyAssetTypeFilterChanged();
        }
        else
        {
            NotifyAssetSourceFilterChanged();
        }

        ApplyFilter();
    }

    private void OnFilterOptionSelectionChanged(bool isTypeFilter)
    {
        if (_isUpdatingFilterSelection)
        {
            return;
        }

        if (isTypeFilter)
        {
            _isAllAssetTypesSelected = false;
            NotifyAssetTypeFilterChanged();
        }
        else
        {
            _isAllAssetSourcesSelected = false;
            NotifyAssetSourceFilterChanged();
        }

        ApplyFilter();
    }

    private void NotifyAssetTypeFilterChanged()
    {
        OnPropertyChanged(nameof(IsAllAssetTypesSelected));
        OnPropertyChanged(nameof(AssetTypeFilterSummary));
    }

    private void NotifyAssetSourceFilterChanged()
    {
        OnPropertyChanged(nameof(IsAllAssetSourcesSelected));
        OnPropertyChanged(nameof(AssetSourceFilterSummary));
    }

    private static string GetFilterSummary(string allLabel, string itemName, IEnumerable<AssetFilterOptionViewModel> options, bool includeAll)
    {
        if (includeAll)
        {
            return allLabel;
        }

        int count = options.Count(option => option.IsSelected);
        return count switch
        {
            0 => $"No {itemName}s",
            1 => $"1 {itemName}",
            _ => $"{count} {itemName}s",
        };
    }

    private static string? ResolveIconPath(string? path)
    {
        if (string.IsNullOrWhiteSpace(path))
        {
            return null;
        }

        string fullPath = Path.IsPathRooted(path) ? path : Path.Combine(AppContext.BaseDirectory, path);
        return File.Exists(fullPath) ? fullPath : null;
    }
}
