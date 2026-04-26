using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Controls.Templates;
using Avalonia.Data;
using Avalonia.Input;
using Avalonia.Layout;
using RedFox.Graphics3D;
using RedFox.Graphics3D.Avalonia;
using AvaloniaGrid = Avalonia.Controls.Grid;
using AvaloniaThickness = Avalonia.Thickness;

namespace RedFox.Samples.Examples;

/// <summary>
/// Hosts the Avalonia OpenGL renderer sample window.
/// </summary>
internal sealed class AvaloniaSampleWindow : Window
{
    private readonly AvaloniaSampleViewModel _viewModel;

    public AvaloniaSampleWindow(string[] arguments)
    {
        _viewModel = new AvaloniaSampleViewModel(arguments);
        DataContext = _viewModel;
        Title = "RedFox Avalonia OpenGL Renderer";
        Width = 1280;
        Height = 720;
        MinWidth = 960;
        MinHeight = 540;
        Content = CreateContent();
        KeyDown += OnKeyDown;
    }

    private Control CreateContent()
    {
        AvaloniaGrid root = new()
        {
            RowDefinitions = new RowDefinitions("Auto,*,Auto"),
            ColumnDefinitions = new ColumnDefinitions("280,*")
        };

        StackPanel toolbar = CreateToolbar();
        AvaloniaGrid.SetRow(toolbar, 0);
        AvaloniaGrid.SetColumnSpan(toolbar, 2);
        root.Children.Add(toolbar);

        TreeView tree = CreateSceneTree();
        AvaloniaGrid.SetRow(tree, 1);
        AvaloniaGrid.SetColumn(tree, 0);
        root.Children.Add(tree);

        AvaloniaOpenGlRendererControl renderer = CreateRendererControl();
        AvaloniaGrid.SetRow(renderer, 1);
        AvaloniaGrid.SetColumn(renderer, 1);
        root.Children.Add(renderer);

        TextBlock status = new()
        {
            Margin = new AvaloniaThickness(8, 4),
            VerticalAlignment = VerticalAlignment.Center
        };
        status.Bind(TextBlock.TextProperty, new Binding(nameof(AvaloniaSampleViewModel.Status)));
        AvaloniaGrid.SetRow(status, 2);
        AvaloniaGrid.SetColumnSpan(status, 2);
        root.Children.Add(status);

        return root;
    }

    private StackPanel CreateToolbar()
    {
        StackPanel toolbar = new()
        {
            Orientation = Orientation.Horizontal,
            Spacing = 8,
            Margin = new AvaloniaThickness(8)
        };

        Button openButton = new()
        {
            Content = "Open"
        };
        openButton.Click += async (_, _) => await _viewModel.OpenFilesAsync(StorageProvider);
        toolbar.Children.Add(openButton);

        toolbar.Children.Add(CreateCommandButton("New", nameof(AvaloniaSampleViewModel.NewSceneCommand)));
        toolbar.Children.Add(CreateCommandButton("Add Node", nameof(AvaloniaSampleViewModel.AddTriangleCommand)));
        toolbar.Children.Add(CreateCommandButton("Remove Node", nameof(AvaloniaSampleViewModel.RemoveSelectedNodeCommand)));
        toolbar.Children.Add(CreateCommandButton("Fit", nameof(AvaloniaSampleViewModel.FitCameraCommand)));
        toolbar.Children.Add(CreateCommandButton("Clear", nameof(AvaloniaSampleViewModel.ClearSceneCommand)));

        CheckBox viewLighting = new()
        {
            Content = "View lighting",
            VerticalAlignment = VerticalAlignment.Center
        };
        viewLighting.Bind(ToggleButton.IsCheckedProperty, new Binding(nameof(AvaloniaSampleViewModel.UseViewBasedLighting)) { Mode = BindingMode.TwoWay });
        toolbar.Children.Add(viewLighting);

        CheckBox pauseAnimation = new()
        {
            Content = "Pause animation",
            VerticalAlignment = VerticalAlignment.Center
        };
        pauseAnimation.Bind(ToggleButton.IsCheckedProperty, new Binding(nameof(AvaloniaSampleViewModel.IsAnimationPaused)) { Mode = BindingMode.TwoWay });
        toolbar.Children.Add(pauseAnimation);

        CheckBox showGrid = new()
        {
            Content = "Grid",
            VerticalAlignment = VerticalAlignment.Center
        };
        showGrid.Bind(ToggleButton.IsCheckedProperty, new Binding(nameof(AvaloniaSampleViewModel.ShowGrid)) { Mode = BindingMode.TwoWay });
        toolbar.Children.Add(showGrid);

        ComboBox skinningMode = new()
        {
            ItemsSource = Enum.GetValues<SkinningMode>(),
            Width = 160,
            VerticalAlignment = VerticalAlignment.Center
        };
        skinningMode.Bind(SelectingItemsControl.SelectedItemProperty, new Binding(nameof(AvaloniaSampleViewModel.SkinningMode)) { Mode = BindingMode.TwoWay });
        toolbar.Children.Add(skinningMode);

        return toolbar;
    }

    private Button CreateCommandButton(string text, string commandProperty)
    {
        Button button = new()
        {
            Content = text
        };
        button.Bind(Button.CommandProperty, new Binding(commandProperty));
        return button;
    }

    private TreeView CreateSceneTree()
    {
        TreeView tree = new()
        {
            Margin = new AvaloniaThickness(8, 0, 4, 0),
            ItemTemplate = new FuncTreeDataTemplate<SceneNodeItem>(
                static (item, _) => new TextBlock { Text = item.Name },
                static item => item.Children)
        };
        tree.Bind(ItemsControl.ItemsSourceProperty, new Binding(nameof(AvaloniaSampleViewModel.Nodes)));
        tree.Bind(SelectingItemsControl.SelectedItemProperty, new Binding(nameof(AvaloniaSampleViewModel.SelectedNodeItem)) { Mode = BindingMode.TwoWay });
        return tree;
    }

    private AvaloniaOpenGlRendererControl CreateRendererControl()
    {
        AvaloniaOpenGlRendererControl renderer = new()
        {
            Margin = new AvaloniaThickness(4, 0, 8, 0)
        };
        renderer.Bind(AvaloniaOpenGlRendererControl.SceneProperty, new Binding(nameof(AvaloniaSampleViewModel.Scene)));
        renderer.Bind(AvaloniaOpenGlRendererControl.CameraProperty, new Binding(nameof(AvaloniaSampleViewModel.Camera)));
        renderer.Bind(AvaloniaOpenGlRendererControl.ViewportControllerProperty, new Binding(nameof(AvaloniaSampleViewModel.ViewportController)));
        renderer.Bind(AvaloniaOpenGlRendererControl.SelectedNodeProperty, new Binding(nameof(AvaloniaSampleViewModel.SelectedNode)) { Mode = BindingMode.TwoWay });
        renderer.Bind(AvaloniaOpenGlRendererControl.ClearColorProperty, new Binding(nameof(AvaloniaSampleViewModel.ClearColor)));
        renderer.Bind(AvaloniaOpenGlRendererControl.UseViewBasedLightingProperty, new Binding(nameof(AvaloniaSampleViewModel.UseViewBasedLighting)));
        renderer.Bind(AvaloniaOpenGlRendererControl.SkinningModeProperty, new Binding(nameof(AvaloniaSampleViewModel.SkinningMode)));
        renderer.Bind(AvaloniaOpenGlRendererControl.IsAnimationPausedProperty, new Binding(nameof(AvaloniaSampleViewModel.IsAnimationPaused)));
        return renderer;
    }

    private void OnKeyDown(object? sender, KeyEventArgs e)
    {
        switch (e.Key)
        {
            case Key.L:
                _viewModel.ToggleViewLighting();
                break;

            case Key.B:
                _viewModel.ToggleSkeletonBones();
                break;

            case Key.F:
                _viewModel.FitCameraToScene();
                break;

            case Key.K:
                _viewModel.ToggleSkinningMode();
                break;

            case Key.Space:
                _viewModel.ToggleAnimationPause();
                break;

            case Key.T:
                _viewModel.ToggleBindPoseAnimation();
                break;

            case Key.G:
                _viewModel.ToggleGridCommand.Execute(null);
                break;
        }
    }
}
