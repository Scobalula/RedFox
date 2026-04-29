using System.Collections.ObjectModel;

using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Controls.Templates;
using Avalonia.Data;
using Avalonia.Layout;
using Avalonia.Media;
using RedFox.Graphics3D.Skeletal;
using AvaloniaGrid = Avalonia.Controls.Grid;

namespace RedFox.Graphics3D.Avalonia;

/// <summary>
/// Provides a bindable view-only control for inspecting skeletal animation curve components and keyed values.
/// </summary>
public sealed class SkeletonAnimationCurveViewer : UserControl
{
    /// <summary>
    /// Defines the <see cref="Animation"/> property.
    /// </summary>
    public static readonly StyledProperty<SkeletonAnimation?> AnimationProperty =
        AvaloniaProperty.Register<SkeletonAnimationCurveViewer, SkeletonAnimation?>(nameof(Animation));

    /// <summary>
    /// Defines the <see cref="SelectedComponent"/> property.
    /// </summary>
    public static readonly StyledProperty<SkeletonAnimationCurveComponent?> SelectedComponentProperty =
        AvaloniaProperty.Register<SkeletonAnimationCurveViewer, SkeletonAnimationCurveComponent?>(nameof(SelectedComponent));

    /// <summary>
    /// Defines the <see cref="SelectedKey"/> property.
    /// </summary>
    public static readonly StyledProperty<SkeletonAnimationCurveKey?> SelectedKeyProperty =
        AvaloniaProperty.Register<SkeletonAnimationCurveViewer, SkeletonAnimationCurveKey?>(nameof(SelectedKey));

    /// <summary>
    /// Defines the <see cref="ViewMode"/> property.
    /// </summary>
    public static readonly StyledProperty<SkeletonAnimationCurveViewerMode> ViewModeProperty =
        AvaloniaProperty.Register<SkeletonAnimationCurveViewer, SkeletonAnimationCurveViewerMode>(nameof(ViewMode));

    /// <summary>
    /// Defines the <see cref="ShowGrid"/> property.
    /// </summary>
    public static readonly StyledProperty<bool> ShowGridProperty =
        SkeletonAnimationCurveGraphControl.ShowGridProperty.AddOwner<SkeletonAnimationCurveViewer>();

    /// <summary>
    /// Defines the <see cref="GraphBackground"/> property.
    /// </summary>
    public static readonly StyledProperty<IBrush> GraphBackgroundProperty =
        SkeletonAnimationCurveGraphControl.GraphBackgroundProperty.AddOwner<SkeletonAnimationCurveViewer>();

    /// <summary>
    /// Defines the <see cref="PlotBackground"/> property.
    /// </summary>
    public static readonly StyledProperty<IBrush> PlotBackgroundProperty =
        SkeletonAnimationCurveGraphControl.PlotBackgroundProperty.AddOwner<SkeletonAnimationCurveViewer>();

    /// <summary>
    /// Defines the <see cref="GridBrush"/> property.
    /// </summary>
    public static readonly StyledProperty<IBrush> GridBrushProperty =
        SkeletonAnimationCurveGraphControl.GridBrushProperty.AddOwner<SkeletonAnimationCurveViewer>();

    /// <summary>
    /// Defines the <see cref="MajorGridBrush"/> property.
    /// </summary>
    public static readonly StyledProperty<IBrush> MajorGridBrushProperty =
        SkeletonAnimationCurveGraphControl.MajorGridBrushProperty.AddOwner<SkeletonAnimationCurveViewer>();

    /// <summary>
    /// Defines the <see cref="AxisBrush"/> property.
    /// </summary>
    public static readonly StyledProperty<IBrush> AxisBrushProperty =
        SkeletonAnimationCurveGraphControl.AxisBrushProperty.AddOwner<SkeletonAnimationCurveViewer>();

    /// <summary>
    /// Defines the <see cref="PlotBorderBrush"/> property.
    /// </summary>
    public static readonly StyledProperty<IBrush?> PlotBorderBrushProperty =
        SkeletonAnimationCurveGraphControl.PlotBorderBrushProperty.AddOwner<SkeletonAnimationCurveViewer>();

    /// <summary>
    /// Defines the <see cref="CurveBrush"/> property.
    /// </summary>
    public static readonly StyledProperty<IBrush> CurveBrushProperty =
        SkeletonAnimationCurveGraphControl.CurveBrushProperty.AddOwner<SkeletonAnimationCurveViewer>();

    /// <summary>
    /// Defines the <see cref="KeyBrush"/> property.
    /// </summary>
    public static readonly StyledProperty<IBrush> KeyBrushProperty =
        SkeletonAnimationCurveGraphControl.KeyBrushProperty.AddOwner<SkeletonAnimationCurveViewer>();

    /// <summary>
    /// Defines the <see cref="SelectedKeyBrush"/> property.
    /// </summary>
    public static readonly StyledProperty<IBrush> SelectedKeyBrushProperty =
        SkeletonAnimationCurveGraphControl.SelectedKeyBrushProperty.AddOwner<SkeletonAnimationCurveViewer>();

    /// <summary>
    /// Defines the <see cref="SelectedKeyOutlineBrush"/> property.
    /// </summary>
    public static readonly StyledProperty<IBrush> SelectedKeyOutlineBrushProperty =
        SkeletonAnimationCurveGraphControl.SelectedKeyOutlineBrushProperty.AddOwner<SkeletonAnimationCurveViewer>();

    private readonly TextBlock _animationHeader;
    private readonly TextBlock _componentHeader;
    private readonly ObservableCollection<AnimationCurveListItem> _curveItems = [];
    private readonly ListBox _curveList;
    private readonly SkeletonAnimationCurveGraphControl _graph;
    private readonly ObservableCollection<SkeletonAnimationCurveKey> _keyItems = [];
    private readonly ListBox _keyList;
    private bool _updatingSelection;

    static SkeletonAnimationCurveViewer()
    {
        AnimationProperty.Changed.AddClassHandler<SkeletonAnimationCurveViewer>((viewer, _) => viewer.RebuildCurveList());
        SelectedComponentProperty.Changed.AddClassHandler<SkeletonAnimationCurveViewer>((viewer, _) => viewer.OnSelectedComponentChanged());
        SelectedKeyProperty.Changed.AddClassHandler<SkeletonAnimationCurveViewer>((viewer, _) => viewer.OnSelectedKeyChanged());
        ViewModeProperty.Changed.AddClassHandler<SkeletonAnimationCurveViewer>((viewer, _) => viewer.OnViewModeChanged());
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="SkeletonAnimationCurveViewer"/> class.
    /// </summary>
    public SkeletonAnimationCurveViewer()
    {
        _animationHeader = new TextBlock
        {
            FontWeight = FontWeight.SemiBold,
            Margin = new Thickness(8.0, 8.0, 8.0, 4.0),
            Text = "Animation Curves",
        };

        _curveList = new ListBox
        {
            ItemTemplate = new FuncDataTemplate<AnimationCurveListItem>(static (item, _) => CreateCurveListItem(item), supportsRecycling: true),
            ItemsSource = _curveItems,
            SelectionMode = SelectionMode.Single,
        };
        _curveList.SelectionChanged += OnCurveListSelectionChanged;

        _componentHeader = new TextBlock
        {
            FontWeight = FontWeight.SemiBold,
            VerticalAlignment = VerticalAlignment.Center,
            Text = "No curve component selected",
        };

        _graph = new SkeletonAnimationCurveGraphControl();
        BindGraphProperties();
        _graph.SelectedKeyChanged += OnGraphSelectedKeyChanged;

        _keyList = new ListBox
        {
            ItemTemplate = new FuncDataTemplate<SkeletonAnimationCurveKey>(static (key, _) => CreateKeyListItem(key), supportsRecycling: true),
            ItemsSource = _keyItems,
            SelectionMode = SelectionMode.Single,
        };
        _keyList.SelectionChanged += OnKeyListSelectionChanged;

        Content = CreateContent();
    }

    /// <summary>
    /// Gets or sets the animation displayed by the control.
    /// </summary>
    public SkeletonAnimation? Animation
    {
        get => GetValue(AnimationProperty);
        set => SetValue(AnimationProperty, value);
    }

    /// <summary>
    /// Gets or sets the selected curve component.
    /// </summary>
    public SkeletonAnimationCurveComponent? SelectedComponent
    {
        get => GetValue(SelectedComponentProperty);
        set => SetValue(SelectedComponentProperty, value);
    }

    /// <summary>
    /// Gets or sets the selected keyed value.
    /// </summary>
    public SkeletonAnimationCurveKey? SelectedKey
    {
        get => GetValue(SelectedKeyProperty);
        set => SetValue(SelectedKeyProperty, value);
    }

    /// <summary>
    /// Gets or sets which region of the viewer is displayed.
    /// </summary>
    public SkeletonAnimationCurveViewerMode ViewMode
    {
        get => GetValue(ViewModeProperty);
        set => SetValue(ViewModeProperty, value);
    }

    /// <summary>
    /// Gets or sets a value indicating whether graph grid lines are drawn.
    /// </summary>
    public bool ShowGrid
    {
        get => GetValue(ShowGridProperty);
        set => SetValue(ShowGridProperty, value);
    }

    /// <summary>
    /// Gets or sets the brush used behind the graph plot area.
    /// </summary>
    public IBrush GraphBackground
    {
        get => GetValue(GraphBackgroundProperty);
        set => SetValue(GraphBackgroundProperty, value);
    }

    /// <summary>
    /// Gets or sets the brush used for the plot area.
    /// </summary>
    public IBrush PlotBackground
    {
        get => GetValue(PlotBackgroundProperty);
        set => SetValue(PlotBackgroundProperty, value);
    }

    /// <summary>
    /// Gets or sets the brush used for minor grid lines.
    /// </summary>
    public IBrush GridBrush
    {
        get => GetValue(GridBrushProperty);
        set => SetValue(GridBrushProperty, value);
    }

    /// <summary>
    /// Gets or sets the brush used for major grid lines.
    /// </summary>
    public IBrush MajorGridBrush
    {
        get => GetValue(MajorGridBrushProperty);
        set => SetValue(MajorGridBrushProperty, value);
    }

    /// <summary>
    /// Gets or sets the brush used for graph axes.
    /// </summary>
    public IBrush AxisBrush
    {
        get => GetValue(AxisBrushProperty);
        set => SetValue(AxisBrushProperty, value);
    }

    /// <summary>
    /// Gets or sets the brush used for the plot border.
    /// </summary>
    public IBrush? PlotBorderBrush
    {
        get => GetValue(PlotBorderBrushProperty);
        set => SetValue(PlotBorderBrushProperty, value);
    }

    /// <summary>
    /// Gets or sets the brush used for the curve line.
    /// </summary>
    public IBrush CurveBrush
    {
        get => GetValue(CurveBrushProperty);
        set => SetValue(CurveBrushProperty, value);
    }

    /// <summary>
    /// Gets or sets the brush used for unselected keys.
    /// </summary>
    public IBrush KeyBrush
    {
        get => GetValue(KeyBrushProperty);
        set => SetValue(KeyBrushProperty, value);
    }

    /// <summary>
    /// Gets or sets the brush used for the selected key fill.
    /// </summary>
    public IBrush SelectedKeyBrush
    {
        get => GetValue(SelectedKeyBrushProperty);
        set => SetValue(SelectedKeyBrushProperty, value);
    }

    /// <summary>
    /// Gets or sets the brush used for the selected key outline.
    /// </summary>
    public IBrush SelectedKeyOutlineBrush
    {
        get => GetValue(SelectedKeyOutlineBrushProperty);
        set => SetValue(SelectedKeyOutlineBrushProperty, value);
    }

    /// <summary>
    /// Fits the graph to the selected component.
    /// </summary>
    public void FitSelectedComponent() => _graph.FitToComponent();

    private static Control CreateCurveListItem(AnimationCurveListItem item)
    {
        TextBlock textBlock = new()
        {
            FontWeight = item.Component is null ? FontWeight.SemiBold : FontWeight.Normal,
            Margin = new Thickness(8.0 + (item.Level * 16.0), 2.0, 4.0, 2.0),
            Opacity = item.Component is null ? 0.78 : 1.0,
            Text = item.Text,
            TextTrimming = TextTrimming.CharacterEllipsis,
        };
        return textBlock;
    }

    private static Control CreateKeyListItem(SkeletonAnimationCurveKey key)
    {
        TextBlock textBlock = new()
        {
            Margin = new Thickness(8.0, 2.0),
            Text = $"Frame {key.Frame:0.###}    Value {key.Value:0.###}",
            TextTrimming = TextTrimming.CharacterEllipsis,
        };
        return textBlock;
    }

    private static string GetComponentName(string curveName, int componentIndex, int componentCount)
    {
        if (curveName.Equals("Translation", StringComparison.OrdinalIgnoreCase)
            || curveName.Equals("Scale", StringComparison.OrdinalIgnoreCase))
        {
            return componentIndex switch
            {
                0 => "X",
                1 => "Y",
                2 => "Z",
                _ => $"C{componentIndex}",
            };
        }

        if (curveName.Equals("Rotation", StringComparison.OrdinalIgnoreCase) && componentCount == 4)
        {
            return componentIndex switch
            {
                0 => "X",
                1 => "Y",
                2 => "Z",
                3 => "W",
                _ => $"C{componentIndex}",
            };
        }

        return componentCount == 1 ? "Value" : $"C{componentIndex}";
    }

    private static bool IsSameKey(SkeletonAnimationCurveKey? first, SkeletonAnimationCurveKey? second)
    {
        if (first is null || second is null)
        {
            return first is null && second is null;
        }

        return ReferenceEquals(first.Component, second.Component) && first.KeyIndex == second.KeyIndex;
    }

    private Control CreateContent()
    {
        return ViewMode switch
        {
            SkeletonAnimationCurveViewerMode.CurveList => CreateCurveListContent(),
            SkeletonAnimationCurveViewerMode.Graph => CreateGraphContent(),
            _ => CreateFullContent(),
        };
    }

    private Control CreateFullContent()
    {
        AvaloniaGrid root = new()
        {
            ColumnDefinitions = new ColumnDefinitions("280,Auto,*"),
            RowDefinitions = new RowDefinitions("*"),
        };

        Control leftPanel = CreateCurveListContent();
        AvaloniaGrid.SetColumn(leftPanel, 0);
        root.Children.Add(leftPanel);

        GridSplitter splitter = new()
        {
            ResizeDirection = GridResizeDirection.Columns,
            Width = 5.0,
        };
        AvaloniaGrid.SetColumn(splitter, 1);
        root.Children.Add(splitter);

        Control rightPanel = CreateGraphContent();
        AvaloniaGrid.SetColumn(rightPanel, 2);
        root.Children.Add(rightPanel);
        return root;
    }

    private Control CreateCurveListContent()
    {
        AvaloniaGrid leftPanel = new()
        {
            RowDefinitions = new RowDefinitions("Auto,*"),
        };

        AvaloniaGrid.SetRow(_animationHeader, 0);
        AddPanelChild(leftPanel, _animationHeader);
        AvaloniaGrid.SetRow(_curveList, 1);
        AddPanelChild(leftPanel, _curveList);
        return leftPanel;
    }

    private Control CreateGraphContent()
    {
        AvaloniaGrid rightPanel = new()
        {
            RowDefinitions = new RowDefinitions("Auto,*,112"),
        };

        DockPanel header = new()
        {
            LastChildFill = true,
            Margin = new Thickness(8.0, 6.0, 8.0, 6.0),
        };
        Button fitButton = new()
        {
            Content = "Fit",
            Margin = new Thickness(8.0, 0.0, 0.0, 0.0),
        };
        fitButton.Click += (_, _) => FitSelectedComponent();
        DockPanel.SetDock(fitButton, Dock.Right);
        header.Children.Add(fitButton);
        AddPanelChild(header, _componentHeader);
        AvaloniaGrid.SetRow(header, 0);
        rightPanel.Children.Add(header);

        AvaloniaGrid.SetRow(_graph, 1);
        AddPanelChild(rightPanel, _graph);
        AvaloniaGrid.SetRow(_keyList, 2);
        AddPanelChild(rightPanel, _keyList);
        return rightPanel;
    }

    private static void AddPanelChild(Panel panel, Control child)
    {
        DetachFromParent(child);
        panel.Children.Add(child);
    }

    private static void DetachFromParent(Control control)
    {
        if (control.Parent is Panel panel)
        {
            panel.Children.Remove(control);
        }
    }

    private void AddCurveItems(List<AnimationCurveListItem> trackItems, SkeletonAnimation animation, SkeletonAnimationTrack track, string curveName, AnimationCurve? curve)
    {
        if (curve is not { KeyFrameCount: > 0, Values: not null })
        {
            return;
        }

        int componentCount = curve.ComponentCount;
        if (componentCount <= 0)
        {
            return;
        }

        trackItems.Add(new AnimationCurveListItem(1, $"{curveName} ({curve.KeyFrameCount})", null));
        for (int componentIndex = 0; componentIndex < componentCount; componentIndex++)
        {
            string componentName = GetComponentName(curveName, componentIndex, componentCount);
            SkeletonAnimationCurveComponent component = new(animation, track, curveName, curve, componentIndex, componentName);
            if (component.KeyFrameCount > 0)
            {
                trackItems.Add(new AnimationCurveListItem(2, $"{componentName} ({component.KeyFrameCount})", component));
            }
        }
    }

    private void BindGraphProperties()
    {
        _graph.Bind(SkeletonAnimationCurveGraphControl.ShowGridProperty, new Binding(nameof(ShowGrid)) { Source = this });
        _graph.Bind(SkeletonAnimationCurveGraphControl.GraphBackgroundProperty, new Binding(nameof(GraphBackground)) { Source = this });
        _graph.Bind(SkeletonAnimationCurveGraphControl.PlotBackgroundProperty, new Binding(nameof(PlotBackground)) { Source = this });
        _graph.Bind(SkeletonAnimationCurveGraphControl.GridBrushProperty, new Binding(nameof(GridBrush)) { Source = this });
        _graph.Bind(SkeletonAnimationCurveGraphControl.MajorGridBrushProperty, new Binding(nameof(MajorGridBrush)) { Source = this });
        _graph.Bind(SkeletonAnimationCurveGraphControl.AxisBrushProperty, new Binding(nameof(AxisBrush)) { Source = this });
        _graph.Bind(SkeletonAnimationCurveGraphControl.PlotBorderBrushProperty, new Binding(nameof(PlotBorderBrush)) { Source = this });
        _graph.Bind(SkeletonAnimationCurveGraphControl.CurveBrushProperty, new Binding(nameof(CurveBrush)) { Source = this });
        _graph.Bind(SkeletonAnimationCurveGraphControl.KeyBrushProperty, new Binding(nameof(KeyBrush)) { Source = this });
        _graph.Bind(SkeletonAnimationCurveGraphControl.SelectedKeyBrushProperty, new Binding(nameof(SelectedKeyBrush)) { Source = this });
        _graph.Bind(SkeletonAnimationCurveGraphControl.SelectedKeyOutlineBrushProperty, new Binding(nameof(SelectedKeyOutlineBrush)) { Source = this });
    }

    private void OnCurveListSelectionChanged(object? sender, SelectionChangedEventArgs e)
    {
        if (_updatingSelection || _curveList.SelectedItem is not AnimationCurveListItem item)
        {
            return;
        }

        if (item.Component is null)
        {
            AnimationCurveListItem? firstComponent = ResolveFirstComponentAfter(item);
            if (firstComponent is not null)
            {
                SelectCurveListItem(firstComponent);
                SelectedComponent = firstComponent.Component;
                SelectedKey = null;
            }

            return;
        }

        SelectedComponent = item.Component;
        SelectedKey = null;
    }

    private void OnGraphSelectedKeyChanged(object? sender, EventArgs e)
    {
        if (_updatingSelection)
        {
            return;
        }

        SkeletonAnimationCurveKey? key = _graph.SelectedKey;
        if (key is not null && ReferenceEquals(key.Component, SelectedComponent))
        {
            SelectedKey = key;
        }
    }

    private void OnKeyListSelectionChanged(object? sender, SelectionChangedEventArgs e)
    {
        if (_updatingSelection)
        {
            return;
        }

        if (_keyList.SelectedItem is SkeletonAnimationCurveKey key)
        {
            SelectedKey = key;
        }
    }

    private void OnSelectedComponentChanged()
    {
        SkeletonAnimationCurveComponent? component = SelectedComponent;
        _componentHeader.Text = component?.DisplayName ?? "No curve component selected";
        _graph.Component = component;
        RebuildKeyList(component);
        SelectCurveListItem(component);

        SkeletonAnimationCurveKey? key = SelectedKey;
        if (key is not null && !ReferenceEquals(key.Component, component))
        {
            SelectedKey = null;
        }
    }

    private void OnSelectedKeyChanged()
    {
        SkeletonAnimationCurveKey? key = SelectedKey;
        if (key is not null && !ReferenceEquals(SelectedComponent, key.Component))
        {
            SelectedComponent = key.Component;
        }

        if (!IsSameKey(_graph.SelectedKey, key))
        {
            _graph.SelectedKey = key;
        }

        SelectKeyListItem(key);
    }

    private void OnViewModeChanged()
    {
        Content = null;
        Content = CreateContent();
    }

    private void RebuildCurveList()
    {
        SkeletonAnimation? animation = Animation;
        _updatingSelection = true;
        _curveItems.Clear();
        _keyItems.Clear();
        _curveList.SelectedItem = null;
        _keyList.SelectedItem = null;
        _updatingSelection = false;

        if (animation is null)
        {
            _animationHeader.Text = "Animation Curves";
            SelectedComponent = null;
            SelectedKey = null;
            return;
        }

        _animationHeader.Text = string.IsNullOrWhiteSpace(animation.Name)
            ? "Animation Curves"
            : animation.Name;

        AnimationCurveListItem? firstComponent = null;
        for (int trackIndex = 0; trackIndex < animation.Tracks.Count; trackIndex++)
        {
            SkeletonAnimationTrack track = animation.Tracks[trackIndex];
            List<AnimationCurveListItem> trackItems = [];
            AddCurveItems(trackItems, animation, track, "Translation", track.TranslationCurve);
            AddCurveItems(trackItems, animation, track, "Rotation", track.RotationCurve);
            AddCurveItems(trackItems, animation, track, "Scale", track.ScaleCurve);
            if (track.CustomCurves is not null)
            {
                foreach (KeyValuePair<string, AnimationCurve> customCurve in track.CustomCurves)
                {
                    AddCurveItems(trackItems, animation, track, customCurve.Key, customCurve.Value);
                }
            }

            if (trackItems.Count == 0)
            {
                continue;
            }

            _curveItems.Add(new AnimationCurveListItem(0, track.Name, null));
            for (int itemIndex = 0; itemIndex < trackItems.Count; itemIndex++)
            {
                AnimationCurveListItem item = trackItems[itemIndex];
                _curveItems.Add(item);
                firstComponent ??= item.Component is not null ? item : null;
            }
        }

        if (firstComponent?.Component is not null)
        {
            SelectedComponent = firstComponent.Component;
            SelectCurveListItem(firstComponent);
        }
        else
        {
            SelectedComponent = null;
            SelectedKey = null;
        }
    }

    private void RebuildKeyList(SkeletonAnimationCurveComponent? component)
    {
        _updatingSelection = true;
        _keyItems.Clear();
        if (component is not null)
        {
            for (int keyIndex = 0; keyIndex < component.KeyFrameCount; keyIndex++)
            {
                _keyItems.Add(component.CreateKey(keyIndex));
            }
        }

        _updatingSelection = false;
        SelectKeyListItem(SelectedKey);
    }

    private AnimationCurveListItem? ResolveFirstComponentAfter(AnimationCurveListItem item)
    {
        int startIndex = _curveItems.IndexOf(item);
        if (startIndex < 0)
        {
            return null;
        }

        for (int itemIndex = startIndex + 1; itemIndex < _curveItems.Count; itemIndex++)
        {
            AnimationCurveListItem candidate = _curveItems[itemIndex];
            if (candidate.Level <= item.Level)
            {
                return null;
            }

            if (candidate.Component is not null)
            {
                return candidate;
            }
        }

        return null;
    }

    private void SelectCurveListItem(SkeletonAnimationCurveComponent? component)
    {
        if (component is null)
        {
            SelectCurveListItem((AnimationCurveListItem?)null);
            return;
        }

        for (int itemIndex = 0; itemIndex < _curveItems.Count; itemIndex++)
        {
            AnimationCurveListItem item = _curveItems[itemIndex];
            if (ReferenceEquals(item.Component, component))
            {
                SelectCurveListItem(item);
                return;
            }
        }
    }

    private void SelectCurveListItem(AnimationCurveListItem? item)
    {
        if (ReferenceEquals(_curveList.SelectedItem, item))
        {
            return;
        }

        _updatingSelection = true;
        _curveList.SelectedItem = item;
        _updatingSelection = false;
    }

    private void SelectKeyListItem(SkeletonAnimationCurveKey? key)
    {
        SkeletonAnimationCurveKey? selectedItem = null;
        if (key is not null)
        {
            for (int keyIndex = 0; keyIndex < _keyItems.Count; keyIndex++)
            {
                SkeletonAnimationCurveKey candidate = _keyItems[keyIndex];
                if (IsSameKey(candidate, key))
                {
                    selectedItem = candidate;
                    break;
                }
            }
        }

        if (ReferenceEquals(_keyList.SelectedItem, selectedItem))
        {
            return;
        }

        _updatingSelection = true;
        _keyList.SelectedItem = selectedItem;
        _updatingSelection = false;
    }
}