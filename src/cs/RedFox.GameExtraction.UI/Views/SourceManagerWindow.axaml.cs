using Avalonia.Controls;
using Avalonia.Input;
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

    /// <summary>
    /// Closes the window when Escape is pressed.
    /// </summary>
    /// <param name="e">The key event data.</param>
    protected override void OnKeyDown(KeyEventArgs e)
    {
        base.OnKeyDown(e);

        if (!e.Handled && e.Key == Key.Escape)
        {
            Close();
            e.Handled = true;
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
