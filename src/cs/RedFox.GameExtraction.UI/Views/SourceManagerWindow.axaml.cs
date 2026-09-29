using Avalonia.Controls;
using Avalonia.Interactivity;
using RedFox.GameExtraction.UI.Models;
using RedFox.GameExtraction.UI.ViewModels;

namespace RedFox.GameExtraction.UI.Views;

/// <summary>
/// Window for inspecting and unloading mounted sources.
/// </summary>
public partial class SourceManagerWindow : Window
{
    private SourceManagerViewModel? _viewModel;

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
        _viewModel = new SourceManagerViewModel(mainViewModel.LoadedSources, mainViewModel.UnloadSourceAsync);
        DataContext = _viewModel;
        SourceList.ItemsSource = _viewModel.Sources;
    }

    private async void OnUnloadClick(object? sender, RoutedEventArgs e)
    {
        if (sender is Button { Tag: AssetSourceViewModel source } && _viewModel is not null)
        {
            await _viewModel.UnloadCommand.ExecuteAsync(source).ConfigureAwait(true);
        }
    }
}
