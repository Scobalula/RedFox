using System.Windows.Input;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Media;

namespace RedFox.GameExtraction.UI.Controls;

/// <summary>
/// Draws per-channel min/max waveform peaks with a playhead. Clicking or dragging executes
/// <see cref="SeekCommand"/> with the fraction of the duration under the pointer.
/// </summary>
public sealed class AudioWaveformControl : Control
{
    /// <summary>
    /// Defines the <see cref="Peaks"/> property.
    /// </summary>
    public static readonly StyledProperty<(float Min, float Max)[][]?> PeaksProperty = AvaloniaProperty.Register<AudioWaveformControl, (float Min, float Max)[][]?>(nameof(Peaks));

    /// <summary>
    /// Defines the <see cref="Position"/> property.
    /// </summary>
    public static readonly StyledProperty<double> PositionProperty = AvaloniaProperty.Register<AudioWaveformControl, double>(nameof(Position));

    /// <summary>
    /// Defines the <see cref="SeekCommand"/> property.
    /// </summary>
    public static readonly StyledProperty<ICommand?> SeekCommandProperty = AvaloniaProperty.Register<AudioWaveformControl, ICommand?>(nameof(SeekCommand));

    private static readonly IBrush BackgroundBrush = Brush.Parse("#18181B");
    private static readonly IBrush FallbackWaveBrush = Brush.Parse("#4A8FD9");
    private static readonly IPen CenterLinePen = new Pen(Brush.Parse("#2A2A2F"));
    private static readonly IPen PlayheadPen = new Pen(Brush.Parse("#F4F4F8"), 1.5);

    private (float Min, float Max)[][] _columns = [];

    static AudioWaveformControl()
    {
        AffectsRender<AudioWaveformControl>(PeaksProperty, PositionProperty);
        PeaksProperty.Changed.AddClassHandler<AudioWaveformControl>((control, _) => control._columns = []);
    }

    /// <summary>
    /// Gets or sets the per-channel min/max peaks to draw, spread evenly across the width of the control.
    /// </summary>
    public (float Min, float Max)[][]? Peaks
    {
        get => GetValue(PeaksProperty);
        set => SetValue(PeaksProperty, value);
    }

    /// <summary>
    /// Gets or sets the playhead position as a fraction of the total duration.
    /// </summary>
    public double Position
    {
        get => GetValue(PositionProperty);
        set => SetValue(PositionProperty, value);
    }

    /// <summary>
    /// Gets or sets the command executed with the seek fraction when the user clicks or drags.
    /// </summary>
    public ICommand? SeekCommand
    {
        get => GetValue(SeekCommandProperty);
        set => SetValue(SeekCommandProperty, value);
    }

    /// <inheritdoc/>
    public override void Render(DrawingContext context)
    {
        Rect bounds = new(Bounds.Size);
        context.FillRectangle(BackgroundBrush, bounds);

        (float Min, float Max)[][]? peaks = Peaks;
        if (peaks is null || peaks.Length == 0 || peaks[0].Length == 0 || bounds.Width < 1 || bounds.Height < 1)
        {
            return;
        }

        int width = (int)bounds.Width;
        if (_columns.Length == 0 || _columns[0].Length != width)
        {
            _columns = [.. peaks.Select(channel => WaveformPeaks.Reduce(channel, width))];
        }

        IBrush waveBrush = this.TryFindResource("AccentBrush", out object? resource) && resource is IBrush accent ? accent : FallbackWaveBrush;
        double laneHeight = bounds.Height / _columns.Length;

        for (int channel = 0; channel < _columns.Length; channel++)
        {
            double centerY = (channel * laneHeight) + (laneHeight * 0.5);
            double amplitude = Math.Max(1.0, (laneHeight * 0.5) - 3.0);
            (float Min, float Max)[] column = _columns[channel];

            StreamGeometry geometry = new();
            using (StreamGeometryContext figure = geometry.Open())
            {
                figure.BeginFigure(new Point(0, centerY - (column[0].Max * amplitude)), true);
                for (int x = 1; x < width; x++)
                {
                    figure.LineTo(new Point(x, centerY - (column[x].Max * amplitude)));
                }

                for (int x = width - 1; x >= 0; x--)
                {
                    figure.LineTo(new Point(x, centerY - (column[x].Min * amplitude)));
                }

                figure.EndFigure(true);
            }

            context.DrawLine(CenterLinePen, new Point(0, centerY), new Point(bounds.Width, centerY));
            context.DrawGeometry(waveBrush, null, geometry);
        }

        double playheadX = Math.Round(Math.Clamp(Position, 0.0, 1.0) * bounds.Width) + 0.5;
        context.DrawLine(PlayheadPen, new Point(playheadX, 0), new Point(playheadX, bounds.Height));
    }

    /// <inheritdoc/>
    protected override void OnPointerPressed(PointerPressedEventArgs e)
    {
        base.OnPointerPressed(e);
        if (e.GetCurrentPoint(this).Properties.IsLeftButtonPressed)
        {
            e.Pointer.Capture(this);
            Seek(e);
        }
    }

    /// <inheritdoc/>
    protected override void OnPointerMoved(PointerEventArgs e)
    {
        base.OnPointerMoved(e);
        if (ReferenceEquals(e.Pointer.Captured, this))
        {
            Seek(e);
        }
    }

    /// <inheritdoc/>
    protected override void OnPointerReleased(PointerReleasedEventArgs e)
    {
        base.OnPointerReleased(e);
        e.Pointer.Capture(null);
    }

    private void Seek(PointerEventArgs e)
    {
        if (Bounds.Width <= 0 || SeekCommand is not { } command)
        {
            return;
        }

        double fraction = Math.Clamp(e.GetPosition(this).X / Bounds.Width, 0.0, 1.0);
        if (command.CanExecute(fraction))
        {
            command.Execute(fraction);
        }
    }
}
