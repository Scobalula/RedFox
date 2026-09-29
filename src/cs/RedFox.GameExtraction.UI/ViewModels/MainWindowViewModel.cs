using System.Collections.ObjectModel;
using System.Diagnostics;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using RedFox.GameExtraction;
using RedFox.GameExtraction.UI.Models;
using RedFox.Graphics3D.IO;
using RedFox.IO.FileSystem;
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
    private readonly ObservableCollection<AssetRowViewModel> _assetsView = [];
    private readonly List<AssetExplorerEntry> _explorerEntries = [];
    private readonly ObservableCollection<AssetExplorerEntry> _explorerView = [];
    private readonly SynchronizationContext? _uiSynchronizationContext;
    private readonly Timer _searchFilterTimer;
    private string _assetNameFilter = string.Empty;
    private CancellationTokenSource? _currentCts;
    private bool _isPreviewWindowOpen;
    private AssetRowViewModel[]? _pendingPreviewSelection;

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
    /// Gets the filtered Explorer-view rows for the currently selected directory.
    /// </summary>
    public ObservableCollection<AssetExplorerEntry> ExplorerView => _explorerView;

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
    /// Gets or sets the total loaded asset count.
    /// </summary>
    [ObservableProperty]
    public partial int TotalCount { get; set; }

    /// <summary>
    /// Gets or sets the displayed asset count after filtering.
    /// </summary>
    [ObservableProperty]
    public partial int FilteredCount { get; set; }

    /// <summary>
    /// Gets a value indicating whether the application exposes the directory view toggle.
    /// </summary>
    [ObservableProperty]
    public partial bool IsDirectoryViewEnabled { get; private set; }

    /// <summary>
    /// Gets or sets a value indicating whether directory view mode is currently active.
    /// </summary>
    [ObservableProperty]
    public partial bool IsDirectoryViewActive { get; set; }

    /// <summary>
    /// Gets the root of the asset directory tree shown in directory view mode.
    /// </summary>
    [ObservableProperty]
    public partial AssetDirectoryNode? RootDirectory { get; private set; }

    /// <summary>
    /// Gets the directory exposed as the items source for the directory tree.
    /// </summary>
    public IReadOnlyList<AssetDirectoryNode> RootDirectories =>
        RootDirectory is null ? Array.Empty<AssetDirectoryNode>() : (IReadOnlyList<AssetDirectoryNode>)new[] { RootDirectory };

    /// <summary>
    /// Gets or sets the directory whose direct files are shown in the asset grid.
    /// </summary>
    [ObservableProperty]
    public partial AssetDirectoryNode? SelectedDirectory { get; set; }

    /// <summary>
    /// Gets the breadcrumb-style path for the currently selected directory.
    /// </summary>
    [ObservableProperty]
    public partial string CurrentDirectoryPath { get; private set; } = string.Empty;

    /// <summary>
    /// Gets a value indicating whether the Explorer view can navigate up one level.
    /// </summary>
    public bool CanNavigateUp => IsDirectoryViewActive && SelectedDirectory?.Parent is not null;

    /// <summary>
    /// Gets the toggle button label for the directory/list view mode switch.
    /// </summary>
    public string ViewModeToggleLabel => IsDirectoryViewActive ? "List View" : "Directory View";

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
        _config = config;
        _assetManager = config.AssetManagerFactory();
        Preview = new PreviewViewModel(_assetManager);
        _assetManager.OperationFailed += OnOperationFailed;
        _assetManager.AssetExportCompleted += OnAssetExportCompleted;

        if (!_assetManager.TryGetService(out PluginsService? plugins))
        {
            string pluginsDirectory = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "RedFox", config.AppName, "plugins");
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

        try
        {
            await _assetManager.UnloadAsync(source.Source).ConfigureAwait(false);
            await InvokeOnUiThreadAsync(() =>
            {
                RemoveSource(source);
                LoadedSources.Remove(source);
                StatusText = $"Unloaded {source.DisplayName}";
            });
        }
        catch (Exception exception)
        {
            StatusText = $"Unload error: {exception.Message}";
        }
    }

    /// <summary>
    /// Adds assets from a mounted source.
    /// </summary>
    /// <param name="source">The source row to add.</param>
    public void AddSource(AssetSourceViewModel source)
    {
        foreach (Asset asset in source.Source.Assets)
        {
            _allAssets.Add(new AssetRowViewModel(asset, source));
        }

        TotalCount = _allAssets.Count;
        RebuildDirectoryTree();
        ApplyFilter();
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
        TotalCount = _allAssets.Count;
        RebuildDirectoryTree();
        ApplyFilter();
        RefreshPreview([.. SelectedAssets]);
    }

    /// <summary>
    /// Updates the selected asset rows from the asset grid.
    /// </summary>
    /// <param name="selectedAssets">The selected asset rows.</param>
    public void SetSelectedAssets(IEnumerable<AssetRowViewModel> selectedAssets)
    {
        SelectedAssets.Clear();
        foreach (AssetRowViewModel asset in selectedAssets)
        {
            SelectedAssets.Add(asset);
        }

        SelectedAsset = SelectedAssets.LastOrDefault();
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
        TotalCount = 0;
        RebuildDirectoryTree();
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
            _ = Preview.UpdateSelectionAsync(selection);
        }
        else
        {
            Preview.Clear();
        }
    }

    /// <inheritdoc/>
    public void Dispose()
    {
        _searchFilterTimer.Dispose();
        _assetManager.OperationFailed -= OnOperationFailed;
        _assetManager.AssetExportCompleted -= OnAssetExportCompleted;
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
            _ = Preview.UpdateSelectionAsync(selection);
        }
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
            IProgress<string> progress = new Progress<string>(message =>
            {
                if (!progressVm.IsCancelling)
                {
                    progressVm.StatusText = message;
                }
            });

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
            Progress<string> progress = new(message =>
            {
                if (!progressVm.IsCancelling)
                {
                    progressVm.StatusText = message;
                }
            });

            IAssetSource source = await Task.Run(() => mountSourceAsync(progress, cancellationSource.Token), cancellationSource.Token).ConfigureAwait(true);

            AddMountedSource(source);
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

    private void AddMountedSource(IAssetSource source)
    {
        _assetManager.TryGetSourceRequest(source, out AssetSourceRequest? request);
        AssetSourceViewModel sourceRow = new(source, request);
        LoadedSources.Add(sourceRow);
        AddSource(sourceRow);
    }

    private void InitializeShellState(GameExtractionConfig config)
    {
        SidebarTitle = config.SidebarTitle;
        SidebarDescription = config.Description;
        CanLoadFiles = config.SupportsFileSources;
        CanLoadDirectories = config.SupportsDirectorySources;
        CanLoadProcess = config.SupportsProcessSources;
        IsDirectoryViewEnabled = config.EnableDirectoryView;
        SidebarIconPath = ResolveIconPath(config.SidebarIconPath ?? config.IconPath);
    }

    [RelayCommand]
    private async Task ExportAll()
    {
        await ExportAssetsAsync(AllAssets).ConfigureAwait(false);
    }

    [RelayCommand]
    private async Task ExportSelected()
    {
        await ExportAssetsAsync(SelectedAssets).ConfigureAwait(false);
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

        try
        {
            ExportConfiguration configuration = _config.ExportConfigurationFactory(_config.Settings);
            Progress<string> progress = new(message =>
            {
                if (!progressVm.IsCancelling)
                {
                    progressVm.StatusText = message;
                }
            });

            List<Asset> assets = [.. rowList.Select(row => row.Asset)];
            await Task.Run(() => _assetManager.ExportAsync(assets, configuration, progress, cancellationSource.Token), cancellationSource.Token).ConfigureAwait(true);

            StatusText = $"Exported {rowList.Count:N0} assets";
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
            ShowProgressDialog = false;
            ProgressDialog = null;
            IsLoading = false;
            if (ReferenceEquals(_currentCts, cancellationSource))
                _currentCts = null;
            cancellationSource.Dispose();
        }
    }

    [RelayCommand]
    private async Task ClearAll()
    {
        foreach (AssetSourceViewModel source in LoadedSources.ToArray())
        {
            await UnloadSourceAsync(source).ConfigureAwait(true);
        }

        ClearAssets();
        LoadedSources.Clear();
        StatusText = "Cleared all sources";
    }

    private bool HasMountedLocation(AssetSourceKind kind, string location)
    {
        return LoadedSources.Any(source => source.Request?.Kind == kind && string.Equals(source.Location, location, StringComparison.OrdinalIgnoreCase));
    }

    private void OpenSourceManager()
    {
        SourceManagerRequested?.Invoke();
    }

    private static ProgressDialogViewModel CreateProgressDialog(string title, CancellationTokenSource cancellationSource)
    {
        ProgressDialogViewModel progressVm = new(title);
        progressVm.CancelCommand = new RelayCommand(() =>
        {
            progressVm.IsCancelling = true;
            progressVm.StatusText = "Cancelling...";
            progressVm.IsIndeterminate = true;
            cancellationSource.Cancel();
        });
        return progressVm;
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

        PostToUiThread(() =>
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

            progressVm.ProgressValue = progressVm.Total > 0 ? Math.Min(100, progressVm.Current / (double)progressVm.Total * 100) : 0;
        });
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

    private void ApplyFilter()
    {
        _assetsView.Clear();
        foreach (AssetRowViewModel asset in _allAssets)
        {
            if (IsVisibleAssetRow(asset))
            {
                _assetsView.Add(asset);
            }
        }

        _explorerView.Clear();
        foreach (AssetExplorerEntry entry in _explorerEntries)
        {
            if (IsVisibleExplorerEntry(entry))
            {
                _explorerView.Add(entry);
            }
        }

        FilteredCount = IsDirectoryViewActive ? _explorerView.Count : _assetsView.Count;
    }

    private bool IsVisibleAssetRow(AssetRowViewModel asset)
    {
        if (string.IsNullOrEmpty(_assetNameFilter))
        {
            return true;
        }

        return asset.Name.Contains(_assetNameFilter, StringComparison.OrdinalIgnoreCase);
    }

    private bool IsVisibleExplorerEntry(AssetExplorerEntry entry)
    {
        if (string.IsNullOrEmpty(_assetNameFilter))
        {
            return true;
        }

        return entry.Name.Contains(_assetNameFilter, StringComparison.OrdinalIgnoreCase);
    }

    private void RebuildDirectoryTree()
    {
        if (!IsDirectoryViewEnabled)
        {
            RootDirectory = null;
            SelectedDirectory = null;
            _explorerEntries.Clear();
            return;
        }

        AssetDirectoryNode? previousSelection = SelectedDirectory;
        string? previousPath = previousSelection?.FullPath;

        AssetDirectoryNode root;
        if (_assetManager.TryGetService(out AssetFileSystemService? fileSystemService))
        {
            root = AssetDirectoryTreeBuilder.BuildFromVirtualFileSystem(fileSystemService.FileSystem, _allAssets);
        }
        else
        {
            root = AssetDirectoryTreeBuilder.BuildFromAssetNames(_allAssets);
        }

        RootDirectory = root;
        OnPropertyChanged(nameof(RootDirectories));

        SelectedDirectory = TryFindByPath(root, previousPath) ?? root;
    }

    private static AssetDirectoryNode? TryFindByPath(AssetDirectoryNode root, string? fullPath)
    {
        if (string.IsNullOrEmpty(fullPath))
        {
            return root;
        }

        if (string.Equals(root.FullPath, fullPath, StringComparison.OrdinalIgnoreCase))
        {
            return root;
        }

        foreach (AssetDirectoryNode child in root.Children)
        {
            AssetDirectoryNode? match = TryFindByPath(child, fullPath);
            if (match is not null)
            {
                return match;
            }
        }

        return null;
    }

    partial void OnIsDirectoryViewActiveChanged(bool value)
    {
        if (value && RootDirectory is null)
        {
            RebuildDirectoryTree();
        }

        if (value && SelectedDirectory is null)
        {
            SelectedDirectory = RootDirectory;
        }

        UpdateSelectedDirectoryState();
        OnPropertyChanged(nameof(ViewModeToggleLabel));
        OnPropertyChanged(nameof(CanNavigateUp));
        ApplyFilter();
    }

    partial void OnSelectedDirectoryChanged(AssetDirectoryNode? value)
    {
        UpdateSelectedDirectoryState();
        if (IsDirectoryViewActive)
        {
            ApplyFilter();
        }
    }

    private void UpdateSelectedDirectoryState()
    {
        AssetDirectoryNode? node = SelectedDirectory;
        _explorerEntries.Clear();

        if (node is null)
        {
            CurrentDirectoryPath = string.Empty;
            OnPropertyChanged(nameof(CanNavigateUp));
            return;
        }

        foreach (AssetDirectoryNode child in node.Children)
        {
            _explorerEntries.Add(AssetExplorerEntry.ForFolder(child));
        }

        foreach (AssetRowViewModel row in node.Files)
        {
            _explorerEntries.Add(AssetExplorerEntry.ForFile(row));
        }

        CurrentDirectoryPath = string.IsNullOrEmpty(node.FullPath) ? "/" : "/" + node.FullPath;
        OnPropertyChanged(nameof(CanNavigateUp));
    }

    /// <summary>
    /// Sets the asset selection from the Explorer-view grid, ignoring folder entries.
    /// </summary>
    /// <param name="entries">The currently selected explorer entries.</param>
    public void SetSelectedExplorerEntries(IEnumerable<AssetExplorerEntry> entries)
    {
        SetSelectedAssets(entries.Where(entry => entry.IsFile && entry.Row is not null).Select(entry => entry.Row!));
    }

    /// <summary>
    /// Activates the supplied Explorer entry: folders navigate inward, files open preview.
    /// </summary>
    /// <param name="entry">The entry to activate.</param>
    public void ActivateExplorerEntry(AssetExplorerEntry entry)
    {
        ArgumentNullException.ThrowIfNull(entry);

        if (entry.IsFolder && entry.Directory is not null)
        {
            SelectedDirectory = entry.Directory;
            return;
        }

        if (entry.IsFile && entry.Row is not null)
        {
            OpenPreview(entry.Row);
        }
    }

    [RelayCommand]
    private void NavigateUp()
    {
        AssetDirectoryNode? parent = SelectedDirectory?.Parent;
        if (parent is null)
        {
            return;
        }

        SelectedDirectory = parent;
    }

    [RelayCommand]
    private void ToggleViewMode()
    {
        if (!IsDirectoryViewEnabled)
        {
            return;
        }

        IsDirectoryViewActive = !IsDirectoryViewActive;
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
