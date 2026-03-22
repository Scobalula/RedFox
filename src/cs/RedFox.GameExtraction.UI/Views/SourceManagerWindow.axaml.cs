using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Templates;
using Avalonia.Data;
using Avalonia.Layout;
using Avalonia.Media;
using RedFox.GameExtraction;
using RedFox.GameExtraction.UI.ViewModels;

namespace RedFox.GameExtraction.UI.Views;

public partial class SourceManagerWindow : Window
{
    private SourceManagerViewModel? _viewModel;

    public SourceManagerWindow()
    {
        InitializeComponent();
    }

    public void Initialize(MainWindowViewModel mainVm)
    {
        _viewModel = new SourceManagerViewModel(mainVm.LoadedSources, mainVm.UnloadSource);

        // Bind the ListBox items in code to avoid compiled binding resolution issues
        var listBox = this.FindControl<ListBox>("SourceList")!;
        listBox.ItemsSource = _viewModel.Sources;
        listBox.ItemTemplate = CreateItemTemplate();
    }

    private FuncDataTemplate<LoadedSource> CreateItemTemplate()
    {
        return new FuncDataTemplate<LoadedSource>((source, _) =>
        {
            var grid = new Grid
            {
                ColumnDefinitions = ColumnDefinitions.Parse("*,Auto"),
                Margin = new Thickness(6, 8)
            };

            var stack = new StackPanel { Spacing = 2 };

            var nameBlock = new TextBlock
            {
                FontSize = 14,
                FontWeight = FontWeight.Medium,
                Foreground = new SolidColorBrush(Color.Parse("#E0E0F0")),
                [!TextBlock.TextProperty] = new Binding("DisplayName")
            };

            var pathBlock = new TextBlock
            {
                FontSize = 11,
                Foreground = new SolidColorBrush(Color.Parse("#6868A0")),
                TextTrimming = TextTrimming.CharacterEllipsis,
                [!TextBlock.TextProperty] = new Binding("Location")
            };

            var countBlock = new TextBlock
            {
                FontSize = 12,
                Foreground = new SolidColorBrush(Color.Parse("#9090B0")),
                [!TextBlock.TextProperty] = new Binding("AssetCount") { StringFormat = "{0:N0} assets" }
            };

            stack.Children.Add(nameBlock);
            stack.Children.Add(pathBlock);
            stack.Children.Add(countBlock);

            Grid.SetColumn(stack, 0);
            grid.Children.Add(stack);

            var unloadBtn = new Button
            {
                Content = "Unload",
                Classes = { "cancel-btn" },
                VerticalAlignment = VerticalAlignment.Center,
                Margin = new Thickness(12, 0, 0, 0)
            };
            unloadBtn.Click += (_, _) =>
            {
                if (source is not null)
                    _viewModel?.UnloadCommand.Execute(source);
            };

            Grid.SetColumn(unloadBtn, 1);
            grid.Children.Add(unloadBtn);

            return grid;
        });
    }
}
