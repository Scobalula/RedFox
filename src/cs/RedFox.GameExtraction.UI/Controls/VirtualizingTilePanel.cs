using System.Collections.Specialized;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Generators;
using Avalonia.Input;
using Avalonia.Layout;

namespace RedFox.GameExtraction.UI.Controls;

internal sealed class VirtualizingTilePanel : VirtualizingPanel
{
    private readonly Dictionary<int, Control>	_realized		= [];
    private readonly Stack<Control>				_recyclePool	= [];
    private Rect								_viewport;
    private int									_columns		= 1;

    public static readonly StyledProperty<double> ItemWidthProperty		= AvaloniaProperty.Register<VirtualizingTilePanel, double>(nameof(ItemWidth), 150);
    public static readonly StyledProperty<double> ItemHeightProperty	= AvaloniaProperty.Register<VirtualizingTilePanel, double>(nameof(ItemHeight), 180);

    public double ItemWidth
    {
        get => GetValue(ItemWidthProperty);
        set => SetValue(ItemWidthProperty, value);
    }

    public double ItemHeight
    {
        get => GetValue(ItemHeightProperty);
        set => SetValue(ItemHeightProperty, value);
    }

    static VirtualizingTilePanel()
    {
        AffectsMeasure<VirtualizingTilePanel>(ItemWidthProperty, ItemHeightProperty);
    }

    public VirtualizingTilePanel()
    {
        EffectiveViewportChanged += OnEffectiveViewportChanged;
    }

    protected override Size MeasureOverride(Size availableSize)
    {
        int count	= Items.Count;
        _columns	= Math.Clamp((int)(availableSize.Width / ItemWidth), 1, Math.Max(1, count));

        int first	= Math.Max(0, (int)(_viewport.Top / ItemHeight)) * _columns;
        int last	= Math.Min(count, (int)Math.Ceiling(_viewport.Bottom / ItemHeight) * _columns);

        // The focused tile stays realized so keyboard navigation survives scrolling it out of view.
        foreach ((int index, Control container) in _realized.ToArray())
        {
            if ((index < first || index >= last) && !container.IsKeyboardFocusWithin)
            {
                Recycle(index, container);
            }
        }

        for (int index = first; index < last; index++)
        {
            if (!_realized.ContainsKey(index))
            {
                Realize(index);
            }
        }

        Size tileSize = new(ItemWidth, ItemHeight);
        foreach (Control container in _realized.Values)
        {
            container.Measure(tileSize);
        }

        int rows = (count + _columns - 1) / _columns;
        return new Size(_columns * ItemWidth, rows * ItemHeight);
    }

    protected override Size ArrangeOverride(Size finalSize)
    {
        foreach ((int index, Control container) in _realized)
        {
            container.Arrange(GetTileRect(index));
        }

        return finalSize;
    }

    protected override void OnItemsChanged(IReadOnlyList<object?> items, NotifyCollectionChangedEventArgs e)
    {
        base.OnItemsChanged(items, e);

        foreach ((int index, Control container) in _realized.ToArray())
        {
            Recycle(index, container);
        }

        InvalidateMeasure();
    }

    protected override Control? ScrollIntoView(int index)
    {
        if (index < 0 || index >= Items.Count)
        {
            return null;
        }

        if (!_realized.TryGetValue(index, out Control? container))
        {
            container = Realize(index);
            container.Measure(new Size(ItemWidth, ItemHeight));
            container.Arrange(GetTileRect(index));
        }

        container.BringIntoView();
        return container;
    }

    protected override Control? ContainerFromIndex(int index) => _realized.GetValueOrDefault(index);

    protected override int IndexFromContainer(Control container)
    {
        foreach ((int index, Control realized) in _realized)
        {
            if (ReferenceEquals(realized, container))
            {
                return index;
            }
        }

        return -1;
    }

    protected override IEnumerable<Control>? GetRealizedContainers() => _realized.Values;

    protected override IInputElement? GetControl(NavigationDirection direction, IInputElement? from, bool wrap)
    {
        int count = Items.Count;
        if (count == 0)
        {
            return null;
        }

        int fromIndex	= from is Control control ? IndexFromContainer(control) : -1;
        int pageSize	= Math.Max(1, (int)(_viewport.Height / ItemHeight)) * _columns;

        int toIndex = direction switch
        {
            NavigationDirection.First									=> 0,
            NavigationDirection.Last									=> count - 1,
            _ when fromIndex < 0										=> 0,
            NavigationDirection.Left or NavigationDirection.Previous	=> fromIndex - 1,
            NavigationDirection.Right or NavigationDirection.Next		=> fromIndex + 1,
            NavigationDirection.Up										=> fromIndex - _columns,
            NavigationDirection.Down									=> fromIndex + _columns,
            NavigationDirection.PageUp									=> fromIndex - pageSize,
            NavigationDirection.PageDown								=> fromIndex + pageSize,
            _															=> fromIndex,
        };

        toIndex = Math.Clamp(toIndex, 0, count - 1);
        return toIndex == fromIndex ? null : ScrollIntoView(toIndex);
    }

    private Control Realize(int index)
    {
        ItemContainerGenerator generator	= ItemContainerGenerator!;
        object? item						= Items[index];

        generator.NeedsContainer(item, index, out object? recycleKey);

        if (_recyclePool.TryPop(out Control? container))
        {
            container.SetCurrentValue(IsVisibleProperty, true);
            generator.PrepareItemContainer(container, item, index);
        }
        else
        {
            container = generator.CreateContainer(item, index, recycleKey);
            generator.PrepareItemContainer(container, item, index);
            AddInternalChild(container);
        }

        generator.ItemContainerPrepared(container, item, index);
        _realized[index] = container;
        return container;
    }

    private void Recycle(int index, Control container)
    {
        _realized.Remove(index);
        ItemContainerGenerator!.ClearItemContainer(container);
        container.SetCurrentValue(IsVisibleProperty, false);
        _recyclePool.Push(container);
    }

    private Rect GetTileRect(int index) => new(index % _columns * ItemWidth, index / _columns * ItemHeight, ItemWidth, ItemHeight);

    private void OnEffectiveViewportChanged(object? sender, EffectiveViewportChangedEventArgs e)
    {
        _viewport = e.EffectiveViewport;
        InvalidateMeasure();
    }
}
