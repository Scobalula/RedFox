using Avalonia.Controls;
using Avalonia.Data;
using Avalonia.Interactivity;
using RedFox.GameExtraction;
using RedFox.GameExtraction.UI.Models;
using RedFox.GameExtraction.UI.ViewModels;

namespace RedFox.GameExtraction.UI.Views;

/// <summary>
/// Window for inspecting and unloading mounted sources.
/// </summary>
public partial class SourceManagerWindow : Window
{
    private SourceManagerViewModel? _viewModel;
    private MainWindowViewModel? _mainViewModel;

    /// <summary>
    /// Initializes a new instance of the <see cref="SourceManagerWindow"/> class.
    /// </summary>
    public SourceManagerWindow()
    {
        InitializeComponent();
    }

    /// <summary>
    /// Initializes the source manager window from the main window model.
    /// </summary>
    /// <param name="mainViewModel">The main window view model.</param>
    public void Initialize(MainWindowViewModel mainViewModel)
    {
        ArgumentNullException.ThrowIfNull(mainViewModel);
        _mainViewModel = mainViewModel;
        _viewModel = new SourceManagerViewModel(mainViewModel.LoadedSources, mainViewModel.UnloadSourceAsync);
        DataContext = _viewModel;
        SourceList.ItemsSource = _viewModel.Sources;
        ProgressOverlay.Bind(IsVisibleProperty, new Binding(nameof(MainWindowViewModel.ShowProgressDialog)) { Source = mainViewModel });
        ProgressCard.Bind(DataContextProperty, new Binding(nameof(MainWindowViewModel.ProgressDialog)) { Source = mainViewModel });
    }

    private async void OnBrowseClick(object? sender, RoutedEventArgs e)
    {
        if (sender is not Button { Tag: AssetSourceViewModel { Source: IAssetSourceContainer container } source } || _mainViewModel is null)
        {
            return;
        }

        SourceBrowserWindow browserWindow = new();
        browserWindow.Initialize(source.DisplayName, container.MountableAssets);
        IReadOnlyList<Asset>? selection = await browserWindow.ShowDialog<IReadOnlyList<Asset>?>(this).ConfigureAwait(true);

        if (selection is { Count: > 0 })
        {
            await _mainViewModel.LoadFromContainerAsync(container, selection).ConfigureAwait(true);
        }
    }

    private async void OnUnloadClick(object? sender, RoutedEventArgs e)
    {
        if (sender is Button { Tag: AssetSourceViewModel source } && _viewModel is not null)
        {
            await _viewModel.UnloadCommand.ExecuteAsync(source).ConfigureAwait(true);
        }
    }
}
