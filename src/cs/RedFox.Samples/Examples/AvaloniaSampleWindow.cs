using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Controls.Templates;
using Avalonia.Data;
using Avalonia.Input;
using Avalonia.Layout;
using Avalonia.Media;
using RedFox.Graphics3D;
using RedFox.Graphics3D.Avalonia;
using RedFox.Graphics3D.Skeletal;
using AvaloniaGrid = Avalonia.Controls.Grid;
using AvaloniaThickness = Avalonia.Thickness;

namespace RedFox.Samples.Examples;

/// <summary>
/// Hosts the Avalonia OpenGL renderer sample window.
/// </summary>
internal sealed class AvaloniaSampleWindow : Window
{
    private readonly FrameStatsReporter? _frameStatsReporter;
    private readonly AvaloniaSampleViewModel _viewModel;
    private double _frameStatsElapsedSeconds;

    public AvaloniaSampleWindow(string[] arguments)
    {
        _viewModel = new AvaloniaSampleViewModel(arguments);
        _frameStatsReporter = HasFrameStats(arguments) ? new FrameStatsReporter(true) : null;
        DataContext = _viewModel;
        Title = "RedFox Avalonia OpenGL Renderer";
        Width = 1280;
        Height = 720;
        MinWidth = 960;
        MinHeight = 540;
        Content = CreateContent();
        KeyDown += OnKeyDown;
        Closed += (_, _) => _frameStatsReporter?.PrintFinal();
    }

    private Control CreateContent()
    {
        DockPanel root = new();

        Border toolbar = CreateToolbar();
        DockPanel.SetDock(toolbar, Dock.Top);
        root.Children.Add(toolbar);

        Border status = CreateStatusBar();
        DockPanel.SetDock(status, Dock.Bottom);
        root.Children.Add(status);

        AvaloniaGrid content = new()
        {
            ColumnDefinitions = new ColumnDefinitions("280,*"),
        };

        TreeView tree = CreateSceneTree();
        Border treeHost = new()
        {
            Child = tree,
        };
        treeHost.Classes.Add("sidebar-host");
        AvaloniaGrid.SetColumn(treeHost, 0);
        content.Children.Add(treeHost);

        AvaloniaOpenGlRendererControl renderer = CreateRendererControl();
        AvaloniaGrid.SetColumn(renderer, 1);
        content.Children.Add(renderer);

        root.Children.Add(content);
        return root;
    }

    private Border CreateStatusBar()
    {
        TextBlock status = new()
        {
            TextTrimming = TextTrimming.CharacterEllipsis,
        };
        status.Classes.Add("status-text");
        status.Bind(TextBlock.TextProperty, new Binding(nameof(AvaloniaSampleViewModel.Status)));

        Border host = new()
        {
            Child = status,
        };
        host.Classes.Add("status-bar");
        return host;
    }

    private Border CreateToolbar()
    {
        StackPanel toolbar = new()
        {
            Orientation = Orientation.Horizontal,
            Spacing = 8,
            VerticalAlignment = VerticalAlignment.Center,
        };

        Button openButton = new()
        {
            Content = "Open",
        };
        openButton.Classes.Add("accent");
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

        Border host = new()
        {
            Child = toolbar,
        };
        host.Classes.Add("sample-toolbar");
        return host;
    }

    private Button CreateCommandButton(string text, string commandProperty)
    {
        Button button = new()
        {
            Content = text,
        };
        button.Classes.Add("toolbar-btn");
        button.Bind(Button.CommandProperty, new Binding(commandProperty));
        return button;
    }

    private TreeView CreateSceneTree()
    {
        TreeView tree = new()
        {
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
        AvaloniaOpenGlRendererControl renderer = new();
        renderer.Bind(AvaloniaOpenGlRendererControl.SceneProperty, new Binding(nameof(AvaloniaSampleViewModel.Scene)));
        renderer.Bind(AvaloniaOpenGlRendererControl.CameraProperty, new Binding(nameof(AvaloniaSampleViewModel.Camera)));
        renderer.Bind(AvaloniaOpenGlRendererControl.ViewportControllerProperty, new Binding(nameof(AvaloniaSampleViewModel.ViewportController)));
        renderer.Bind(AvaloniaOpenGlRendererControl.SelectedNodeProperty, new Binding(nameof(AvaloniaSampleViewModel.SelectedNode)) { Mode = BindingMode.TwoWay });
        renderer.Bind(AvaloniaOpenGlRendererControl.ClearColorProperty, new Binding(nameof(AvaloniaSampleViewModel.ClearColor)));
        renderer.Bind(AvaloniaOpenGlRendererControl.UseViewBasedLightingProperty, new Binding(nameof(AvaloniaSampleViewModel.UseViewBasedLighting)));
        renderer.Bind(AvaloniaOpenGlRendererControl.SkinningModeProperty, new Binding(nameof(AvaloniaSampleViewModel.SkinningMode)));
        renderer.Bind(AvaloniaOpenGlRendererControl.IsAnimationPausedProperty, new Binding(nameof(AvaloniaSampleViewModel.IsAnimationPaused)));
        if (_frameStatsReporter is not null)
        {
            renderer.RenderFrame += OnRendererFrame;
        }

        return renderer;
    }

    private void OnRendererFrame(object? sender, AvaloniaRenderFrameEventArgs e)
    {
        _frameStatsElapsedSeconds += e.ElapsedTime.TotalSeconds;
        _frameStatsReporter?.Record(e.RenderDuration.TotalMilliseconds, _frameStatsElapsedSeconds);
    }

    private static bool HasFrameStats(string[] arguments)
    {
        for (int i = 0; i < arguments.Length; i++)
        {
            string argument = arguments[i];
            if (argument.Equals("--frame-stats", StringComparison.OrdinalIgnoreCase)
                || argument.Equals("--stats", StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
        }

        return false;
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
