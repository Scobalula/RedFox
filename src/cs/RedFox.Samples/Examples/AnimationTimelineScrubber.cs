using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Media;
using BindingMode = global::Avalonia.Data.BindingMode;

namespace RedFox.Samples.Examples;

public sealed class AnimationTimelineScrubber : Control
{
    public static readonly StyledProperty<double> MinimumProperty =
        AvaloniaProperty.Register<AnimationTimelineScrubber, double>(nameof(Minimum));

    public static readonly StyledProperty<double> MaximumProperty =
        AvaloniaProperty.Register<AnimationTimelineScrubber, double>(nameof(Maximum), 1.0);

    public static readonly StyledProperty<double> ValueProperty =
        AvaloniaProperty.Register<AnimationTimelineScrubber, double>(nameof(Value), defaultBindingMode: BindingMode.TwoWay);

    private const double ThumbRadius = 10.0;
    private const double TrackThickness = 2.0;

    private readonly IBrush _trackBrush = new SolidColorBrush(Color.FromRgb(52, 52, 56));
    private readonly IBrush _thumbBrush = new SolidColorBrush(Color.FromRgb(118, 118, 122));
    private bool _isDragging;

    static AnimationTimelineScrubber()
    {
        AffectsRender<AnimationTimelineScrubber>(
            MinimumProperty,
            MaximumProperty,
            ValueProperty,
            IsEnabledProperty);
    }

    public AnimationTimelineScrubber()
    {
        Cursor = new Cursor(StandardCursorType.Hand);
        Focusable = true;
        MinHeight = ThumbRadius * 2.0;
    }

    public double Minimum
    {
        get => GetValue(MinimumProperty);
        set => SetValue(MinimumProperty, value);
    }

    public double Maximum
    {
        get => GetValue(MaximumProperty);
        set => SetValue(MaximumProperty, value);
    }

    public double Value
    {
        get => GetValue(ValueProperty);
        set => SetValue(ValueProperty, value);
    }

    public override void Render(DrawingContext context)
    {
        base.Render(context);

        double centerY = Bounds.Height * 0.5;
        double trackLeft = ThumbRadius;
        double trackRight = Math.Max(trackLeft, Bounds.Width - ThumbRadius);
        Pen trackPen = new(IsEnabled ? _trackBrush : Brushes.DimGray, TrackThickness);
        context.DrawLine(trackPen, new Point(trackLeft, centerY), new Point(trackRight, centerY));

        double thumbX = GetThumbX(trackLeft, trackRight);
        IBrush thumbBrush = IsEnabled ? _thumbBrush : Brushes.DimGray;
        context.DrawEllipse(thumbBrush, null, new Point(thumbX, centerY), ThumbRadius, ThumbRadius);
    }

    protected override void OnPointerPressed(PointerPressedEventArgs e)
    {
        base.OnPointerPressed(e);
        if (!IsEnabled || !e.GetCurrentPoint(this).Properties.IsLeftButtonPressed)
        {
            return;
        }

        Focus(NavigationMethod.Pointer);
        _isDragging = true;
        e.Pointer.Capture(this);
        UpdateValueFromPoint(e.GetPosition(this));
        e.Handled = true;
    }

    protected override void OnPointerMoved(PointerEventArgs e)
    {
        base.OnPointerMoved(e);
        if (!_isDragging || !IsEnabled)
        {
            return;
        }

        UpdateValueFromPoint(e.GetPosition(this));
        e.Handled = true;
    }

    protected override void OnPointerReleased(PointerReleasedEventArgs e)
    {
        base.OnPointerReleased(e);
        if (!_isDragging)
        {
            return;
        }

        _isDragging = false;
        e.Pointer.Capture(null);
        e.Handled = true;
    }

    private double GetThumbX(double trackLeft, double trackRight)
    {
        double span = Maximum - Minimum;
        if (!double.IsFinite(span) || span <= 0.0)
        {
            return trackLeft;
        }

        double ratio = Math.Clamp((Value - Minimum) / span, 0.0, 1.0);
        return trackLeft + (ratio * (trackRight - trackLeft));
    }

    private void UpdateValueFromPoint(Point point)
    {
        double trackLeft = ThumbRadius;
        double trackRight = Math.Max(trackLeft, Bounds.Width - ThumbRadius);
        double span = Maximum - Minimum;
        if (!double.IsFinite(span) || span <= 0.0 || trackRight <= trackLeft)
        {
            SetCurrentValue(ValueProperty, Minimum);
            return;
        }

        double ratio = Math.Clamp((point.X - trackLeft) / (trackRight - trackLeft), 0.0, 1.0);
        SetCurrentValue(ValueProperty, Minimum + (ratio * span));
    }
}
