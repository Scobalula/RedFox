using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Templates;
using Avalonia.Data;
using Avalonia.Layout;
using Avalonia.Media;
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

        ListBox listBox = this.FindControl<ListBox>("SourceList")!;
        listBox.ItemsSource = _viewModel.Sources;
        listBox.ItemTemplate = CreateItemTemplate();
    }

    private FuncDataTemplate<AssetSourceViewModel> CreateItemTemplate()
    {
        return new FuncDataTemplate<AssetSourceViewModel>((source, _) =>
        {
            Grid grid = new()
            {
                ColumnDefinitions = ColumnDefinitions.Parse("*,Auto"),
                Margin = new Thickness(6, 8),
            };

            StackPanel stack = new() { Spacing = 2 };

            TextBlock nameBlock = new()
            {
                FontSize = 14,
                FontWeight = FontWeight.Medium,
                Foreground = new SolidColorBrush(Color.Parse("#E0E0F0")),
                [!TextBlock.TextProperty] = new Binding("DisplayName"),
            };

            TextBlock pathBlock = new()
            {
                FontSize = 11,
                Foreground = new SolidColorBrush(Color.Parse("#6868A0")),
                TextTrimming = TextTrimming.CharacterEllipsis,
                [!TextBlock.TextProperty] = new Binding("Location"),
            };

            TextBlock kindBlock = new()
            {
                FontSize = 11,
                Foreground = new SolidColorBrush(Color.Parse("#787880")),
                [!TextBlock.TextProperty] = new Binding("Kind"),
            };

            TextBlock countBlock = new()
            {
                FontSize = 12,
                Foreground = new SolidColorBrush(Color.Parse("#9090B0")),
                [!TextBlock.TextProperty] = new Binding("AssetCount") { StringFormat = "{0:N0} assets" },
            };

            stack.Children.Add(nameBlock);
            stack.Children.Add(pathBlock);
            stack.Children.Add(kindBlock);
            stack.Children.Add(countBlock);

            Grid.SetColumn(stack, 0);
            grid.Children.Add(stack);

            Button unloadButton = new()
            {
                Content = "Unload",
                Classes = { "cancel-btn" },
                VerticalAlignment = VerticalAlignment.Center,
                Margin = new Thickness(12, 0, 0, 0),
            };
            unloadButton.Click += async (_, _) =>
            {
                if (source is not null && _viewModel is not null)
                {
                    await _viewModel.UnloadCommand.ExecuteAsync(source).ConfigureAwait(true);
                }
            };

            Grid.SetColumn(unloadButton, 1);
            grid.Children.Add(unloadButton);

            return grid;
        });
    }
}
