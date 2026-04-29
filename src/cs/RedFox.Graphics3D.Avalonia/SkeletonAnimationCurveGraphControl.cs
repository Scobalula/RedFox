using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Media;

namespace RedFox.Graphics3D.Avalonia;

/// <summary>
/// Draws and navigates a single skeletal animation curve component.
/// </summary>
public sealed class SkeletonAnimationCurveGraphControl : Control
{
    /// <summary>
    /// Defines the <see cref="Component"/> property.
    /// </summary>
    public static readonly StyledProperty<SkeletonAnimationCurveComponent?> ComponentProperty =
        AvaloniaProperty.Register<SkeletonAnimationCurveGraphControl, SkeletonAnimationCurveComponent?>(nameof(Component));

    /// <summary>
    /// Defines the <see cref="SelectedKey"/> property.
    /// </summary>
    public static readonly StyledProperty<SkeletonAnimationCurveKey?> SelectedKeyProperty =
        AvaloniaProperty.Register<SkeletonAnimationCurveGraphControl, SkeletonAnimationCurveKey?>(nameof(SelectedKey));

    /// <summary>
    /// Defines the <see cref="ShowGrid"/> property.
    /// </summary>
    public static readonly StyledProperty<bool> ShowGridProperty =
        AvaloniaProperty.Register<SkeletonAnimationCurveGraphControl, bool>(nameof(ShowGrid), defaultValue: true);

    /// <summary>
    /// Defines the <see cref="GraphBackground"/> property.
    /// </summary>
    public static readonly StyledProperty<IBrush> GraphBackgroundProperty =
        AvaloniaProperty.Register<SkeletonAnimationCurveGraphControl, IBrush>(nameof(GraphBackground), new SolidColorBrush(Color.FromRgb(18, 20, 24)));

    /// <summary>
    /// Defines the <see cref="PlotBackground"/> property.
    /// </summary>
    public static readonly StyledProperty<IBrush> PlotBackgroundProperty =
        AvaloniaProperty.Register<SkeletonAnimationCurveGraphControl, IBrush>(nameof(PlotBackground), new SolidColorBrush(Color.FromRgb(28, 31, 37)));

    /// <summary>
    /// Defines the <see cref="GridBrush"/> property.
    /// </summary>
    public static readonly StyledProperty<IBrush> GridBrushProperty =
        AvaloniaProperty.Register<SkeletonAnimationCurveGraphControl, IBrush>(nameof(GridBrush), new SolidColorBrush(Color.FromRgb(42, 47, 56)));

    /// <summary>
    /// Defines the <see cref="MajorGridBrush"/> property.
    /// </summary>
    public static readonly StyledProperty<IBrush> MajorGridBrushProperty =
        AvaloniaProperty.Register<SkeletonAnimationCurveGraphControl, IBrush>(nameof(MajorGridBrush), new SolidColorBrush(Color.FromRgb(54, 61, 72)));

    /// <summary>
    /// Defines the <see cref="AxisBrush"/> property.
    /// </summary>
    public static readonly StyledProperty<IBrush> AxisBrushProperty =
        AvaloniaProperty.Register<SkeletonAnimationCurveGraphControl, IBrush>(nameof(AxisBrush), new SolidColorBrush(Color.FromRgb(92, 100, 112)));

    /// <summary>
    /// Defines the <see cref="PlotBorderBrush"/> property.
    /// </summary>
    public static readonly StyledProperty<IBrush?> PlotBorderBrushProperty =
        AvaloniaProperty.Register<SkeletonAnimationCurveGraphControl, IBrush?>(nameof(PlotBorderBrush));

    /// <summary>
    /// Defines the <see cref="CurveBrush"/> property.
    /// </summary>
    public static readonly StyledProperty<IBrush> CurveBrushProperty =
        AvaloniaProperty.Register<SkeletonAnimationCurveGraphControl, IBrush>(nameof(CurveBrush), new SolidColorBrush(Color.FromRgb(248, 178, 75)));

    /// <summary>
    /// Defines the <see cref="KeyBrush"/> property.
    /// </summary>
    public static readonly StyledProperty<IBrush> KeyBrushProperty =
        AvaloniaProperty.Register<SkeletonAnimationCurveGraphControl, IBrush>(nameof(KeyBrush), new SolidColorBrush(Color.FromRgb(248, 178, 75)));

    /// <summary>
    /// Defines the <see cref="SelectedKeyBrush"/> property.
    /// </summary>
    public static readonly StyledProperty<IBrush> SelectedKeyBrushProperty =
        AvaloniaProperty.Register<SkeletonAnimationCurveGraphControl, IBrush>(nameof(SelectedKeyBrush), new SolidColorBrush(Color.FromRgb(78, 213, 190)));

    /// <summary>
    /// Defines the <see cref="SelectedKeyOutlineBrush"/> property.
    /// </summary>
    public static readonly StyledProperty<IBrush> SelectedKeyOutlineBrushProperty =
        AvaloniaProperty.Register<SkeletonAnimationCurveGraphControl, IBrush>(nameof(SelectedKeyOutlineBrush), new SolidColorBrush(Color.FromRgb(219, 245, 240)));

    private const double PlotPaddingBottom = 0.0;
    private const double PlotPaddingLeft = 0.0;
    private const double PlotPaddingRight = 0.0;
    private const double PlotPaddingTop = 0.0;
    private const double SelectionRadius = 8.0;
    private const double MinimumGridSpacingPixels = 48.0;

    private SkeletonAnimationCurveGraphDragMode _dragMode;
    private Point _lastPointerPosition;
    private double _viewMaxFrame = 1.0;
    private double _viewMaxValue = 1.0;
    private double _viewMinFrame;
    private double _viewMinValue = -1.0;

    static SkeletonAnimationCurveGraphControl()
    {
        AffectsRender<SkeletonAnimationCurveGraphControl>(
            ShowGridProperty,
            GraphBackgroundProperty,
            PlotBackgroundProperty,
            GridBrushProperty,
            MajorGridBrushProperty,
            AxisBrushProperty,
            PlotBorderBrushProperty,
            CurveBrushProperty,
            KeyBrushProperty,
            SelectedKeyBrushProperty,
            SelectedKeyOutlineBrushProperty);
        ComponentProperty.Changed.AddClassHandler<SkeletonAnimationCurveGraphControl>((control, _) => control.OnComponentChanged());
        SelectedKeyProperty.Changed.AddClassHandler<SkeletonAnimationCurveGraphControl>((control, _) => control.OnSelectedKeyChanged());
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="SkeletonAnimationCurveGraphControl"/> class.
    /// </summary>
    public SkeletonAnimationCurveGraphControl()
    {
        ClipToBounds = true;
        Focusable = true;
        MinHeight = 160.0;
    }

    /// <summary>
    /// Occurs when <see cref="SelectedKey"/> changes through graph interaction.
    /// </summary>
    public event EventHandler? SelectedKeyChanged;

    /// <summary>
    /// Gets or sets the component drawn by the graph.
    /// </summary>
    public SkeletonAnimationCurveComponent? Component
    {
        get => GetValue(ComponentProperty);
        set => SetValue(ComponentProperty, value);
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
    /// Fits the graph view to the selected component's frame and value range.
    /// </summary>
    public void FitToComponent()
    {
        SkeletonAnimationCurveComponent? component = Component;
        if (component is null || component.KeyFrameCount == 0)
        {
            _viewMinFrame = 0.0;
            _viewMaxFrame = 1.0;
            _viewMinValue = -1.0;
            _viewMaxValue = 1.0;
            InvalidateVisual();
            return;
        }

        float minFrame = float.MaxValue;
        float maxFrame = float.MinValue;
        float minValue = float.MaxValue;
        float maxValue = float.MinValue;
        for (int keyIndex = 0; keyIndex < component.KeyFrameCount; keyIndex++)
        {
            float frame = component.GetFrame(keyIndex);
            float value = component.GetValue(keyIndex);
            minFrame = MathF.Min(minFrame, frame);
            maxFrame = MathF.Max(maxFrame, frame);
            minValue = MathF.Min(minValue, value);
            maxValue = MathF.Max(maxValue, value);
        }

        double frameSpan = Math.Max(maxFrame - minFrame, 1.0f);
        double valueSpan = Math.Max(maxValue - minValue, 1.0f);
        double framePadding = frameSpan * 0.06;
        double valuePadding = valueSpan * 0.12;
        _viewMinFrame = minFrame - framePadding;
        _viewMaxFrame = maxFrame + framePadding;
        _viewMinValue = minValue - valuePadding;
        _viewMaxValue = maxValue + valuePadding;
        InvalidateVisual();
    }

    /// <inheritdoc/>
    public override void Render(DrawingContext context)
    {
        base.Render(context);

        Rect bounds = new(0.0, 0.0, Bounds.Width, Bounds.Height);
        context.DrawRectangle(GraphBackground, null, bounds);
        Rect plotArea = GetPlotArea(Bounds.Size);
        context.DrawRectangle(PlotBackground, null, plotArea);

        if (Component is not { KeyFrameCount: > 0 } component || !HasValidView())
        {
            DrawPlotBorder(context, plotArea);
            return;
        }

        Rect clipArea = GetPlotClipArea(plotArea);
        using (context.PushClip(clipArea))
        {
            if (ShowGrid)
            {
                DrawGrid(context, plotArea);
            }

            DrawCurve(context, plotArea, component);
            DrawKeys(context, plotArea, component);
        }

        DrawPlotBorder(context, plotArea);
    }

    /// <inheritdoc/>
    protected override void OnPointerPressed(PointerPressedEventArgs e)
    {
        base.OnPointerPressed(e);
        if (Component is not { KeyFrameCount: > 0 })
        {
            return;
        }

        Focus(NavigationMethod.Pointer);
        PointerPoint point = e.GetCurrentPoint(this);
        Point position = e.GetPosition(this);
        if (point.Properties.IsLeftButtonPressed)
        {
            SkeletonAnimationCurveKey? nearestKey = FindNearestKey(position, SelectionRadius);
            if (nearestKey is not null)
            {
                SelectedKey = nearestKey;
                e.Handled = true;
            }

            return;
        }

        if (!e.KeyModifiers.HasFlag(KeyModifiers.Alt))
        {
            return;
        }

        if (point.Properties.IsMiddleButtonPressed)
        {
            BeginDrag(e, position, SkeletonAnimationCurveGraphDragMode.Pan);
            return;
        }

        if (point.Properties.IsRightButtonPressed)
        {
            BeginDrag(e, position, SkeletonAnimationCurveGraphDragMode.Zoom);
        }
    }

    /// <inheritdoc/>
    protected override void OnPointerMoved(PointerEventArgs e)
    {
        base.OnPointerMoved(e);
        if (_dragMode == SkeletonAnimationCurveGraphDragMode.None || Component is not { KeyFrameCount: > 0 })
        {
            return;
        }

        Point position = e.GetPosition(this);
        double deltaX = position.X - _lastPointerPosition.X;
        double deltaY = position.Y - _lastPointerPosition.Y;
        _lastPointerPosition = position;

        if (_dragMode == SkeletonAnimationCurveGraphDragMode.Pan)
        {
            PanBy(deltaX, deltaY);
        }
        else if (_dragMode == SkeletonAnimationCurveGraphDragMode.Zoom)
        {
            double zoomFactor = Math.Pow(1.01, deltaY);
            ZoomAt(position, zoomFactor, zoomFactor);
        }

        e.Handled = true;
    }

    /// <inheritdoc/>
    protected override void OnPointerReleased(PointerReleasedEventArgs e)
    {
        base.OnPointerReleased(e);
        if (_dragMode == SkeletonAnimationCurveGraphDragMode.None)
        {
            return;
        }

        _dragMode = SkeletonAnimationCurveGraphDragMode.None;
        e.Pointer.Capture(null);
        e.Handled = true;
    }

    /// <inheritdoc/>
    protected override void OnPointerWheelChanged(PointerWheelEventArgs e)
    {
        base.OnPointerWheelChanged(e);
        if (Component is not { KeyFrameCount: > 0 })
        {
            return;
        }

        double zoomFactor = Math.Pow(0.88, e.Delta.Y);
        ZoomAt(e.GetPosition(this), zoomFactor, zoomFactor);
        e.Handled = true;
    }

    private static Rect GetPlotArea(Size size)
    {
        double width = Math.Max(1.0, size.Width - PlotPaddingLeft - PlotPaddingRight);
        double height = Math.Max(1.0, size.Height - PlotPaddingTop - PlotPaddingBottom);
        return new Rect(PlotPaddingLeft, PlotPaddingTop, width, height);
    }

    private static Rect GetPlotClipArea(Rect plotArea)
    {
        return new Rect(
            plotArea.X + 1.0,
            plotArea.Y + 1.0,
            Math.Max(1.0, plotArea.Width - 2.0),
            Math.Max(1.0, plotArea.Height - 2.0));
    }

    private static bool IsSameKey(SkeletonAnimationCurveKey? first, SkeletonAnimationCurveKey? second)
    {
        if (first is null || second is null)
        {
            return first is null && second is null;
        }

        return ReferenceEquals(first.Component, second.Component) && first.KeyIndex == second.KeyIndex;
    }

    private void BeginDrag(PointerPressedEventArgs e, Point position, SkeletonAnimationCurveGraphDragMode dragMode)
    {
        _dragMode = dragMode;
        _lastPointerPosition = position;
        e.Pointer.Capture(this);
        e.Handled = true;
    }

    private void DrawCurve(DrawingContext context, Rect plotArea, SkeletonAnimationCurveComponent component)
    {
        Pen curvePen = new(CurveBrush, 1.6);
        Point? previous = null;
        for (int keyIndex = 0; keyIndex < component.KeyFrameCount; keyIndex++)
        {
            Point point = ToScreenPoint(plotArea, component.GetFrame(keyIndex), component.GetValue(keyIndex));
            if (previous is Point previousPoint)
            {
                context.DrawLine(curvePen, previousPoint, point);
            }

            previous = point;
        }
    }

    private void DrawGrid(DrawingContext context, Rect plotArea)
    {
        DrawVerticalGridLines(context, plotArea);
        DrawHorizontalGridLines(context, plotArea);
    }

    private void DrawKeys(DrawingContext context, Rect plotArea, SkeletonAnimationCurveComponent component)
    {
        SkeletonAnimationCurveKey? selectedKey = SelectedKey;
        Pen selectedKeyPen = new(SelectedKeyOutlineBrush, 1.5);
        for (int keyIndex = 0; keyIndex < component.KeyFrameCount; keyIndex++)
        {
            Point point = ToScreenPoint(plotArea, component.GetFrame(keyIndex), component.GetValue(keyIndex));
            bool selected = IsSameKey(selectedKey, component.CreateKey(keyIndex));
            IBrush brush = selected ? SelectedKeyBrush : KeyBrush;
            Pen? pen = selected ? selectedKeyPen : null;
            double radius = selected ? 4.5 : 3.2;
            context.DrawEllipse(brush, pen, point, radius, radius);
        }
    }

    private SkeletonAnimationCurveKey? FindNearestKey(Point position, double maxDistance)
    {
        SkeletonAnimationCurveComponent? component = Component;
        if (component is null)
        {
            return null;
        }

        Rect plotArea = GetPlotArea(Bounds.Size);
        double maxDistanceSquared = maxDistance * maxDistance;
        double nearestDistanceSquared = maxDistanceSquared;
        int nearestKeyIndex = -1;
        for (int keyIndex = 0; keyIndex < component.KeyFrameCount; keyIndex++)
        {
            Point point = ToScreenPoint(plotArea, component.GetFrame(keyIndex), component.GetValue(keyIndex));
            double deltaX = point.X - position.X;
            double deltaY = point.Y - position.Y;
            double distanceSquared = (deltaX * deltaX) + (deltaY * deltaY);
            if (distanceSquared <= nearestDistanceSquared)
            {
                nearestDistanceSquared = distanceSquared;
                nearestKeyIndex = keyIndex;
            }
        }

        return nearestKeyIndex >= 0 ? component.CreateKey(nearestKeyIndex) : null;
    }

    private void DrawHorizontalGridLines(DrawingContext context, Rect plotArea)
    {
        Pen gridPen = new(GridBrush, 1.0);
        Pen majorGridPen = new(MajorGridBrush, 1.0);
        Pen axisPen = new(AxisBrush, 1.0);
        double valueSpan = _viewMaxValue - _viewMinValue;
        double step = CalculateGridStep(valueSpan, plotArea.Height);
        if (!double.IsFinite(step) || step <= 0.0)
        {
            return;
        }

        double firstValue = Math.Ceiling(_viewMinValue / step) * step;
        for (double value = firstValue; value <= _viewMaxValue + (step * 0.5); value += step)
        {
            double y = plotArea.Bottom - ((value - _viewMinValue) / valueSpan * plotArea.Height);
            if (y < plotArea.Top - 0.5 || y > plotArea.Bottom + 0.5)
            {
                continue;
            }

            Pen pen = SelectGridPen(value, step, gridPen, majorGridPen, axisPen);
            context.DrawLine(pen, new Point(plotArea.Left, y), new Point(plotArea.Right, y));
        }
    }

    private void DrawVerticalGridLines(DrawingContext context, Rect plotArea)
    {
        Pen gridPen = new(GridBrush, 1.0);
        Pen majorGridPen = new(MajorGridBrush, 1.0);
        Pen axisPen = new(AxisBrush, 1.0);
        double frameSpan = _viewMaxFrame - _viewMinFrame;
        double step = CalculateGridStep(frameSpan, plotArea.Width);
        if (!double.IsFinite(step) || step <= 0.0)
        {
            return;
        }

        double firstFrame = Math.Ceiling(_viewMinFrame / step) * step;
        for (double frame = firstFrame; frame <= _viewMaxFrame + (step * 0.5); frame += step)
        {
            double x = plotArea.Left + ((frame - _viewMinFrame) / frameSpan * plotArea.Width);
            if (x < plotArea.Left - 0.5 || x > plotArea.Right + 0.5)
            {
                continue;
            }

            Pen pen = SelectGridPen(frame, step, gridPen, majorGridPen, axisPen);
            context.DrawLine(pen, new Point(x, plotArea.Top), new Point(x, plotArea.Bottom));
        }
    }

    private void DrawPlotBorder(DrawingContext context, Rect plotArea)
    {
        if (PlotBorderBrush is not { } borderBrush)
        {
            return;
        }

        context.DrawRectangle(null, new Pen(borderBrush, 1.0), plotArea);
    }

    private bool HasValidView()
    {
        return double.IsFinite(_viewMinFrame)
            && double.IsFinite(_viewMaxFrame)
            && double.IsFinite(_viewMinValue)
            && double.IsFinite(_viewMaxValue)
            && _viewMaxFrame > _viewMinFrame
            && _viewMaxValue > _viewMinValue;
    }

    private static double CalculateGridStep(double viewSpan, double pixelSpan)
    {
        if (!double.IsFinite(viewSpan) || !double.IsFinite(pixelSpan) || viewSpan <= 0.0 || pixelSpan <= 0.0)
        {
            return 0.0;
        }

        double rawStep = viewSpan / Math.Max(1.0, pixelSpan / MinimumGridSpacingPixels);
        double exponent = Math.Floor(Math.Log10(rawStep));
        double scale = Math.Pow(10.0, exponent);
        double normalized = rawStep / scale;
        double niceNormalized = normalized <= 1.0
            ? 1.0
            : normalized <= 2.0
                ? 2.0
                : normalized <= 5.0
                    ? 5.0
                    : 10.0;
        return niceNormalized * scale;
    }

    private static Pen SelectGridPen(double value, double step, Pen gridPen, Pen majorGridPen, Pen axisPen)
    {
        double tolerance = Math.Abs(step) * 0.0001;
        if (Math.Abs(value) <= tolerance)
        {
            return axisPen;
        }

        double majorStep = step * 5.0;
        double majorRatio = value / majorStep;
        return Math.Abs(majorRatio - Math.Round(majorRatio)) <= 0.0001 ? majorGridPen : gridPen;
    }

    private void OnComponentChanged()
    {
        FitToComponent();
    }

    private void OnSelectedKeyChanged()
    {
        InvalidateVisual();
        SelectedKeyChanged?.Invoke(this, EventArgs.Empty);
    }

    private void PanBy(double deltaX, double deltaY)
    {
        Rect plotArea = GetPlotArea(Bounds.Size);
        double frameSpan = _viewMaxFrame - _viewMinFrame;
        double valueSpan = _viewMaxValue - _viewMinValue;
        double frameDelta = -(deltaX / Math.Max(plotArea.Width, 1.0)) * frameSpan;
        double valueDelta = (deltaY / Math.Max(plotArea.Height, 1.0)) * valueSpan;
        _viewMinFrame += frameDelta;
        _viewMaxFrame += frameDelta;
        _viewMinValue += valueDelta;
        _viewMaxValue += valueDelta;
        InvalidateVisual();
    }

    private Point ToScreenPoint(Rect plotArea, double frame, double value)
    {
        double x = plotArea.Left + ((frame - _viewMinFrame) / (_viewMaxFrame - _viewMinFrame) * plotArea.Width);
        double y = plotArea.Bottom - ((value - _viewMinValue) / (_viewMaxValue - _viewMinValue) * plotArea.Height);
        return new Point(x, y);
    }

    private double ToViewFrame(Rect plotArea, double x)
    {
        return _viewMinFrame + (((x - plotArea.Left) / Math.Max(plotArea.Width, 1.0)) * (_viewMaxFrame - _viewMinFrame));
    }

    private double ToViewValue(Rect plotArea, double y)
    {
        return _viewMinValue + (((plotArea.Bottom - y) / Math.Max(plotArea.Height, 1.0)) * (_viewMaxValue - _viewMinValue));
    }

    private void ZoomAt(Point position, double frameFactor, double valueFactor)
    {
        Rect plotArea = GetPlotArea(Bounds.Size);
        double anchorFrame = ToViewFrame(plotArea, position.X);
        double anchorValue = ToViewValue(plotArea, position.Y);
        double safeFrameFactor = Math.Clamp(frameFactor, 0.05, 20.0);
        double safeValueFactor = Math.Clamp(valueFactor, 0.05, 20.0);
        _viewMinFrame = anchorFrame - ((anchorFrame - _viewMinFrame) * safeFrameFactor);
        _viewMaxFrame = anchorFrame + ((_viewMaxFrame - anchorFrame) * safeFrameFactor);
        _viewMinValue = anchorValue - ((anchorValue - _viewMinValue) * safeValueFactor);
        _viewMaxValue = anchorValue + ((_viewMaxValue - anchorValue) * safeValueFactor);
        EnsureMinimumSpan();
        InvalidateVisual();
    }

    private void EnsureMinimumSpan()
    {
        if (_viewMaxFrame - _viewMinFrame < 0.0001)
        {
            double center = (_viewMinFrame + _viewMaxFrame) * 0.5;
            _viewMinFrame = center - 0.00005;
            _viewMaxFrame = center + 0.00005;
        }

        if (_viewMaxValue - _viewMinValue < 0.0001)
        {
            double center = (_viewMinValue + _viewMaxValue) * 0.5;
            _viewMinValue = center - 0.00005;
            _viewMaxValue = center + 0.00005;
        }
    }
}