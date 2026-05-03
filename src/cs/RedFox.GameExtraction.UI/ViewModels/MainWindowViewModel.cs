using System.Collections.ObjectModel;
using System.Diagnostics;
using Avalonia.Collections;
using Avalonia.Controls;
using Avalonia.Media.Imaging;
using Avalonia.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using RedFox.GameExtraction;
using RedFox.GameExtraction.UI.Models;

namespace RedFox.GameExtraction.UI.ViewModels;

/// <summary>
/// Coordinates mounted sources, asset rows, and export operations for the main window.
/// </summary>
public partial class MainWindowViewModel : ObservableObject, IDisposable
{
    private readonly AssetManager _assetManager;
    private readonly GameExtractionConfig _config;
    private readonly Func<MainWindowViewModel, Control?> _previewControlFactory;
    private readonly List<AssetRowViewModel> _allAssets = [];
    private readonly DataGridCollectionView _assetsView;
    private string _assetNameFilter = string.Empty;
    private CancellationTokenSource? _currentCts;
    private CancellationTokenSource? _previewLoadCts;
    private int _previewLoadVersion;
    private Control? _previewControl;
    private bool _isPreviewWindowOpen;

    /// <summary>
    /// Initializes a new instance of the <see cref="MainWindowViewModel"/> class.
    /// </summary>
    /// <param name="config">The application configuration.</param>
    public MainWindowViewModel(GameExtractionConfig config)
    {
        _config = config;
        _assetManager = config.AssetManagerFactory();
        _previewControlFactory = config.PreviewControlFactory;
        _assetsView = new DataGridCollectionView(_allAssets)
        {
            Filter = FilterAssetRow,
        };
        _assetManager.OperationFailed += OnOperationFailed;
        _assetManager.AssetExportCompleted += OnAssetExportCompleted;

        InitializeShellState(config);

        LoadedSources.CollectionChanged += (_, _) =>
        {
            SourceCount = LoadedSources.Count;
        };
    }

    /// <summary>
    /// Gets the application configuration.
    /// </summary>
    public GameExtractionConfig Config => _config;

    /// <summary>
    /// Gets the filtered asset view bound to the asset grid.
    /// </summary>
    public DataGridCollectionView AssetsView => _assetsView;

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
    /// Gets or sets the selected asset currently being previewed.
    /// </summary>
    [ObservableProperty]
    public partial AssetRowViewModel? PreviewAsset { get; private set; }

    /// <summary>
    /// Gets or sets the most recent raw preview read result.
    /// </summary>
    [ObservableProperty]
    public partial AssetReadResult? PreviewReadResult { get; private set; }

    /// <summary>
    /// Gets or sets the most recent preview data payload.
    /// </summary>
    [ObservableProperty]
    public partial object? PreviewData { get; private set; }

    /// <summary>
    /// Gets or sets the byte payload displayed by the hex previewer.
    /// </summary>
    [ObservableProperty]
    public partial byte[]? PreviewBytes { get; private set; }

    /// <summary>
    /// Gets the active preview control selected for the current payload.
    /// </summary>
    public Control? PreviewControl
    {
        get => _previewControl;
        private set
        {
            if (ReferenceEquals(_previewControl, value))
            {
                return;
            }

            _previewControl = value;
            OnPropertyChanged();
            OnPropertyChanged(nameof(HasPreviewControl));
            OnPropertyChanged(nameof(ShowPreviewPlaceholder));
        }
    }

    /// <summary>
    /// Gets or sets a value indicating whether preview data is loading.
    /// </summary>
    [ObservableProperty]
    public partial bool IsPreviewLoading { get; private set; }

    /// <summary>
    /// Gets or sets the current preview status message.
    /// </summary>
    [ObservableProperty]
    public partial string PreviewStatusText { get; private set; } = "Waiting for selection";

    /// <summary>
    /// Gets or sets the active handler display name.
    /// </summary>
    [ObservableProperty]
    public partial string HandlerDisplay { get; private set; } = "-";

    /// <summary>
    /// Gets or sets the payload type display name.
    /// </summary>
    [ObservableProperty]
    public partial string PayloadTypeDisplay { get; private set; } = "-";

    /// <summary>
    /// Gets or sets the reference count display text.
    /// </summary>
    [ObservableProperty]
    public partial string ReferenceCountDisplay { get; private set; } = "0";

    /// <summary>
    /// Gets or sets the number of selected assets reflected in the preview window.
    /// </summary>
    [ObservableProperty]
    public partial int PreviewSelectionCount { get; private set; }

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
    /// Gets or sets the title displayed in the preview body.
    /// </summary>
    [ObservableProperty]
    public partial string ContentTitle { get; private set; } = "No asset selected";

    /// <summary>
    /// Gets or sets the description displayed in the preview body.
    /// </summary>
    [ObservableProperty]
    public partial string ContentText { get; private set; } = "Select an asset in the main window to capture data for a future preview surface.";

    /// <summary>
    /// Gets the preview window title.
    /// </summary>
    public string WindowTitle => PreviewAsset?.Name ?? "Preview";

    /// <summary>
    /// Gets the selected asset name display.
    /// </summary>
    public string AssetNameDisplay => PreviewAsset?.Name ?? "No asset selected";

    /// <summary>
    /// Gets the selected asset type display.
    /// </summary>
    public string AssetTypeDisplay => PreviewAsset?.Type ?? string.Empty;

    /// <summary>
    /// Gets the selected source name display.
    /// </summary>
    public string SourceNameDisplay => PreviewAsset?.SourceName ?? string.Empty;

    /// <summary>
    /// Gets the selected asset size display.
    /// </summary>
    public string SizeDisplay => PreviewAsset?.SizeDisplay ?? "-";

    /// <summary>
    /// Gets the selected asset information display.
    /// </summary>
    public string InformationDisplay => PreviewAsset?.Information ?? "Select an asset to inspect its preview payload.";

    /// <summary>
    /// Gets the selection summary display.
    /// </summary>
    public string SelectionDisplay => PreviewSelectionCount switch
    {
        <= 0 => "No selection",
        1 => "1 selected",
        _ => $"{PreviewSelectionCount} selected, previewing the latest",
    };

    /// <summary>
    /// Gets a value indicating whether preview data is available for future controls.
    /// </summary>
    public bool HasPreviewData => PreviewData is not null;

    /// <summary>
    /// Gets a value indicating whether the current payload can be displayed as hex bytes.
    /// </summary>
    public bool HasPreviewBytes => PreviewBytes is not null;

    /// <summary>
    /// Gets a value indicating whether a preview control is active for the current payload.
    /// </summary>
    public bool HasPreviewControl => PreviewControl is not null;

    /// <summary>
    /// Gets a value indicating whether the placeholder content should be shown.
    /// </summary>
    public bool ShowPreviewPlaceholder => PreviewControl is null;

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
    public Bitmap? SidebarIcon { get; private set; }

    /// <summary>
    /// Gets a value indicating whether an icon is available to display.
    /// </summary>
    public bool HasSidebarIcon => SidebarIcon is not null;

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
    public string SourceCountDisplay => SourceCount > 0
        ? $"Manage Sources ({SourceCount})"
        : "Manage Sources";

    /// <summary>
    /// Gets or sets the current status text.
    /// </summary>
    [ObservableProperty]
    public partial string StatusText { get; set; } = "Ready";

    /// <summary>
    /// Gets or sets the loaded asset count shown in the status bar.
    /// </summary>
    [ObservableProperty]
    public partial int AssetCount { get; set; }

    /// <summary>
    /// Gets the status bar asset count text.
    /// </summary>
    public string AssetCountDisplay => AssetCount > 0
        ? $"{AssetCount:N0} assets loaded"
        : "No assets loaded";

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
        string fullPath = Path.GetFullPath(directoryPath);
        if (HasMountedLocation(AssetSourceKind.Directory, fullPath))
        {
            StatusText = $"Already loaded: {Path.GetFileName(fullPath)}";
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
        if (!LoadedSources.Contains(source))
        {
            return;
        }

        try
        {
            await _assetManager.UnloadAsync(source.Source).ConfigureAwait(false);
            await Dispatcher.UIThread.InvokeAsync(() =>
            {
                RemoveSource(source);
                LoadedSources.Remove(source);
                AssetCount = TotalCount;
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
        ApplyFilter();
        if (_isPreviewWindowOpen)
        {
            _ = UpdatePreviewSelectionAsync(SelectedAssets);
        }
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
        if (_isPreviewWindowOpen)
        {
            _ = UpdatePreviewSelectionAsync(SelectedAssets);
        }
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
        ApplyFilter();
        if (_isPreviewWindowOpen)
        {
            _ = UpdatePreviewSelectionAsync([]);
        }
    }

    /// <summary>
    /// Opens the preview window for the specified asset row.
    /// </summary>
    /// <param name="asset">The asset row to preview.</param>
    public void OpenPreview(AssetRowViewModel asset)
    {
        SelectedAsset = asset;
        _ = UpdatePreviewSelectionAsync([asset]);
        PreviewRequested?.Invoke();
    }

    public void SetPreviewWindowOpen(bool value)
    {
        if (_isPreviewWindowOpen == value)
        {
            return;
        }

        _isPreviewWindowOpen = value;
        if (value)
        {
            _ = UpdatePreviewSelectionAsync(SelectedAssets);
            return;
        }

        CancelPendingPreviewLoad();
        IsPreviewLoading = false;
    }

    /// <inheritdoc/>
    public void Dispose()
    {
        _assetManager.OperationFailed -= OnOperationFailed;
        _assetManager.AssetExportCompleted -= OnAssetExportCompleted;
        _currentCts?.Cancel();
        _currentCts?.Dispose();
        _currentCts = null;
        CancelPendingPreviewLoad();
    }

    partial void OnIsLoadingChanged(bool value)
    {
    }

    partial void OnSearchTextChanged(string value)
    {
        _assetNameFilter = value.Trim();
        ApplyFilter();
    }

    partial void OnPreviewAssetChanged(AssetRowViewModel? value)
    {
        OnPropertyChanged(nameof(WindowTitle));
        OnPropertyChanged(nameof(AssetNameDisplay));
        OnPropertyChanged(nameof(AssetTypeDisplay));
        OnPropertyChanged(nameof(SourceNameDisplay));
        OnPropertyChanged(nameof(SizeDisplay));
        OnPropertyChanged(nameof(InformationDisplay));
    }

    partial void OnPreviewSelectionCountChanged(int value)
    {
        OnPropertyChanged(nameof(SelectionDisplay));
    }

    partial void OnPreviewDataChanged(object? value)
    {
        OnPropertyChanged(nameof(HasPreviewData));
    }

    partial void OnPreviewBytesChanged(byte[]? value)
    {
        OnPropertyChanged(nameof(HasPreviewBytes));
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
        _ = UpdatePreviewSelectionAsync(SelectedAssets);
        PreviewRequested?.Invoke();
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

    partial void OnAssetCountChanged(int value)
    {
        OnPropertyChanged(nameof(AssetCountDisplay));
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

        await MountSourceAsync(
            $"Loading {process.DisplayName}...",
            (progress, cancellationToken) => _assetManager.MountProcessAsync(
                process.ProcessId,
                _config.SourceOptions,
                progress,
                cancellationToken)).ConfigureAwait(true);
    }

    private async Task<IReadOnlyList<ProcessCandidateViewModel>> DiscoverProcessCandidatesAsync(bool applyConfiguredFilter)
    {
        if (IsLoading)
        {
            return [];
        }

        IsLoading = true;
        _currentCts = new CancellationTokenSource();
        CancellationTokenSource cts = _currentCts;
        ProgressDialogViewModel progressVm = CreateProgressDialog("Scanning processes...", cts);
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

            if (_assetManager.SourceReaders.Count == 0)
            {
                return [];
            }

            List<ProcessCandidateViewModel> candidates = [];

            foreach (Process process in Process.GetProcesses().OrderBy(process => GetProcessName(process), StringComparer.OrdinalIgnoreCase))
            {
                using var candidate = process;

                cts.Token.ThrowIfCancellationRequested();

                try
                {
                    int processId = process.Id;
                    var processName = process.ProcessName;

                    if (processId <= 0)
                        continue;

                    string windowTitle = GetProcessWindowTitle(process);
                    progress.Report($"Checking {processName} ({processId})");

                    AssetSourceRequest request = AssetSourceRequest.ForProcess(processId, _config.SourceOptions);
                    List<ProcessReaderViewModel> matchingReaders = [.. FindMatchingProcessReaders(request)];

                    if (matchingReaders.Count <= 0)
                        continue;

                    candidates.Add(new ProcessCandidateViewModel(
                        request,
                        processId,
                        processName,
                        windowTitle,
                        matchingReaders));
                }
                catch
                {
                }
            }

            return candidates;
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
            _currentCts?.Dispose();
            _currentCts = null;
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

            IAssetSource source = await Task.Run(
                () => mountSourceAsync(progress, cts.Token),
                cts.Token).ConfigureAwait(true);

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
            _currentCts?.Dispose();
            _currentCts = null;
        }
    }

    private void AddMountedSource(IAssetSource source)
    {
        _assetManager.TryGetSourceRequest(source, out AssetSourceRequest? request);
        AssetSourceViewModel sourceRow = new(source, request);
        LoadedSources.Add(sourceRow);
        AddSource(sourceRow);
        AssetCount = TotalCount;
    }

    private void InitializeShellState(GameExtractionConfig config)
    {
        SidebarTitle = config.SidebarTitle;
        SidebarDescription = config.Description;
        CanLoadFiles = config.SupportsFileSources;
        CanLoadDirectories = config.SupportsDirectorySources;
        CanLoadProcess = config.SupportsProcessSources;
        SidebarIcon = LoadIcon(config.SidebarIconPath ?? config.IconPath);
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
        _currentCts = new CancellationTokenSource();
        CancellationTokenSource cts = _currentCts;
        ProgressDialogViewModel progressVm = CreateProgressDialog($"Exporting {rowList.Count:N0} assets...", cts);
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
            await Task.Run(
                () => _assetManager.ExportAsync(assets, configuration, progress, cts.Token),
                cts.Token).ConfigureAwait(true);

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
            _currentCts?.Dispose();
            _currentCts = null;
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
        AssetCount = 0;
        StatusText = "Cleared all sources";
    }

    private bool HasMountedLocation(AssetSourceKind kind, string location)
    {
        return LoadedSources.Any(source =>
            source.Request?.Kind == kind &&
            string.Equals(source.Location, location, StringComparison.OrdinalIgnoreCase));
    }

    private void OpenSourceManager()
    {
        SourceManagerRequested?.Invoke();
    }

    public async Task UpdatePreviewSelectionAsync(IEnumerable<AssetRowViewModel> selectedAssets)
    {
        AssetRowViewModel[] selection = [.. selectedAssets];
        PreviewSelectionCount = selection.Length;
        AssetRowViewModel? asset = selection.LastOrDefault();
        PreviewAsset = asset;

        if (asset is null)
        {
            CancelPendingPreviewLoad();
            IsPreviewLoading = false;
            PreviewReadResult = null;
            PreviewData = null;
            PreviewBytes = null;
            PreviewControl = null;
            ContentTitle = "No asset selected";
            ContentText = "Select an asset in the main window to capture data for a future preview surface.";
            PreviewStatusText = "Waiting for selection";
            HandlerDisplay = "-";
            PayloadTypeDisplay = "-";
            ReferenceCountDisplay = "0";
            return;
        }

        int loadVersion = ++_previewLoadVersion;
        CancellationTokenSource cts = BeginPreviewLoad();

        IsPreviewLoading = true;
        HandlerDisplay = ResolveHandlerName(asset.Asset);
        PayloadTypeDisplay = "-";
        ReferenceCountDisplay = "0";
        PreviewStatusText = $"Reading {asset.Name} for preview";
        PreviewReadResult = null;
        PreviewData = null;
        PreviewBytes = null;
        PreviewControl = null;
        ContentTitle = "Loading asset data";
        ContentText = "Reading the selected asset through the registered asset handler.";

        try
        {
            AssetReadResult readResult = await _assetManager
                .ReadAsync(asset.Asset, cts.Token)
                .ConfigureAwait(true);

            if (loadVersion != _previewLoadVersion || cts.IsCancellationRequested)
            {
                return;
            }

            string handlerName = ResolveHandlerName(asset.Asset);
            PreviewData = ExtractPayload(readResult, out Type? payloadType);
            PreviewBytes = PreviewData as byte[];
            PreviewReadResult = readResult;
            HandlerDisplay = handlerName;
            PayloadTypeDisplay = payloadType?.Name ?? "Unknown";
            ReferenceCountDisplay = readResult.References.Count.ToString("N0");
            ContentTitle = PreviewBytes is not null ? "Hex preview" : PreviewData is null ? "Asset data loaded" : "Asset data captured";
            ContentText = PreviewBytes is not null
                ? $"{PreviewBytes.Length:N0} bytes"
                : PreviewData is null
                    ? "The asset handler returned a result without a typed payload. Future preview controls can still inspect the read result."
                    : "No compatible preview control accepted this payload.";
            PreviewControl = CreatePreviewControl();
            if (PreviewControl is null && PreviewData is not null)
            {
                ContentTitle = "No preview available";
                ContentText = "No compatible preview control is configured for this payload.";
            }

            PreviewStatusText = $"Data ready ({PayloadTypeDisplay})";
        }
        catch (OperationCanceledException)
        {
            if (loadVersion == _previewLoadVersion && PreviewAsset is null)
            {
                PreviewStatusText = "Waiting for selection";
            }
        }
        catch (Exception exception)
        {
            if (loadVersion != _previewLoadVersion || cts.IsCancellationRequested)
            {
                return;
            }

            PreviewReadResult = null;
            PreviewData = null;
            PreviewBytes = null;
            PreviewControl = null;
            ContentTitle = "Preview read failed";
            ContentText = exception.Message;
            PayloadTypeDisplay = "-";
            ReferenceCountDisplay = "0";
            PreviewStatusText = exception.Message;
        }
        finally
        {
            if (loadVersion == _previewLoadVersion)
            {
                IsPreviewLoading = false;
            }
        }
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

    private void ApplyFilter()
    {
        _assetsView.Refresh();
        FilteredCount = _assetsView.Count;
    }

    private bool FilterAssetRow(object item)
    {
        if (item is not AssetRowViewModel asset || string.IsNullOrEmpty(_assetNameFilter))
        {
            return true;
        }

        return asset.Name.Contains(_assetNameFilter, StringComparison.OrdinalIgnoreCase);
    }

    private CancellationTokenSource BeginPreviewLoad()
    {
        CancelPendingPreviewLoad();
        _previewLoadCts = new CancellationTokenSource();
        return _previewLoadCts;
    }

    private void CancelPendingPreviewLoad()
    {
        CancellationTokenSource? previous = _previewLoadCts;
        _previewLoadCts = null;

        if (previous is null)
        {
            return;
        }

        previous.Cancel();
        previous.Dispose();
    }

    private static object? ExtractPayload(AssetReadResult readResult, out Type? payloadType)
    {
        Type resultType = readResult.GetType();
        if (resultType.IsGenericType && resultType.GetGenericTypeDefinition() == typeof(AssetReadResult<>))
        {
            payloadType = resultType.GetGenericArguments()[0];
            return resultType.GetProperty("Data")?.GetValue(readResult);
        }

        payloadType = null;
        return null;
    }

    private Control? CreatePreviewControl()
    {
        try
        {
            return _previewControlFactory(this);
        }
        catch (Exception exception)
        {
            ContentTitle = "Preview control failed";
            ContentText = exception.Message;
            PreviewStatusText = exception.Message;
            return null;
        }
    }

    private string ResolveHandlerName(Asset asset)
    {
        IAssetHandler? handler = _assetManager.FindHandler(asset);
        return handler?.GetType().Name ?? "Unknown Handler";
    }

    private static Bitmap? LoadIcon(string? path)
    {
        if (string.IsNullOrWhiteSpace(path))
        {
            return null;
        }

        string fullPath = Path.IsPathRooted(path)
            ? path
            : Path.Combine(AppContext.BaseDirectory, path);

        if (!File.Exists(fullPath))
        {
            return null;
        }

        try
        {
            return new Bitmap(fullPath);
        }
        catch
        {
            return null;
        }
    }
}
